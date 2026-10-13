using FinancialApp.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FinancialApp.Core.Data.Repositories
{
    public class TransactionLineRepository : Repository<TransactionLine>, ITransactionLineRepository
    {
        public TransactionLineRepository(FinancialDbContext context) : base(context)
        {
        }

        // Normalizes the quantity before the change tracker flushes the line: the quantity column has a
        // CHECK (quantity >= 1) constraint, so a cleared or zeroed field must not reach the database.
        // The name, quantity and amount edits themselves are applied by UpdateLineWithAutoBalanceAsync.
        public override void Update(TransactionLine entity)
        {
            if (entity.Quantity <= 0)
            {
                entity.Quantity = 1;
            }

            base.Update(entity);
        }

        public override async Task<TransactionLine?> GetByIdAsync(int id)
        {
            return await _dbSet
                .Include(tl => tl.Transaction)
                .Include(tl => tl.Account)
                .FirstOrDefaultAsync(tl => tl.Id == id);
        }

        public override async Task<IEnumerable<TransactionLine>> ListAsync()
        {
            return await _dbSet
                .Include(tl => tl.Transaction)
                .Include(tl => tl.Account)
                .ToListAsync();
        }

        public async Task<TransactionLine?> GetByIdWithRelationsAsync(int id)
        {
            return await GetByIdAsync(id);
        }

        public async Task<IEnumerable<TransactionLine>> ListWithRelationsAsync()
        {
            return await ListAsync();
        }

        // Persists the edits of a single line (name, quantity and amount) and keeps the transaction
        // balanced: the entry this line points to (AutoBalancedByLineId) is moved by the inverse of the
        // line's change, so a balanced transaction (line + entry = 0) stays balanced. Both values are
        // quantity scaled, matching the way the balancing service computed the entry when it created it.
        public async Task UpdateLineWithAutoBalanceAsync(TransactionLine line)
        {
            if (line is null)
            {
                throw new ArgumentNullException(nameof(line));
            }

            // The stored row is updated instead of the incoming line so the previous value comes from the
            // database and the edit is applied to the tracked entity (the line handed in comes from an
            // AsNoTracking read of the financial history).
            var stored = await _dbSet.FirstOrDefaultAsync(tl => tl.Id == line.Id);

            if (stored is null)
            {
                throw new InvalidOperationException($"The transaction line with id {line.Id} does not exist. It cannot be updated");
            }

            // Only the fields exposed by the edit dialog change: the transaction, the account and the
            // auto-balance lineage stay as they were stored.
            var previousValue = LineValue(stored);

            stored.Description = line.Description;
            stored.Quantity = line.Quantity <= 0 ? 1 : line.Quantity;
            stored.Amount = line.Amount;

            Update(stored);

            await AdjustAutoBalanceEntryAsync(stored.AutoBalancedByLineId, LineValue(stored) - previousValue);
            await SaveChangesAsync();
        }

        // Moves the auto-balance entry that compensates the given line by the inverse of the line's
        // change. The entry holds the negation of the value of the lines it balances, so taking the old
        // value away from the transaction means giving it back to the entry, and adding the new value to
        // the transaction means taking it away from the entry: entry = entry - (new value - old value).
        private async Task AdjustAutoBalanceEntryAsync(int? autoBalanceLineId, decimal delta)
        {
            if (!autoBalanceLineId.HasValue || delta == 0m)
            {
                return;
            }

            var entry = await _dbSet.FirstOrDefaultAsync(tl => tl.Id == autoBalanceLineId.Value);

            if (entry is null)
            {
                throw new InvalidOperationException(
                    $"The auto-balance entry with id {autoBalanceLineId.Value} does not exist. The transaction line cannot be updated");
            }

            // The entry is a single unit line, so the whole change is carried by its amount.
            entry.Amount -= delta;
        }

        // Value the balancing service assigns to a line: its amount times its quantity, where a missing
        // quantity counts as a single unit.
        private static decimal LineValue(TransactionLine line)
        {
            return line.Amount * (line.Quantity <= 0 ? 1 : line.Quantity);
        }
    }
}
