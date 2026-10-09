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
    }

}
