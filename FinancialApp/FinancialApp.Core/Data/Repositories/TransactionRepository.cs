using FinancialApp.Core.Helpers;
using FinancialApp.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FinancialApp.Core.Data.Repositories;

public class TransactionRepository : Repository<Transaction>, ITransactionRepository
{
    private readonly ITransactionLineRepository _transactionLineRepository;

    public TransactionRepository(FinancialDbContext context, ITransactionLineRepository transactionLineRepository) : base(context)
    {
        _transactionLineRepository = transactionLineRepository;
    }

    public override async Task AddAsync(Transaction entity)
    {
        // Set server local time on create
        entity.Date = DateTime.UtcNow;
        await base.AddAsync(entity);
    }

    // Deliberately no Update override: editing a transaction must leave its date alone. The date the
    // transaction happened is not something the edit dialog changes, and the moment of the edit will be
    // carried by its own dedicated column, added in a later change.

    // Persists the edit made in the "Edit transaction" dialog: the header name plus every line the dialog
    // handed back. Each line is written through the line repository so the auto-balance entry that
    // compensates it follows the edit and the transaction stays balanced.
    public async Task UpdateTransactionWithLinesAsync(Transaction transaction)
    {
        if (transaction is null)
        {
            throw new ArgumentNullException(nameof(transaction));
        }

        // The stored row is updated instead of the incoming transaction: the header edit is applied to
        // the tracked entity and the lines are only written by the loop below, so no line is flushed
        // without its auto-balance entry being realigned first.
        var stored = await _dbSet.FirstOrDefaultAsync(t => t.Id == transaction.Id);

        if (stored is null)
        {
            throw new InvalidOperationException($"The transaction with id {transaction.Id} does not exist. It cannot be updated");
        }

        stored.Description = transaction.Description;

        // Saved through the change tracker, the way UpdateAccountByIdAsync does it: only the edited columns
        // are written, so the date the transaction happened keeps the value it was created with. The header
        // goes in first, mirroring how a transaction is created, so a failure while writing a line still
        // leaves the edited header readable.
        await SaveChangesAsync();

        if (transaction.TransactionLines is not null)
        {
            foreach (var line in transaction.TransactionLines)
            {
                await _transactionLineRepository.UpdateLineWithAutoBalanceAsync(line);
            }
        }
    }

    // Override GetByIdAsync to include TransactionLines
    public override async Task<Transaction?> GetByIdAsync(int id)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(t => t.TransactionLines)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    // Override ListAsync to include TransactionLines
    public override async Task<IEnumerable<Transaction>> ListAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Include(t => t.TransactionLines)
            .ToListAsync();
    }

    public async Task<Transaction?> GetByIdWithLinesAsync(int id)
    {
        return await GetByIdAsync(id);
    }

    public async Task<IEnumerable<Transaction>> ListWithLinesAsync()
    {
        return await ListAsync();
    }

    public async Task<IEnumerable<Transaction>> ListWithNoSystemLinesByDateRangeAsync(DateOnly start, DateOnly end)
    {
        var startUtc = DateUtils.ConvertToUTCPostgreSQL(start);
        var endUtc = DateUtils.ConvertToUTCPostgreSQL(end, 1);

        return await _dbSet
            .AsNoTracking()
            .Where(t => t.Date >= startUtc && t.Date < endUtc)
            .Include(t => t.TransactionLines.Where(l => l.Account != null && !l.Account.IsSystem && l.AutoBalancedByLineId != null))
                .ThenInclude(l => l.Account)
            .OrderByDescending(t => t.Date)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalExpenses(DateOnly? start, DateOnly? end)
    {
        if (start is null)
        {
            start = DateOnly.MinValue;
        }
        if (end is null)
        {
            end = DateOnly.FromDateTime(DateTime.Now);
        }
        var startUtc = DateUtils.ConvertToUTCPostgreSQL(start.Value);
        var endUtc = DateUtils.ConvertToUTCPostgreSQL(end.Value, 1);

        return await _dbSet
            .AsNoTracking()
            .Where(t => t.Date >= startUtc && t.Date < endUtc)
            .SelectMany(t => t.TransactionLines)
            .Where(l => l.Account != null
                     && l.Account.FinancialStatement == FinancialStatement.EXPENSE)
            .SumAsync(l => l.Amount * l.Quantity);
    }

}
