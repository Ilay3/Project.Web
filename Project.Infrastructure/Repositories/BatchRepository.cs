using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Repositories;
using Project.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Infrastructure.Repositories
{
    public class BatchRepository : IBatchRepository
    {
        private readonly ManufacturingDbContext _db;

        public BatchRepository(ManufacturingDbContext db)
        {
            _db = db;
        }

        public async Task<List<Batch>> GetAllAsync() =>
            await _db.Batches
                .Include(b => b.Detail)
                .Include(b => b.SubBatches)
                    .ThenInclude(sb => sb.StageExecutions)
                        .ThenInclude(se => se.RouteStage)
                .Include(b => b.SubBatches)
                    .ThenInclude(sb => sb.StageExecutions)
                        .ThenInclude(se => se.Machine)
                .AsNoTracking()
                .ToListAsync();

        public async Task<Batch?> GetByIdAsync(int id) =>
            await _db.Batches
                .Include(b => b.Detail)
                .Include(b => b.SubBatches)
                    .ThenInclude(sb => sb.StageExecutions)
                        .ThenInclude(se => se.RouteStage)
                .Include(b => b.SubBatches)
                    .ThenInclude(sb => sb.StageExecutions)
                        .ThenInclude(se => se.Machine)
                .FirstOrDefaultAsync(b => b.Id == id);

        public async Task AddAsync(Batch entity)
        {
            _db.Batches.Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(Batch entity)
        {
            _db.Batches.Update(entity);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var b = await _db.Batches.FindAsync(id);
            if (b != null)
            {
                _db.Batches.Remove(b);
                await _db.SaveChangesAsync();
            }
        }
    }

}
