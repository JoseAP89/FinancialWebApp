using FinancialApp.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FinancialApp.Core.Helpers;

namespace FinancialApp.Core.Data.Repositories
{
    public class AccountRepository : Repository<Account>, IAccountRepository
    {
        public AccountRepository(FinancialDbContext context) : base(context)
        {
        }

        public override async Task AddAsync(Account entity)
        {
            entity.IsSystem = false; // Ensure new accounts are not system accounts by default
            entity.CreatedAt = DateTime.UtcNow;
            await base.AddAsync(entity);
        }

        public async Task<Account?> GetByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Account name cannot be null or whitespace.", nameof(name));
            }

            // Trim and normalize the search term
            var normalizedName = name.Trim();

            // Use case-insensitive search with collation or ToLower/ToUpper
            // For SQL Server, you can use EF.Functions.Like or collation
            return await _dbSet
                .AsNoTracking() // Improves performance for read-only queries
                .Where(a => a.Name != null && a.Name.ToUpper() == normalizedName.ToUpper())
                // Or use EF.Functions for better performance:
                // .Where(a => EF.Functions.Like(a.Name, normalizedName))
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Account>> GetAllParentAccountsAsync()
        {
            return await _dbSet
                .AsNoTracking()
                .Where(a => a.ParentId == null)
                .OrderBy(a => a.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Account>> ListSystemAccountsAsync()
        {
            return await _dbSet
                .Where(a => a.IsSystem)
                .ToListAsync();
        }


        public async Task<IEnumerable<Account>> GetAllChildAccountsByParentIdAsync(int parentId)
        {
            return await _dbSet
                .Where(a => a.ParentId == parentId)
                .OrderBy(a => a.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Account>> GetVisibleParentAccountsAsync()
        {
            return await _dbSet
                .AsNoTracking()
                .Where(a => a.ParentId == null
                            && !a.IsSystem
                            && a.FinancialStatement != FinancialStatement.EQUITY)
                .OrderBy(a => a.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Account>> GetVisibleChildAccountsByParentIdAsync(int parentId)
        {
            return await _dbSet
                .AsNoTracking()
                .Where(a => a.ParentId == parentId
                            && !a.IsSystem
                            && a.FinancialStatement != FinancialStatement.EQUITY)
                .OrderBy(a => a.Name)
                .ToListAsync();
        }

        public async Task<decimal> GetAccountTotalAsync(int accountId, DateOnly? beginDate = null, DateOnly? endDate = null, bool includeChildren = false)
        {
            // Build the base transaction line query for the account
            var tlQuery = _context.TransactionLines
                .AsNoTracking()
                .Where(tl => tl.AccountId == accountId);

            if (includeChildren)
            {
                // Collect descendant ids by querying only the relevant subtree iteratively.
                var accountIds = _context.Accounts
                    .Where(a => a.ParentId == accountId)
                    .Select(a => a.Id)
                    .ToList();

                // Replace tlQuery to filter by all collected account ids
                tlQuery = _context.TransactionLines
                    .AsNoTracking()
                    .Where(tl => accountIds.Contains(tl.AccountId));
            }

            // Apply optional date filters by joining Transaction (navigation property)
            if (beginDate.HasValue)
            {
                var startUtc = DateUtils.ConvertToUTCPostgreSQL(beginDate.Value);
                tlQuery = tlQuery.Where(tl => tl.Transaction != null && tl.Transaction.Date >= startUtc);
            }

            if (endDate.HasValue)
            {
                var endUtc = DateUtils.ConvertToUTCPostgreSQL(endDate.Value, 1);
                tlQuery = tlQuery.Where(tl => tl.Transaction != null && tl.Transaction.Date < endUtc);
            }

            // Execute single-server aggregate; multiply Amount * Quantity
            var total = await tlQuery.SumAsync(tl => tl.Amount * tl.Quantity);
            return total;
        }

        public async Task DeleteAccountByIdAsync(int id)
        {
            var account = await _dbSet.FindAsync(id);

            // Nothing to delete when the account does not exist.
            if (account is null)
            {
                return;
            }

            // An account cannot be deleted while transaction lines reference it. There is currently
            // no way to link an auto-balance created transaction line back to the lines that produced
            // it, so the associated lines cannot be safely removed/realigned automatically.
            var hasTransactionLines = await _context.TransactionLines
                .AnyAsync(tl => tl.AccountId == id);

            if (hasTransactionLines)
            {
                throw new InvalidOperationException("The account has transaction lines associated with it. It cannot be deleted");
            }

            // An account that is the parent of one or more other accounts cannot be deleted, otherwise
            // those children would be orphaned.
            var hasChildren = await _context.Accounts
                .AnyAsync(a => a.ParentId == id);

            if (hasChildren)
            {
                throw new InvalidOperationException($"The account {account.Name} has children accounts. It cannot be deleted");
            }

            Remove(account);
            await SaveChangesAsync();
        }

        public async Task UpdateAccountByIdAsync(int id, string name, string description, FinancialStatement financialStatement, int? parentId)
        {
            var account = await _dbSet.FindAsync(id);

            // The account must exist for it to be updated.
            if (account is null)
            {
                throw new InvalidOperationException($"The account with id {id} does not exist. It cannot be updated");
            }

            // An account that is the parent of one or more other accounts cannot be updated, otherwise
            // its children would be left with an invalid hierarchy.
            var hasChildren = await _dbSet.AnyAsync(a => a.ParentId == id);

            if (hasChildren)
            {
                throw new InvalidOperationException($"The account {account.Name} has children accounts. It cannot be updated");
            }

            // An account cannot be updated while transaction lines reference it. There is currently no way
            // to link an auto-balance created transaction line back to the lines that produced it, so the
            // associated lines cannot be safely realigned automatically.
            var hasTransactionLines = await _context.TransactionLines
                .AnyAsync(tl => tl.AccountId == id);

            if (hasTransactionLines)
            {
                throw new InvalidOperationException($"The account {account.Name} has transaction lines associated with it. It cannot be updated");
            }

            var normalizedName = name?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                throw new InvalidOperationException("The account name cannot be empty. It cannot be updated");
            }

            // The name must stay unique across accounts, mirroring the validation applied when creating one.
            var hasDuplicateName = await _dbSet
                .AnyAsync(a => a.Id != id && a.Name != null && a.Name.ToUpper() == normalizedName.ToUpper());

            if (hasDuplicateName)
            {
                throw new InvalidOperationException($"An account with the name {normalizedName} already exists");
            }

            // The parent (when provided) must be a valid account that is not the account itself and belongs
            // to the same financial statement as the account.
            if (parentId.HasValue)
            {
                if (parentId.Value == id)
                {
                    throw new InvalidOperationException($"The account {account.Name} cannot be its own parent");
                }

                var parent = await _dbSet.FindAsync(parentId.Value);

                if (parent is null)
                {
                    throw new InvalidOperationException($"The parent account with id {parentId.Value} does not exist. The account cannot be updated");
                }

                if (parent.FinancialStatement != financialStatement)
                {
                    throw new InvalidOperationException($"The parent account {parent.Name} belongs to a different financial statement. The account cannot be updated");
                }
            }

            account.Name = normalizedName;
            account.Description = description;
            account.FinancialStatement = financialStatement;
            account.ParentId = parentId;

            await SaveChangesAsync();
        }
    }

}
