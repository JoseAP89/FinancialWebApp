using FinancialApp.Data.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FinancialApp.Core.Data.Repositories
{
    public interface ITransactionLineRepository : IRepository<TransactionLine>
    {
        Task<TransactionLine?> GetByIdWithRelationsAsync(int id);

        Task<IEnumerable<TransactionLine>> ListWithRelationsAsync();
    }
}
