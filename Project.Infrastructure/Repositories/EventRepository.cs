using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Repositories;
using Project.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Infrastructure.Repositories
{
    public class EventRepository : IEventRepository
    {
        private readonly ManufacturingDbContext _context;

        public EventRepository(ManufacturingDbContext context)
        {
            _context = context;
        }

        // === Операции с событиями этапов ===

        public async Task<int> AddStageEventAsync(StageEvent stageEvent)
        {
            _context.StageEvents.Add(stageEvent);
            await _context.SaveChangesAsync();
            return stageEvent.Id;
        }

        public async Task<List<StageEvent>> GetStageEventsAsync(int stageExecutionId)
        {
            return await _context.StageEvents
                .Include(se => se.StageExecution)
                    .ThenInclude(se => se.SubBatch)
                        .ThenInclude(sb => sb.Batch)
                            .ThenInclude(b => b.Detail)
                .Include(se => se.StageExecution)
                    .ThenInclude(se => se.RouteStage)
                .Include(se => se.PreviousMachine)
                .Include(se => se.NewMachine)
                .Where(se => se.StageExecutionId == stageExecutionId)
                .OrderBy(se => se.EventTimeUtc)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<StageEvent>> GetStageEventsAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? stageExecutionId = null,
            int? machineId = null,
            int? batchId = null,
            string eventType = null,
            string operatorId = null,
            bool? isAutomatic = null,
            int skip = 0,
            int take = 100)
        {
            var query = _context.StageEvents
                .Include(se => se.StageExecution)
                    .ThenInclude(se => se.SubBatch)
                        .ThenInclude(sb => sb.Batch)
                            .ThenInclude(b => b.Detail)
                .Include(se => se.StageExecution)
                    .ThenInclude(se => se.RouteStage)
                .Include(se => se.StageExecution)
                    .ThenInclude(se => se.Machine)
                .Include(se => se.PreviousMachine)
                .Include(se => se.NewMachine)
                .AsQueryable();

            if (startDate.HasValue)
                query = query.Where(se => se.EventTimeUtc >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(se => se.EventTimeUtc <= endDate.Value);

            if (stageExecutionId.HasValue)
                query = query.Where(se => se.StageExecutionId == stageExecutionId.Value);

            if (machineId.HasValue)
                query = query.Where(se => se.StageExecution.MachineId == machineId.Value ||
                                         se.PreviousMachineId == machineId.Value ||
                                         se.NewMachineId == machineId.Value);

            if (batchId.HasValue)
                query = query.Where(se => se.StageExecution.SubBatch.BatchId == batchId.Value);

            if (!string.IsNullOrEmpty(eventType))
                query = query.Where(se => se.EventType == eventType);

            if (!string.IsNullOrEmpty(operatorId))
                query = query.Where(se => se.OperatorId == operatorId);

            if (isAutomatic.HasValue)
                query = query.Where(se => se.IsAutomatic == isAutomatic.Value);

            return await query
                .OrderByDescending(se => se.EventTimeUtc)
                .Skip(skip)
                .Take(take)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> GetStageEventsCountAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? stageExecutionId = null,
            int? machineId = null,
            int? batchId = null,
            string eventType = null,
            string operatorId = null,
            bool? isAutomatic = null)
        {
            var query = _context.StageEvents.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(se => se.EventTimeUtc >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(se => se.EventTimeUtc <= endDate.Value);

            if (stageExecutionId.HasValue)
                query = query.Where(se => se.StageExecutionId == stageExecutionId.Value);

            if (machineId.HasValue)
                query = query.Where(se => se.StageExecution.MachineId == machineId.Value ||
                                         se.PreviousMachineId == machineId.Value ||
                                         se.NewMachineId == machineId.Value);

            if (batchId.HasValue)
                query = query.Where(se => se.StageExecution.SubBatch.BatchId == batchId.Value);

            if (!string.IsNullOrEmpty(eventType))
                query = query.Where(se => se.EventType == eventType);

            if (!string.IsNullOrEmpty(operatorId))
                query = query.Where(se => se.OperatorId == operatorId);

            if (isAutomatic.HasValue)
                query = query.Where(se => se.IsAutomatic == isAutomatic.Value);

            return await query.CountAsync();
        }

        public async Task<StageEvent> GetLatestStageEventAsync(int stageExecutionId)
        {
            return await _context.StageEvents
                .Include(se => se.StageExecution)
                .Include(se => se.PreviousMachine)
                .Include(se => se.NewMachine)
                .Where(se => se.StageExecutionId == stageExecutionId)
                .OrderByDescending(se => se.EventTimeUtc)
                .FirstOrDefaultAsync();
        }

        public async Task<Dictionary<string, int>> GetStageEventStatisticsAsync(
            DateTime startDate,
            DateTime endDate,
            int? machineId = null)
        {
            var query = _context.StageEvents
                .Where(se => se.EventTimeUtc >= startDate && se.EventTimeUtc <= endDate);

            if (machineId.HasValue)
                query = query.Where(se => se.StageExecution.MachineId == machineId.Value);

            return await query
                .GroupBy(se => se.EventType)
                .ToDictionaryAsync(g => g.Key, g => g.Count());
        }

        // === Операции с системными событиями ===

        public async Task<int> AddSystemEventAsync(SystemEvent systemEvent)
        {
            _context.SystemEvents.Add(systemEvent);
            await _context.SaveChangesAsync();
            return systemEvent.Id;
        }

        public async Task<List<SystemEvent>> GetSystemEventsAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string category = null,
            string eventType = null,
            string severity = null,
            string source = null,
            string userId = null,
            bool? isProcessed = null,
            int skip = 0,
            int take = 100)
        {
            var query = _context.SystemEvents.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(se => se.EventTimeUtc >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(se => se.EventTimeUtc <= endDate.Value);

            if (!string.IsNullOrEmpty(category))
                query = query.Where(se => se.Category == category);

            if (!string.IsNullOrEmpty(eventType))
                query = query.Where(se => se.EventType == eventType);

            if (!string.IsNullOrEmpty(severity))
                query = query.Where(se => se.Severity == severity);

            if (!string.IsNullOrEmpty(source))
                query = query.Where(se => se.Source == source);

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(se => se.UserId == userId);

            if (isProcessed.HasValue)
                query = query.Where(se => se.IsProcessed == isProcessed.Value);

            return await query
                .OrderByDescending(se => se.EventTimeUtc)
                .Skip(skip)
                .Take(take)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> GetSystemEventsCountAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string category = null,
            string eventType = null,
            string severity = null,
            string source = null,
            string userId = null,
            bool? isProcessed = null)
        {
            var query = _context.SystemEvents.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(se => se.EventTimeUtc >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(se => se.EventTimeUtc <= endDate.Value);

            if (!string.IsNullOrEmpty(category))
                query = query.Where(se => se.Category == category);

            if (!string.IsNullOrEmpty(eventType))
                query = query.Where(se => se.EventType == eventType);

            if (!string.IsNullOrEmpty(severity))
                query = query.Where(se => se.Severity == severity);

            if (!string.IsNullOrEmpty(source))
                query = query.Where(se => se.Source == source);

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(se => se.UserId == userId);

            if (isProcessed.HasValue)
                query = query.Where(se => se.IsProcessed == isProcessed.Value);

            return await query.CountAsync();
        }

        public async Task MarkSystemEventAsProcessedAsync(int eventId, string processedBy)
        {
            var systemEvent = await _context.SystemEvents.FindAsync(eventId);
            if (systemEvent != null)
            {
                systemEvent.IsProcessed = true;
                systemEvent.ProcessedTimeUtc = DateTime.UtcNow;
                systemEvent.ProcessedBy = processedBy;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<SystemEvent>> GetUnprocessedCriticalEventsAsync()
        {
            return await _context.SystemEvents
                .Where(se => !se.IsProcessed && se.Severity == EventSeverity.Critical)
                .OrderBy(se => se.EventTimeUtc)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Dictionary<string, Dictionary<string, int>>> GetSystemEventStatisticsAsync(
            DateTime startDate,
            DateTime endDate)
        {
            var events = await _context.SystemEvents
                .Where(se => se.EventTimeUtc >= startDate && se.EventTimeUtc <= endDate)
                .GroupBy(se => se.Category)
                .Select(g => new
                {
                    Category = g.Key,
                    Severities = g.GroupBy(se => se.Severity)
                        .ToDictionary(sg => sg.Key, sg => sg.Count())
                })
                .AsNoTracking()
                .ToListAsync();

            return events.ToDictionary(e => e.Category, e => e.Severities);
        }

        // === Операции очистки ===

        public async Task<int> CleanupOldEventsAsync(DateTime olderThan)
        {
            var oldStageEvents = await _context.StageEvents
                .Where(se => se.EventTimeUtc < olderThan)
                .CountAsync();

            var oldSystemEvents = await _context.SystemEvents
                .Where(se => se.EventTimeUtc < olderThan && se.IsProcessed)
                .CountAsync();

            // Удаляем старые события
            _context.StageEvents.RemoveRange(
                _context.StageEvents.Where(se => se.EventTimeUtc < olderThan));

            _context.SystemEvents.RemoveRange(
                _context.SystemEvents.Where(se => se.EventTimeUtc < olderThan && se.IsProcessed));

            await _context.SaveChangesAsync();

            return oldStageEvents + oldSystemEvents;
        }

        public async Task<(int StageEvents, int SystemEvents, long TotalSizeBytes)> GetEventLogSizeAsync()
        {
            var stageEventCount = await _context.StageEvents.CountAsync();
            var systemEventCount = await _context.SystemEvents.CountAsync();

            // Примерная оценка размера (можно улучшить через SQL запросы)
            var estimatedSize = (stageEventCount * 500L) + (systemEventCount * 300L);

            return (stageEventCount, systemEventCount, estimatedSize);
        }

        // === Аналитические запросы ===

        public async Task<List<StageEvent>> GetStageTimelineAsync(int stageExecutionId)
        {
            return await _context.StageEvents
                .Include(se => se.PreviousMachine)
                .Include(se => se.NewMachine)
                .Where(se => se.StageExecutionId == stageExecutionId)
                .OrderBy(se => se.EventTimeUtc)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<(string OperatorId, string OperatorName, int EventCount, DateTime LastActivity)>>
            GetOperatorActivityAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.StageEvents
                .Where(se => se.EventTimeUtc >= startDate &&
                           se.EventTimeUtc <= endDate &&
                           !string.IsNullOrEmpty(se.OperatorId))
                .GroupBy(se => new { se.OperatorId, se.OperatorName })
                .Select(g => new
                {
                    g.Key.OperatorId,
                    g.Key.OperatorName,
                    EventCount = g.Count(),
                    LastActivity = g.Max(se => se.EventTimeUtc)
                })
                .AsNoTracking()
                .ToListAsync()
                .ContinueWith(task => task.Result.Select(r =>
                    (r.OperatorId, r.OperatorName ?? "Неизвестно", r.EventCount, r.LastActivity)).ToList());
        }

        public async Task<List<(int FromMachineId, string FromMachineName, int ToMachineId, string ToMachineName, int Count)>>
            GetReassignmentStatisticsAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.StageEvents
                .Include(se => se.PreviousMachine)
                .Include(se => se.NewMachine)
                .Where(se => se.EventTimeUtc >= startDate &&
                           se.EventTimeUtc <= endDate &&
                           se.EventType == StageEventTypes.Reassigned &&
                           se.PreviousMachineId.HasValue &&
                           se.NewMachineId.HasValue)
                .GroupBy(se => new
                {
                    se.PreviousMachineId,
                    se.NewMachineId,
                    FromMachineName = se.PreviousMachine.Name,
                    ToMachineName = se.NewMachine.Name
                })
                .Select(g => new
                {
                    FromMachineId = g.Key.PreviousMachineId.Value,
                    g.Key.FromMachineName,
                    ToMachineId = g.Key.NewMachineId.Value,
                    g.Key.ToMachineName,
                    Count = g.Count()
                })
                .AsNoTracking()
                .ToListAsync()
                .ContinueWith(task => task.Result.Select(r =>
                    (r.FromMachineId, r.FromMachineName ?? "Неизвестно", r.ToMachineId, r.ToMachineName ?? "Неизвестно", r.Count)).ToList());
        }

        public async Task<Dictionary<string, TimeSpan>> GetAverageTimeInStatusAsync(
            DateTime startDate,
            DateTime endDate,
            int? machineId = null)
        {
            var events = await _context.StageEvents
                .Where(se => se.EventTimeUtc >= startDate &&
                           se.EventTimeUtc <= endDate &&
                           se.DurationInPreviousState.HasValue)
                .Where(se => !machineId.HasValue || se.StageExecution.MachineId == machineId.Value)
                .GroupBy(se => se.PreviousStatus)
                .Select(g => new
                {
                    Status = g.Key,
                    AverageDuration = g.Average(se => se.DurationInPreviousState.Value.TotalMilliseconds)
                })
                .AsNoTracking()
                .ToListAsync();

            return events
                .Where(e => !string.IsNullOrEmpty(e.Status))
                .ToDictionary(
                    e => e.Status,
                    e => TimeSpan.FromMilliseconds(e.AverageDuration));
        }
    }
}