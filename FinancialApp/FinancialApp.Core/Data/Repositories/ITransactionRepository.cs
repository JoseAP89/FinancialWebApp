using FinancialApp.Data.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FinancialApp.Core.Data.Repositories
{
    public interface ITransactionRepository : IRepository<Transaction>
    {
        // Returns a transaction including its transaction lines
        Task<Transaction?> GetByIdWithLinesAsync(int id);

        // Returns all transactions including their transaction lines
        Task<IEnumerable<Transaction>> ListWithLinesAsync();

        // Returns transactions including their lines, without system account lines, between the given start and end (inclusive)
        Task<IEnumerable<Transaction>> ListWithNoSystemLinesByDateRangeAsync(DateOnly start, DateOnly end);

        // Returns the total expenses within the given date range
        Task<decimal> GetTotalExpenses(DateOnly? start, DateOnly? end);

        // Persists the header (description and server generated date) of the given transaction together
        // with every line it carries, keeping the auto-balance entries that compensate those lines in
        // sync. Throws an InvalidOperationException when the transaction does not exist.
        Task UpdateTransactionWithLinesAsync(Transaction transaction);
    }
}
