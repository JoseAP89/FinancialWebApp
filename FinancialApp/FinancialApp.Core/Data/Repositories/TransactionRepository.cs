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
        // Convert DateOnly to LOCAL DateTime first
        var startLocal = new DateTime(start.Year, start.Month, start.Day, 0, 0, 0, DateTimeKind.Local);
        var endLocal = new DateTime(end.Year, end.Month, end.Day, 0, 0, 0, DateTimeKind.Local).AddDays(1);

        // Convert to UTC for PostgreSQL
        var startUtc = startLocal.ToUniversalTime();
        var endUtc = endLocal.ToUniversalTime();

        Console.WriteLine($"Start (Local): {startLocal:yyyy-MM-dd HH:mm:ss.fff}");
        Console.WriteLine($"Start (UTC): {startUtc:yyyy-MM-dd HH:mm:ss.fff}");
        Console.WriteLine($"End (Local): {endLocal:yyyy-MM-dd HH:mm:ss.fff}");
        Console.WriteLine($"End (UTC): {endUtc:yyyy-MM-dd HH:mm:ss.fff}");

        return await _dbSet
            .AsNoTracking()
            .Where(t => t.Date >= startUtc && t.Date < endUtc)
            .Include(t => t.TransactionLines.Where(l => l.Account != null && !l.Account.IsSystem))
                .ThenInclude(l => l.Account)
            .OrderByDescending(t => t.Date)
            .ToListAsync();
    }
}
