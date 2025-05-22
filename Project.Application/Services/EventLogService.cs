using Microsoft.Extensions.Logging;
using Project.Contracts.ModelDTO;
using Project.Domain.Entities;
using Project.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class EventLogService
    {
        private readonly IEventRepository _eventRepo;
        private readonly ILogger<EventLogService> _logger;

        public EventLogService(
            IEventRepository eventRepo,
            ILogger<EventLogService> logger)
        {
            _eventRepo = eventRepo;
            _logger = logger;
        }

        // === Методы для событий этапов ===

        /// <summary>
        /// Логирование события этапа
        /// </summary>
        public async Task<int> LogStageEventAsync(
            int stageExecutionId,
            string eventType,
            string previousStatus,
            string newStatus,
            string operatorId = null,
            string operatorName = null,
            string deviceId = null,
            string comment = null,
            object additionalData = null,
            bool isAutomatic = false,
            int? previousMachineId = null,
            int? newMachineId = null,
            TimeSpan? durationInPreviousState = null,
            string ipAddress = null,
            string userAgent = null)
        {
            try
            {
                var stageEvent = new StageEvent
                {
                    StageExecutionId = stageExecutionId,
                    EventType = eventType,
                    PreviousStatus = previousStatus,
                    NewStatus = newStatus,
                    EventTimeUtc = DateTime.UtcNow,
                    OperatorId = operatorId,
                    OperatorName = operatorName,
                    DeviceId = deviceId,
                    Comment = comment,
                    AdditionalData = additionalData != null ? JsonSerializer.Serialize(additionalData) : null,
                    IsAutomatic = isAutomatic,
                    PreviousMachineId = previousMachineId,
                    NewMachineId = newMachineId,
                    DurationInPreviousState = durationInPreviousState,
                    IpAddress = ipAddress,
                    UserAgent = userAgent
                };

                return await _eventRepo.AddStageEventAsync(stageEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при записи события этапа {StageId}, тип {EventType}",
                    stageExecutionId, eventType);
                throw;
            }
        }

        /// <summary>
        /// Логирование создания этапа
        /// </summary>
        public async Task LogStageCreatedAsync(
            int stageExecutionId,
            string operatorId = null,
            string operatorName = null,
            object additionalData = null,
            bool isAutomatic = true)
        {
            await LogStageEventAsync(
                stageExecutionId: stageExecutionId,
                eventType: StageEventTypes.Created,
                previousStatus: null,
                newStatus: StageExecutionStatus.Pending.ToString(),
                operatorId: operatorId,
                operatorName: operatorName,
                additionalData: additionalData,
                isAutomatic: isAutomatic);
        }

        /// <summary>
        /// Логирование назначения этапа на станок
        /// </summary>
        public async Task LogStageAssignedAsync(
            int stageExecutionId,
            int machineId,
            string operatorId = null,
            string operatorName = null,
            string comment = null,
            bool isAutomatic = true)
        {
            await LogStageEventAsync(
                stageExecutionId: stageExecutionId,
                eventType: StageEventTypes.Assigned,
                previousStatus: StageExecutionStatus.Pending.ToString(),
                newStatus: StageExecutionStatus.Pending.ToString(),
                operatorId: operatorId,
                operatorName: operatorName,
                comment: comment,
                newMachineId: machineId,
                isAutomatic: isAutomatic);
        }

        /// <summary>
        /// Логирование запуска этапа
        /// </summary>
        public async Task LogStageStartedAsync(
            int stageExecutionId,
            string operatorId = null,
            string operatorName = null,
            string deviceId = null,
            string comment = null,
            TimeSpan? timeInPending = null,
            bool isAutomatic = false)
        {
            await LogStageEventAsync(
                stageExecutionId: stageExecutionId,
                eventType: StageEventTypes.Started,
                previousStatus: StageExecutionStatus.Pending.ToString(),
                newStatus: StageExecutionStatus.InProgress.ToString(),
                operatorId: operatorId,
                operatorName: operatorName,
                deviceId: deviceId,
                comment: comment,
                durationInPreviousState: timeInPending,
                isAutomatic: isAutomatic);
        }

        /// <summary>
        /// Логирование приостановки этапа
        /// </summary>
        public async Task LogStagePausedAsync(
            int stageExecutionId,
            string reason,
            string operatorId = null,
            string operatorName = null,
            string deviceId = null,
            TimeSpan? timeInProgress = null,
            bool isAutomatic = false)
        {
            await LogStageEventAsync(
                stageExecutionId: stageExecutionId,
                eventType: StageEventTypes.Paused,
                previousStatus: StageExecutionStatus.InProgress.ToString(),
                newStatus: StageExecutionStatus.Paused.ToString(),
                operatorId: operatorId,
                operatorName: operatorName,
                deviceId: deviceId,
                comment: reason,
                durationInPreviousState: timeInProgress,
                isAutomatic: isAutomatic);
        }

        /// <summary>
        /// Логирование возобновления этапа
        /// </summary>
        public async Task LogStageResumedAsync(
            int stageExecutionId,
            string operatorId = null,
            string operatorName = null,
            string deviceId = null,
            string comment = null,
            TimeSpan? timeInPause = null,
            bool isAutomatic = false)
        {
            await LogStageEventAsync(
                stageExecutionId: stageExecutionId,
                eventType: StageEventTypes.Resumed,
                previousStatus: StageExecutionStatus.Paused.ToString(),
                newStatus: StageExecutionStatus.InProgress.ToString(),
                operatorId: operatorId,
                operatorName: operatorName,
                deviceId: deviceId,
                comment: comment,
                durationInPreviousState: timeInPause,
                isAutomatic: isAutomatic);
        }

        /// <summary>
        /// Логирование завершения этапа
        /// </summary>
        public async Task LogStageCompletedAsync(
            int stageExecutionId,
            string operatorId = null,
            string operatorName = null,
            string deviceId = null,
            string comment = null,
            TimeSpan? timeInProgress = null,
            bool isAutomatic = false)
        {
            await LogStageEventAsync(
                stageExecutionId: stageExecutionId,
                eventType: StageEventTypes.Completed,
                previousStatus: StageExecutionStatus.InProgress.ToString(),
                newStatus: StageExecutionStatus.Completed.ToString(),
                operatorId: operatorId,
                operatorName: operatorName,
                deviceId: deviceId,
                comment: comment,
                durationInPreviousState: timeInProgress,
                isAutomatic: isAutomatic);
        }

        /// <summary>
        /// Логирование переназначения этапа
        /// </summary>
        public async Task LogStageReassignedAsync(
            int stageExecutionId,
            int fromMachineId,
            int toMachineId,
            string reason,
            string operatorId = null,
            string operatorName = null,
            bool isAutomatic = false)
        {
            await LogStageEventAsync(
                stageExecutionId: stageExecutionId,
                eventType: StageEventTypes.Reassigned,
                previousStatus: null, // статус может не измениться
                newStatus: null,
                operatorId: operatorId,
                operatorName: operatorName,
                comment: reason,
                previousMachineId: fromMachineId,
                newMachineId: toMachineId,
                isAutomatic: isAutomatic);
        }

        /// <summary>
        /// Логирование отмены этапа
        /// </summary>
        public async Task LogStageCancelledAsync(
            int stageExecutionId,
            string reason,
            string operatorId = null,
            string operatorName = null,
            string deviceId = null,
            bool isAutomatic = false)
        {
            await LogStageEventAsync(
                stageExecutionId: stageExecutionId,
                eventType: StageEventTypes.Cancelled,
                previousStatus: null, // может быть разным
                newStatus: StageExecutionStatus.Error.ToString(),
                operatorId: operatorId,
                operatorName: operatorName,
                deviceId: deviceId,
                comment: reason,
                isAutomatic: isAutomatic);
        }

        // === Методы для системных событий ===

        /// <summary>
        /// Логирование системного события
        /// </summary>
        public async Task<int> LogSystemEventAsync(
            string category,
            string eventType,
            string severity,
            string title,
            string description = null,
            string source = null,
            string userId = null,
            string userName = null,
            string ipAddress = null,
            string relatedEntityId = null,
            string relatedEntityType = null,
            object additionalData = null,
            string stackTrace = null,
            string innerException = null)
        {
            try
            {
                var systemEvent = new SystemEvent
                {
                    Category = category,
                    EventType = eventType,
                    Severity = severity,
                    Title = title,
                    Description = description,
                    EventTimeUtc = DateTime.UtcNow,
                    Source = source,
                    UserId = userId,
                    UserName = userName,
                    IpAddress = ipAddress,
                    RelatedEntityId = relatedEntityId,
                    RelatedEntityType = relatedEntityType,
                    AdditionalData = additionalData != null ? JsonSerializer.Serialize(additionalData) : null,
                    StackTrace = stackTrace,
                    InnerException = innerException,
                    IsProcessed = false,
                    ProcessingAttempts = 0
                };

                return await _eventRepo.AddSystemEventAsync(systemEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при записи системного события {EventType} категории {Category}",
                    eventType, category);
                throw;
            }
        }

        /// <summary>
        /// Логирование ошибки
        /// </summary>
        public async Task LogErrorAsync(
            string title,
            Exception exception,
            string source = null,
            string userId = null,
            string userName = null,
            string relatedEntityId = null,
            string relatedEntityType = null,
            object additionalData = null)
        {
            await LogSystemEventAsync(
                category: SystemEventCategories.System,
                eventType: "Error",
                severity: EventSeverity.Error,
                title: title,
                description: exception.Message,
                source: source,
                userId: userId,
                userName: userName,
                relatedEntityId: relatedEntityId,
                relatedEntityType: relatedEntityType,
                additionalData: additionalData,
                stackTrace: exception.StackTrace,
                innerException: exception.InnerException?.Message);
        }

        /// <summary>
        /// Логирование критической ошибки
        /// </summary>
        public async Task LogCriticalErrorAsync(
            string title,
            Exception exception,
            string source = null,
            object additionalData = null)
        {
            await LogSystemEventAsync(
                category: SystemEventCategories.System,
                eventType: "CriticalError",
                severity: EventSeverity.Critical,
                title: title,
                description: exception.Message,
                source: source,
                additionalData: additionalData,
                stackTrace: exception.StackTrace,
                innerException: exception.InnerException?.Message);
        }

        /// <summary>
        /// Логирование действия пользователя
        /// </summary>
        public async Task LogUserActionAsync(
            string action,
            string userId,
            string userName,
            string ipAddress = null,
            string relatedEntityId = null,
            string relatedEntityType = null,
            object additionalData = null)
        {
            await LogSystemEventAsync(
                category: SystemEventCategories.UserActivity,
                eventType: action,
                severity: EventSeverity.Info,
                title: $"Пользователь {userName} выполнил действие: {action}",
                source: "WebUI",
                userId: userId,
                userName: userName,
                ipAddress: ipAddress,
                relatedEntityId: relatedEntityId,
                relatedEntityType: relatedEntityType,
                additionalData: additionalData);
        }

        /// <summary>
        /// Логирование старта/остановки сервиса
        /// </summary>
        public async Task LogServiceEventAsync(
            string serviceName,
            string eventType, // Started, Stopped, Error
            string description = null,
            object additionalData = null)
        {
            var severity = eventType == "Error" ? EventSeverity.Error : EventSeverity.Info;

            await LogSystemEventAsync(
                category: SystemEventCategories.System,
                eventType: $"Service{eventType}",
                severity: severity,
                title: $"Сервис {serviceName}: {eventType}",
                description: description,
                source: "BackgroundService",
                additionalData: additionalData);
        }

        // === Методы получения событий ===

        /// <summary>
        /// Получить события этапа с фильтрацией
        /// </summary>
        public async Task<List<StageEventDto>> GetStageEventsAsync(
            int stageExecutionId)
        {
            var events = await _eventRepo.GetStageEventsAsync(stageExecutionId);

            return events.ConvertAll(e => new StageEventDto
            {
                Id = e.Id,
                StageExecutionId = e.StageExecutionId,
                EventType = e.EventType,
                EventTypeName = GetEventTypeName(e.EventType),
                PreviousStatus = e.PreviousStatus,
                NewStatus = e.NewStatus,
                EventTime = e.EventTimeUtc,
                OperatorId = e.OperatorId,
                OperatorName = e.OperatorName ?? "Система",
                DeviceId = e.DeviceId,
                Comment = e.Comment,
                IsAutomatic = e.IsAutomatic,
                PreviousMachineId = e.PreviousMachineId,
                PreviousMachineName = e.PreviousMachine?.Name,
                NewMachineId = e.NewMachineId,
                NewMachineName = e.NewMachine?.Name,
                DurationInPreviousState = e.DurationInPreviousState,
                StageName = e.StageExecution?.RouteStage?.Name,
                DetailName = e.StageExecution?.SubBatch?.Batch?.Detail?.Name
            });
        }

        /// <summary>
        /// Получить статистику событий
        /// </summary>
        public async Task<EventStatisticsDto> GetEventStatisticsAsync(
            DateTime startDate,
            DateTime endDate,
            int? machineId = null)
        {
            var stageStats = await _eventRepo.GetStageEventStatisticsAsync(startDate, endDate, machineId);
            var systemStats = await _eventRepo.GetSystemEventStatisticsAsync(startDate, endDate);
            var operatorActivity = await _eventRepo.GetOperatorActivityAsync(startDate, endDate);
            var avgTimeInStatus = await _eventRepo.GetAverageTimeInStatusAsync(startDate, endDate, machineId);

            return new EventStatisticsDto
            {
                StartDate = startDate,
                EndDate = endDate,
                MachineId = machineId,
                StageEventStatistics = stageStats,
                SystemEventStatistics = systemStats,
                OperatorActivity = operatorActivity.ConvertAll(o => new OperatorActivityDto
                {
                    OperatorId = o.OperatorId,
                    OperatorName = o.OperatorName,
                    EventCount = o.EventCount,
                    LastActivity = o.LastActivity
                }),
                AverageTimeInStatus = avgTimeInStatus.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.TotalHours)
            };
        }

        // === Вспомогательные методы ===

        private string GetEventTypeName(string eventType)
        {
            return eventType switch
            {
                StageEventTypes.Created => "Создан",
                StageEventTypes.Assigned => "Назначен",
                StageEventTypes.Started => "Запущен",
                StageEventTypes.Paused => "Приостановлен",
                StageEventTypes.Resumed => "Возобновлен",
                StageEventTypes.Completed => "Завершен",
                StageEventTypes.Cancelled => "Отменен",
                StageEventTypes.Reassigned => "Переназначен",
                StageEventTypes.Failed => "Ошибка",
                StageEventTypes.Prioritized => "Приоритизирован",
                StageEventTypes.QueuePositionChanged => "Изменена позиция в очереди",
                StageEventTypes.SetupRequired => "Требуется переналадка",
                StageEventTypes.SetupCompleted => "Переналадка завершена",
                StageEventTypes.CommentAdded => "Добавлен комментарий",
                _ => eventType
            };
        }
    }
}