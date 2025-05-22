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

        // Получение всех этапов в статусе "Pending" (готовы к запуску)
        public async Task<List<StageExecution>> GetPendingStagesAsync()
        {
            return await _db.StageExecutions
                .Include(se => se.SubBatch)
                    .ThenInclude(sb => sb.Batch)
                        .ThenInclude(b => b.Detail)
                .Include(se => se.RouteStage)
                    .ThenInclude(rs => rs.MachineType)
                .Include(se => se.Machine)
                .Where(se => se.Status == StageExecutionStatus.Pending)
                .OrderBy(se => se.SubBatch.Batch.CreatedUtc)
                .ThenBy(se => se.RouteStage.Order)
                .ToListAsync();
        }


        // Получение недавно завершенных этапов, которые еще не обработаны системой планирования
        public async Task<List<StageExecution>> GetRecentlyCompletedStagesAsync()
        {
            var oneHourAgo = DateTime.UtcNow.AddHours(-1);

            return await _db.StageExecutions
                .Include(se => se.SubBatch)
                    .ThenInclude(sb => sb.Batch)
                        .ThenInclude(b => b.Detail)
                .Include(se => se.RouteStage)
                .Include(se => se.Machine)
                .Where(se => se.Status == StageExecutionStatus.Completed &&
                            se.EndTimeUtc >= oneHourAgo &&
                            !se.IsProcessedByScheduler)
                .OrderBy(se => se.EndTimeUtc)
                .ToListAsync();
        }


        // Получение следующих доступных этапов для указанной подпартии
        public async Task<List<StageExecution>> GetNextAvailableStagesForSubBatchAsync(int subBatchId)
        {
            var subBatch = await _db.SubBatches
                .Include(sb => sb.StageExecutions)
                    .ThenInclude(se => se.RouteStage)
                .FirstOrDefaultAsync(sb => sb.Id == subBatchId);

            if (subBatch == null) return new List<StageExecution>();

            // Находим последний завершенный этап
            var lastCompletedStage = subBatch.StageExecutions
                .Where(se => se.Status == StageExecutionStatus.Completed && !se.IsSetup)
                .OrderByDescending(se => se.RouteStage.Order)
                .FirstOrDefault();

            // Если нет завершенных этапов, возвращаем первый этап
            var nextStageOrder = lastCompletedStage == null ?
                0 : lastCompletedStage.RouteStage.Order;

            // Находим следующие этапы
            return subBatch.StageExecutions
                .Where(se => !se.IsSetup && se.RouteStage.Order > nextStageOrder &&
                            (se.Status == StageExecutionStatus.Pending || se.Status == StageExecutionStatus.Waiting))
                .OrderBy(se => se.RouteStage.Order)
                .ToList();
        }

        // Получение списка свободных станков, подходящих для указанного этапа
        public async Task<List<Machine>> GetAvailableMachinesForStageAsync(int stageExecutionId)
        {
            var stage = await _db.StageExecutions
                .Include(se => se.RouteStage)
                .FirstOrDefaultAsync(se => se.Id == stageExecutionId);

            if (stage == null) return new List<Machine>();

            // Получаем требуемый тип станка
            var machineTypeId = stage.RouteStage.MachineTypeId;

            // Получаем ID занятых станков
            var busyMachineIds = await _db.StageExecutions
                .Where(se => se.Status == StageExecutionStatus.InProgress && se.MachineId.HasValue)
                .Select(se => se.MachineId.Value)
                .Distinct()
                .ToListAsync();

            // Находим свободные станки указанного типа
            return await _db.Machines
                .Include(m => m.MachineType)
                .Where(m => m.MachineTypeId == machineTypeId && !busyMachineIds.Contains(m.Id))
                .OrderByDescending(m => m.Priority)
                .ToListAsync();
        }

        // Обновление статуса обработки для завершенного этапа
        public async Task MarkStageAsProcessedAsync(int stageExecutionId)
        {
            var stage = await _db.StageExecutions.FindAsync(stageExecutionId);
            if (stage != null)
            {
                stage.IsProcessedByScheduler = true;
                await _db.SaveChangesAsync();
            }
        }

        // Получение информации о загрузке станков на указанный период
        public async Task<Dictionary<int, List<StageExecution>>> GetMachineScheduleAsync(DateTime startDate, DateTime endDate)
        {
            var scheduledStages = await _db.StageExecutions
                .Include(se => se.SubBatch)
                    .ThenInclude(sb => sb.Batch)
                        .ThenInclude(b => b.Detail)
                .Include(se => se.RouteStage)
                .Include(se => se.Machine)
                .Where(se => se.MachineId.HasValue &&
                           ((se.StartTimeUtc.HasValue && se.StartTimeUtc >= startDate && se.StartTimeUtc <= endDate) ||
                            (se.EndTimeUtc.HasValue && se.EndTimeUtc >= startDate && se.EndTimeUtc <= endDate) ||
                            (se.Status == StageExecutionStatus.InProgress) ||
                            (se.Status == StageExecutionStatus.Pending && se.MachineId.HasValue)))
                .ToListAsync();

            // Группируем по станкам
            var result = new Dictionary<int, List<StageExecution>>();

            foreach (var stage in scheduledStages)
            {
                if (stage.MachineId.HasValue)
                {
                    var machineId = stage.MachineId.Value;

                    if (!result.ContainsKey(machineId))
                    {
                        result[machineId] = new List<StageExecution>();
                    }

                    result[machineId].Add(stage);
                }
            }

            return result;
        }

        // Получение прогнозируемого времени завершения для указанной партии
        public async Task<DateTime?> GetEstimatedCompletionTimeForBatchAsync(int batchId)
        {
            var batch = await _db.Batches
                .Include(b => b.SubBatches)
                    .ThenInclude(sb => sb.StageExecutions)
                        .ThenInclude(se => se.RouteStage)
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null) return null;

            DateTime latestEndTime = DateTime.UtcNow;

            // Проходим по всем подпартиям
            foreach (var subBatch in batch.SubBatches)
            {
                // Находим последний этап в маршруте
                var lastStage = subBatch.StageExecutions
                    .Where(se => !se.IsSetup)
                    .OrderByDescending(se => se.RouteStage.Order)
                    .FirstOrDefault();

                if (lastStage == null) continue;

                // Если этап завершен, используем фактическое время завершения
                if (lastStage.Status == StageExecutionStatus.Completed && lastStage.EndTimeUtc.HasValue)
                {
                    if (lastStage.EndTimeUtc.Value > latestEndTime)
                    {
                        latestEndTime = lastStage.EndTimeUtc.Value;
                    }
                }
                // Если этап в процессе, оцениваем время завершения
                else if (lastStage.Status == StageExecutionStatus.InProgress && lastStage.StartTimeUtc.HasValue)
                {
                    // Расчет оставшегося времени на основе нормы времени и количества деталей
                    var totalDuration = TimeSpan.FromHours(lastStage.RouteStage.NormTime * subBatch.Quantity);
                    var elapsedTime = DateTime.UtcNow - lastStage.StartTimeUtc.Value;
                    var remainingTime = totalDuration > elapsedTime ? totalDuration - elapsedTime : TimeSpan.FromMinutes(30);

                    var estimatedEndTime = DateTime.UtcNow.Add(remainingTime);
                    if (estimatedEndTime > latestEndTime)
                    {
                        latestEndTime = estimatedEndTime;
                    }
                }
                // Если этап ожидает или в очереди, оцениваем на основе предыдущих этапов и нормы времени
                else
                {
                    // Находим все незавершенные этапы
                    var pendingStages = subBatch.StageExecutions
                        .Where(se => !se.IsSetup && se.Status != StageExecutionStatus.Completed)
                        .OrderBy(se => se.RouteStage.Order)
                        .ToList();

                    // Суммируем время на выполнение
                    var remainingTime = TimeSpan.FromHours(
                        pendingStages.Sum(se => se.RouteStage.NormTime * subBatch.Quantity));

                    // Добавляем время на переналадки (примерная оценка)
                    remainingTime = remainingTime.Add(TimeSpan.FromHours(pendingStages.Count() * 0.5));

                    var estimatedEndTime = DateTime.UtcNow.Add(remainingTime);
                    if (estimatedEndTime > latestEndTime)
                    {
                        latestEndTime = estimatedEndTime;
                    }
                }
            }

            return latestEndTime;
        }

        // Проверка наличия возможных конфликтов в расписании
        public async Task<List<ScheduleConflict>> GetScheduleConflictsAsync()
        {
            var result = new List<ScheduleConflict>();

            // Получаем текущее и будущее расписание
            var schedule = await GetMachineScheduleAsync(DateTime.UtcNow, DateTime.UtcNow.AddDays(7));

            foreach (var machineSchedule in schedule)
            {
                var machineId = machineSchedule.Key;
                var machineStages = machineSchedule.Value
                    .OrderBy(s => s.StartTimeUtc)
                    .ThenBy(s => s.ScheduledStartTimeUtc)
                    .ToList();

                // Проверяем наличие пересечений во времени
                for (int i = 0; i < machineStages.Count - 1; i++)
                {
                    var currentStage = machineStages[i];
                    var nextStage = machineStages[i + 1];

                    // Определяем фактическое или ожидаемое время завершения текущего этапа
                    DateTime? currentEndTime = currentStage.EndTimeUtc;
                    if (!currentEndTime.HasValue && currentStage.StartTimeUtc.HasValue)
                    {
                        // Оцениваем время завершения по норме времени
                        var duration = TimeSpan.FromHours(currentStage.IsSetup ?
                            currentStage.RouteStage.SetupTime :
                            currentStage.RouteStage.NormTime * currentStage.SubBatch.Quantity);

                        currentEndTime = currentStage.StartTimeUtc.Value.Add(duration);
                    }

                    // Определяем фактическое или запланированное время начала следующего этапа
                    DateTime? nextStartTime = nextStage.StartTimeUtc ?? nextStage.ScheduledStartTimeUtc;

                    // Проверяем пересечение
                    if (currentEndTime.HasValue && nextStartTime.HasValue && currentEndTime.Value > nextStartTime.Value)
                    {
                        // Найден конфликт
                        result.Add(new ScheduleConflict
                        {
                            MachineId = machineId,
                            MachineName = machineStages[0].Machine?.Name ?? $"Станок #{machineId}",
                            ConflictingStages = new List<StageExecution> { currentStage, nextStage },
                            ConflictStartTime = nextStartTime.Value,
                            ConflictEndTime = currentEndTime.Value,
                            ConflictType = "Overlap"
                        });
                    }
                }

                // Проверяем наличие параллельных этапов в работе (двойное назначение)
                var inProgressStages = machineStages
                    .Where(s => s.Status == StageExecutionStatus.InProgress)
                    .ToList();

                if (inProgressStages.Count > 1)
                {
                    result.Add(new ScheduleConflict
                    {
                        MachineId = machineId,
                        MachineName = machineStages[0].Machine?.Name ?? $"Станок #{machineId}",
                        ConflictingStages = inProgressStages,
                        ConflictStartTime = DateTime.UtcNow,
                        ConflictEndTime = DateTime.UtcNow.AddHours(1), // Условно 1 час
                        ConflictType = "DoubleBooking"
                    });
                }
            }

            return result;
        }

    }
}