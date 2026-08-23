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
    }
}
