using FinancialApp.Data.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FinancialApp.Core.Data.Repositories
{
    public interface ITransactionLineRepository : IRepository<TransactionLine>
    {
        Task<TransactionLine?> GetByIdWithRelationsAsync(int id);

        Task<IEnumerable<TransactionLine>> ListWithRelationsAsync();

        // Persists the name, quantity and amount of the given line and keeps the transaction balanced by
        // moving the auto-balance entry the line points to (AutoBalancedByLineId) by the inverse of the
        // line's change. Throws an InvalidOperationException (mirroring UpdateAccountByIdAsync) when the
        // line does not exist or when the entry balancing it is missing.
        Task UpdateLineWithAutoBalanceAsync(TransactionLine line);

        // Deletes every line stored against the given transaction: the user lines and the auto-balance
        // entries that compensate them. The compensated lines go first, so no row still references an
        // auto-balance entry when the entry itself is removed (the self-referencing lineage forbids the
        // other way around). The rows are removed explicitly instead of relying on the database cascade.
        // Nothing happens when the transaction has no lines.
        Task DeleteLinesByTransactionIdAsync(int transactionId);
    }
}
