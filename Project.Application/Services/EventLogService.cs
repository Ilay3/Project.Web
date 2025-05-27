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

        /// <summary>
        /// Логирование события этапа с проверкой NULL значений
        /// </summary>
        public async Task<int> LogStageEventAsync(
            int stageExecutionId,
            string eventType,
            string previousStatus = null,
            string newStatus = null,
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
                    EventType = string.IsNullOrEmpty(eventType) ? "Unknown" : eventType,
                    PreviousStatus = previousStatus ?? "",
                    NewStatus = newStatus ?? "",
                    EventTimeUtc = DateTime.UtcNow,
                    OperatorId = operatorId ?? "SYSTEM",
                    OperatorName = operatorName ?? "Система",
                    DeviceId = deviceId ?? "AUTO",
                    Comment = comment ?? "",
                    AdditionalData = additionalData != null ? JsonSerializer.Serialize(additionalData) : "{}",
                    IsAutomatic = isAutomatic,
                    PreviousMachineId = previousMachineId,
                    NewMachineId = newMachineId,
                    DurationInPreviousState = durationInPreviousState,
                    IpAddress = ipAddress ?? "",
                    UserAgent = userAgent ?? ""
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
                previousStatus: "",
                newStatus: StageExecutionStatus.Pending.ToString(),
                operatorId: operatorId,
                operatorName: operatorName,
                comment: "Этап создан",
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
                comment: comment ?? "Этап назначен на станок",
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
            TimeSpan? timeInPreviousState = null,
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
                comment: comment ?? "Этап запущен",
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
                comment: reason ?? "Этап приостановлен",
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
                comment: comment ?? "Этап возобновлен",
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
                comment: comment ?? "Этап завершен",
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
                previousStatus: "",
                newStatus: "",
                operatorId: operatorId,
                operatorName: operatorName,
                comment: reason ?? "Этап переназначен",
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
                previousStatus: "",
                newStatus: StageExecutionStatus.Error.ToString(),
                operatorId: operatorId,
                operatorName: operatorName,
                deviceId: deviceId,
                comment: reason ?? "Этап отменен",
                isAutomatic: isAutomatic);
        }

        /// <summary>
        /// Получить события этапа с фильтрацией
        /// </summary>
        public async Task<List<StageEventDto>> GetStageEventsAsync(int stageExecutionId)
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

        // Вспомогательные методы
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
                _ => eventType
            };
        }
    }
}