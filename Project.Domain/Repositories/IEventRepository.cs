using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Domain.Repositories
{
    public interface IEventRepository
    {
        // === Операции с событиями этапов ===

        /// <summary>
        /// Добавить событие этапа
        /// </summary>
        Task<int> AddStageEventAsync(StageEvent stageEvent);

        /// <summary>
        /// Получить события этапа
        /// </summary>
        Task<List<StageEvent>> GetStageEventsAsync(int stageExecutionId);

        /// <summary>
        /// Получить события этапов по фильтру
        /// </summary>
        Task<List<StageEvent>> GetStageEventsAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? stageExecutionId = null,
            int? machineId = null,
            int? batchId = null,
            string eventType = null,
            string operatorId = null,
            bool? isAutomatic = null,
            int skip = 0,
            int take = 100);

        /// <summary>
        /// Получить количество событий по фильтру
        /// </summary>
        Task<int> GetStageEventsCountAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? stageExecutionId = null,
            int? machineId = null,
            int? batchId = null,
            string eventType = null,
            string operatorId = null,
            bool? isAutomatic = null);

        /// <summary>
        /// Получить последнее событие этапа
        /// </summary>
        Task<StageEvent> GetLatestStageEventAsync(int stageExecutionId);

        /// <summary>
        /// Получить статистику событий этапов
        /// </summary>
        Task<Dictionary<string, int>> GetStageEventStatisticsAsync(
            DateTime startDate,
            DateTime endDate,
            int? machineId = null);

        // === Операции с системными событиями ===

        /// <summary>
        /// Добавить системное событие
        /// </summary>
        Task<int> AddSystemEventAsync(SystemEvent systemEvent);

        /// <summary>
        /// Получить системные события по фильтру
        /// </summary>
        Task<List<SystemEvent>> GetSystemEventsAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string category = null,
            string eventType = null,
            string severity = null,
            string source = null,
            string userId = null,
            bool? isProcessed = null,
            int skip = 0,
            int take = 100);

        /// <summary>
        /// Получить количество системных событий по фильтру
        /// </summary>
        Task<int> GetSystemEventsCountAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string category = null,
            string eventType = null,
            string severity = null,
            string source = null,
            string userId = null,
            bool? isProcessed = null);

        /// <summary>
        /// Отметить системное событие как обработанное
        /// </summary>
        Task MarkSystemEventAsProcessedAsync(int eventId, string processedBy);

        /// <summary>
        /// Получить необработанные критические события
        /// </summary>
        Task<List<SystemEvent>> GetUnprocessedCriticalEventsAsync();

        /// <summary>
        /// Получить статистику системных событий
        /// </summary>
        Task<Dictionary<string, Dictionary<string, int>>> GetSystemEventStatisticsAsync(
            DateTime startDate,
            DateTime endDate);

        // === Операции очистки ===

        /// <summary>
        /// Удалить старые события (для очистки БД)
        /// </summary>
        Task<int> CleanupOldEventsAsync(DateTime olderThan);

        /// <summary>
        /// Получить размер лога событий
        /// </summary>
        Task<(int StageEvents, int SystemEvents, long TotalSizeBytes)> GetEventLogSizeAsync();

        // === Аналитические запросы ===

        /// <summary>
        /// Получить временную линию событий для этапа
        /// </summary>
        Task<List<StageEvent>> GetStageTimelineAsync(int stageExecutionId);

        /// <summary>
        /// Получить активность операторов
        /// </summary>
        Task<List<(string OperatorId, string OperatorName, int EventCount, DateTime LastActivity)>>
            GetOperatorActivityAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Получить статистику переназначений этапов
        /// </summary>
        Task<List<(int FromMachineId, string FromMachineName, int ToMachineId, string ToMachineName, int Count)>>
            GetReassignmentStatisticsAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Получить среднее время в каждом статусе
        /// </summary>
        Task<Dictionary<string, TimeSpan>> GetAverageTimeInStatusAsync(
            DateTime startDate,
            DateTime endDate,
            int? machineId = null);
    }
}