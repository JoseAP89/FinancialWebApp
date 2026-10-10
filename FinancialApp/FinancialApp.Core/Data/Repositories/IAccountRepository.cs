using FinancialApp.Data.Models;
using System;
using System.Threading.Tasks;

namespace FinancialApp.Core.Data.Repositories
{
    public interface IAccountRepository : IRepository<Account>
    {
        Task<Account?> GetByNameAsync(string name);
        Task<IEnumerable<Account>> GetAllParentAccountsAsync();
        Task<IEnumerable<Account>> ListSystemAccountsAsync();
        Task<IEnumerable<Account>> GetAllChildAccountsByParentIdAsync(int parentId);
        Task<IEnumerable<Account>> GetVisibleParentAccountsAsync();
        Task<IEnumerable<Account>> GetVisibleChildAccountsByParentIdAsync(int parentId);
        // Returns the total amount for the given account including its descendant accounts.
        // beginDate and endDate are optional; when provided they filter by Transaction.Date (inclusive start, exclusive end + 1 day).
        Task<decimal> GetAccountTotalAsync(int accountId, DateOnly? beginDate = null, DateOnly? endDate = null, bool includeChildren = false);
        // Deletes the account with the given id when no transaction lines reference it.
        // Throws an InvalidOperationException when transaction lines are associated with the account,
        // because auto-balance created lines cannot currently be linked back to their originating lines.
        Task DeleteAccountByIdAsync(int id);
    }
}
