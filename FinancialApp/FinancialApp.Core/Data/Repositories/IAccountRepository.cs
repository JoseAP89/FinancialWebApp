using FinancialApp.Data.Models;
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
    }
}
