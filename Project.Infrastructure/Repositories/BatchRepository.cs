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

        // Базовые CRUD операции
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
                            .ThenInclude(rs => rs.MachineType)
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

        // Операции с подпартиями
        public async Task<SubBatch?> GetSubBatchByIdAsync(int id) =>
            await _db.SubBatches
                .Include(sb => sb.Batch)
                    .ThenInclude(b => b.Detail)
                .Include(sb => sb.StageExecutions)
                    .ThenInclude(se => se.RouteStage)
                        .ThenInclude(rs => rs.MachineType)
                .Include(sb => sb.StageExecutions)
                    .ThenInclude(se => se.Machine)
                .FirstOrDefaultAsync(sb => sb.Id == id);

        public async Task UpdateSubBatchAsync(SubBatch entity)
        {
            _db.SubBatches.Update(entity);
            await _db.SaveChangesAsync();
        }

        // Операции с этапами выполнения
        public async Task<StageExecution?> GetStageExecutionByIdAsync(int id) =>
            await _db.StageExecutions
                .Include(se => se.SubBatch)
                    .ThenInclude(sb => sb.Batch)
                        .ThenInclude(b => b.Detail)
                .Include(se => se.RouteStage)
                    .ThenInclude(rs => rs.MachineType)
                .Include(se => se.Machine)
                .FirstOrDefaultAsync(se => se.Id == id);

        public async Task UpdateStageExecutionAsync(StageExecution entity)
        {
            _db.StageExecutions.Update(entity);
            await _db.SaveChangesAsync();
        }

        public async Task<List<StageExecution>> GetAllStageExecutionsAsync() =>
            await _db.StageExecutions
                .Include(se => se.SubBatch)
                    .ThenInclude(sb => sb.Batch)
                        .ThenInclude(b => b.Detail)
                .Include(se => se.RouteStage)
                    .ThenInclude(rs => rs.MachineType)
                .Include(se => se.Machine)
                .ToListAsync();

        // Специализированные запросы для планирования

        // Последний завершенный этап на станке
        public async Task<StageExecution?> GetLastCompletedStageOnMachineAsync(int machineId) =>
            await _db.StageExecutions
                .Include(se => se.SubBatch)
                    .ThenInclude(sb => sb.Batch)
                        .ThenInclude(b => b.Detail)
                .Include(se => se.RouteStage)
                .Where(se => se.MachineId == machineId &&
                             se.Status == StageExecutionStatus.Completed &&
                             !se.IsSetup) // берем только основные операции, не переналадку
                .OrderByDescending(se => se.EndTimeUtc)
                .FirstOrDefaultAsync();

        // Текущий этап, выполняемый на станке
        public async Task<StageExecution?> GetCurrentStageOnMachineAsync(int machineId) =>
            await _db.StageExecutions
                .Include(se => se.SubBatch)
                    .ThenInclude(sb => sb.Batch)
                        .ThenInclude(b => b.Detail)
                .Include(se => se.RouteStage)
                .Where(se => se.MachineId == machineId &&
                             se.Status == StageExecutionStatus.InProgress)
                .FirstOrDefaultAsync();

        // Этап переналадки для основного этапа
        public async Task<StageExecution?> GetSetupStageForMainStageAsync(int mainStageId) =>
            await _db.StageExecutions
                .Include(se => se.SubBatch)
                .Include(se => se.RouteStage)
                .Include(se => se.Machine)
                .Where(se => se.IsSetup &&
                             se.SubBatch.StageExecutions.Any(mse => mse.Id == mainStageId && !mse.IsSetup) &&
                             se.RouteStageId == _db.StageExecutions
                                 .Where(mse => mse.Id == mainStageId)
                                 .Select(mse => mse.RouteStageId)
                                 .FirstOrDefault())
                .FirstOrDefaultAsync();

        // Основной этап для этапа переналадки
        public async Task<StageExecution?> GetMainStageForSetupAsync(int setupStageId) =>
            await _db.StageExecutions
                .Include(se => se.SubBatch)
                .Include(se => se.RouteStage)
                .Include(se => se.Machine)
                .Where(se => !se.IsSetup &&
                             se.SubBatchId == _db.StageExecutions
                                 .Where(sse => sse.Id == setupStageId)
                                 .Select(sse => sse.SubBatchId)
                                 .FirstOrDefault() &&
                             se.RouteStageId == _db.StageExecutions
                                 .Where(sse => sse.Id == setupStageId)
                                 .Select(sse => sse.RouteStageId)
                                 .FirstOrDefault())
                .FirstOrDefaultAsync();

        // Следующий этап в очереди для станка
        public async Task<StageExecution?> GetNextStageInQueueForMachineAsync(int machineId) =>
            await _db.StageExecutions
                .Include(se => se.SubBatch)
                    .ThenInclude(sb => sb.Batch)
                        .ThenInclude(b => b.Detail)
                .Include(se => se.RouteStage)
                .Where(se => se.MachineId == machineId &&
                             se.Status == StageExecutionStatus.Waiting)
                .OrderBy(se => se.SubBatch.Batch.CreatedUtc) // по дате создания партии
                .FirstOrDefaultAsync();

        // Все этапы в очереди
        public async Task<List<StageExecution>> GetAllStagesInQueueAsync() =>
            await _db.StageExecutions
                .Include(se => se.SubBatch)
                    .ThenInclude(sb => sb.Batch)
                        .ThenInclude(b => b.Detail)
                .Include(se => se.RouteStage)
                    .ThenInclude(rs => rs.MachineType)
                .Where(se => se.Status == StageExecutionStatus.Waiting)
                .OrderBy(se => se.SubBatch.Batch.CreatedUtc) // по дате создания партии
                .ToListAsync();

        // Этапы в очереди на конкретный станок
        public async Task<List<StageExecution>> GetQueuedStagesForMachineAsync(int machineId) =>
            await _db.StageExecutions
                .Include(se => se.SubBatch)
                .Include(se => se.RouteStage)
                .Where(se => se.MachineId == machineId &&
                             se.Status == StageExecutionStatus.Waiting)
                .OrderBy(se => se.SubBatch.Batch.CreatedUtc) // по дате создания партии
                .ToListAsync();

        // История и отчетность
        public async Task<List<StageExecution>> GetStageExecutionHistoryAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? machineId = null,
            int? detailId = null)
        {
            var query = _db.StageExecutions
                .Include(se => se.SubBatch)
                    .ThenInclude(sb => sb.Batch)
                        .ThenInclude(b => b.Detail)
                .Include(se => se.RouteStage)
                    .ThenInclude(rs => rs.MachineType)
                .Include(se => se.Machine)
                .AsQueryable();

            // Фильтрация по дате начала
            if (startDate.HasValue)
            {
                query = query.Where(se =>
                    (se.StartTimeUtc.HasValue && se.StartTimeUtc >= startDate) ||
                    (se.EndTimeUtc.HasValue && se.EndTimeUtc >= startDate));
            }

            // Фильтрация по дате окончания
            if (endDate.HasValue)
            {
                query = query.Where(se =>
                    (!se.StartTimeUtc.HasValue) ||
                    (se.StartTimeUtc.HasValue && se.StartTimeUtc <= endDate));
            }

            // Фильтрация по станку
            if (machineId.HasValue)
            {
                query = query.Where(se => se.MachineId == machineId);
            }

            // Фильтрация по детали
            if (detailId.HasValue)
            {
                query = query.Where(se => se.SubBatch.Batch.DetailId == detailId);
            }

            // Сортировка по времени начала
            return await query
                .OrderByDescending(se => se.StartTimeUtc)
                .ToListAsync();
        }
    }
}