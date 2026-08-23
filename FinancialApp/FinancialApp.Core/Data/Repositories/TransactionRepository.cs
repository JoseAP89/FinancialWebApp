using FinancialApp.Core.Helpers;
using FinancialApp.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FinancialApp.Core.Data.Repositories;

public class TransactionRepository : Repository<Transaction>, ITransactionRepository
{
    public TransactionRepository(FinancialDbContext context) : base(context)
    {
    }

    public override async Task AddAsync(Transaction entity)
    {
        // Set server local time on create
        entity.Date = DateTime.UtcNow;
        await base.AddAsync(entity);
    }

    public override void Update(Transaction entity)
    {
        // Update the date to server local time on update
        entity.Date = DateTime.UtcNow;
        base.Update(entity);
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
            .Include(t => t.TransactionLines.Where(l => l.Account != null && !l.Account.IsSystem && !l.IsAutoBalanced))
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
