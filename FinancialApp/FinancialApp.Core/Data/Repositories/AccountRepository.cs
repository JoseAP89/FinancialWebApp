using FinancialApp.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinancialApp.Core.Data.Repositories
{
    public class AccountRepository : Repository<Account>, IAccountRepository
    {
        public AccountRepository(FinancialDbContext context) : base(context)
        {
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

    }
}
