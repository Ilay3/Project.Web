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
    public class SetupTimeRepository : ISetupTimeRepository
    {
        private readonly ManufacturingDbContext _db;
        public SetupTimeRepository(ManufacturingDbContext db) => _db = db;

        public async Task<List<SetupTime>> GetAllAsync() =>
            await _db.SetupTimes
                .Include(st => st.Machine)
                .Include(st => st.FromDetail)
                .Include(st => st.ToDetail)
                .AsNoTracking()
                .ToListAsync();

        public async Task<SetupTime?> GetByIdAsync(int id) =>
            await _db.SetupTimes
                .Include(st => st.Machine)
                .Include(st => st.FromDetail)
                .Include(st => st.ToDetail)
                .FirstOrDefaultAsync(st => st.Id == id);

        public async Task<SetupTime?> GetSetupTimeAsync(int machineId, int fromDetailId, int toDetailId) =>
            await _db.SetupTimes
                .Where(st => st.MachineId == machineId &&
                            st.FromDetailId == fromDetailId &&
                            st.ToDetailId == toDetailId)
                .FirstOrDefaultAsync();

        public async Task<Detail?> GetLastDetailOnMachineAsync(int machineId)
        {
            // Находим последний завершенный этап на станке
            var lastStage = await _db.StageExecutions
                .Where(se => se.MachineId == machineId &&
                            se.Status == StageExecutionStatus.Completed &&
                            !se.IsSetup) // только основные операции, не переналадки
                .OrderByDescending(se => se.EndTimeUtc)
                .FirstOrDefaultAsync();

            if (lastStage == null)
                return null;

            // Получаем подпартию этого этапа
            var subBatch = await _db.SubBatches
                .Include(sb => sb.Batch)
                    .ThenInclude(b => b.Detail)
                .FirstOrDefaultAsync(sb => sb.Id == lastStage.SubBatchId);

            if (subBatch == null)
                return null;

            return subBatch.Batch.Detail;
        }

        public async Task AddAsync(SetupTime entity)
        {
            _db.SetupTimes.Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(SetupTime entity)
        {
            _db.SetupTimes.Update(entity);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var st = await _db.SetupTimes.FindAsync(id);
            if (st != null)
            {
                _db.SetupTimes.Remove(st);
                await _db.SaveChangesAsync();
            }
        }
    }
}