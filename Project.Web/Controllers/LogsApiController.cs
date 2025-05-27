using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Domain.Repositories;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Web.Controllers
{
    [Route("api/logs")]
    [ApiController]
    public class LogsApiController : ControllerBase
    {
        private readonly IEventRepository _eventRepo;
        private readonly EventLogService _eventLogService;

        public LogsApiController(
            IEventRepository eventRepo,
            EventLogService eventLogService)
        {
            _eventRepo = eventRepo;
            _eventLogService = eventLogService;
        }

        /// <summary>
        /// Получение событий этапов с фильтрацией
        /// </summary>
        [HttpGet("stage-events")]
        public async Task<IActionResult> GetStageEvents(
            string eventType = null,
            bool? isAutomatic = null,
            int hours = 24,
            int take = 100)
        {
            try
            {
                var endDate = DateTime.UtcNow;
                var startDate = endDate.AddHours(-hours);

                var events = await _eventRepo.GetStageEventsAsync(
                    startDate: startDate,
                    endDate: endDate,
                    eventType: eventType,
                    isAutomatic: isAutomatic,
                    take: take);

                var total = await _eventRepo.GetStageEventsCountAsync(
                    startDate: startDate,
                    endDate: endDate,
                    eventType: eventType,
                    isAutomatic: isAutomatic);

                var result = events.Select(e => new
                {
                    id = e.Id,
                    stageExecutionId = e.StageExecutionId,
                    eventType = e.EventType,
                    eventTypeName = GetEventTypeName(e.EventType),
                    previousStatus = e.PreviousStatus,
                    newStatus = e.NewStatus,
                    eventTime = e.EventTimeUtc,
                    operatorId = e.OperatorId,
                    operatorName = e.OperatorName ?? "Система",
                    deviceId = e.DeviceId,
                    comment = e.Comment,
                    isAutomatic = e.IsAutomatic,
                    durationInPreviousState = e.DurationInPreviousState?.ToString(@"hh\:mm\:ss"),
                    stageName = e.StageExecution?.RouteStage?.Name,
                    detailName = e.StageExecution?.SubBatch?.Batch?.Detail?.Name,
                    machineName = e.NewMachine?.Name ?? e.StageExecution?.Machine?.Name
                }).ToList();

                return Ok(new { events = result, total = total });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Получение деталей конкретного события
        /// </summary>
        [HttpGet("stage-events/{eventId}")]
        public async Task<IActionResult> GetStageEventDetail(int eventId)
        {
            try
            {
                var events = await _eventRepo.GetStageEventsAsync(take: 1);
                var eventDetail = events.FirstOrDefault(e => e.Id == eventId);

                if (eventDetail == null)
                {
                    return NotFound(new { error = "Событие не найдено" });
                }

                var result = new
                {
                    id = eventDetail.Id,
                    stageExecutionId = eventDetail.StageExecutionId,
                    eventType = eventDetail.EventType,
                    eventTypeName = GetEventTypeName(eventDetail.EventType),
                    previousStatus = eventDetail.PreviousStatus,
                    newStatus = eventDetail.NewStatus,
                    eventTime = eventDetail.EventTimeUtc,
                    operatorId = eventDetail.OperatorId,
                    operatorName = eventDetail.OperatorName ?? "Система",
                    deviceId = eventDetail.DeviceId,
                    comment = eventDetail.Comment,
                    isAutomatic = eventDetail.IsAutomatic,
                    durationInPreviousState = eventDetail.DurationInPreviousState?.ToString(@"hh\:mm\:ss"),
                    stageName = eventDetail.StageExecution?.RouteStage?.Name,
                    detailName = eventDetail.StageExecution?.SubBatch?.Batch?.Detail?.Name,
                    machineName = eventDetail.NewMachine?.Name ?? eventDetail.StageExecution?.Machine?.Name,
                    additionalData = eventDetail.AdditionalData,
                    ipAddress = eventDetail.IpAddress,
                    userAgent = eventDetail.UserAgent
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Очистка старых событий
        /// </summary>
        [HttpPost("cleanup")]
        public async Task<IActionResult> CleanupOldEvents([FromBody] CleanupRequest request)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-request.DaysOld);
                var deletedCount = await _eventRepo.CleanupOldEventsAsync(cutoffDate);

                return Ok(new { deletedCount = deletedCount });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Статистика событий
        /// </summary>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics(int hours = 24)
        {
            try
            {
                var endDate = DateTime.UtcNow;
                var startDate = endDate.AddHours(-hours);

                var stageStats = await _eventRepo.GetStageEventStatisticsAsync(startDate, endDate);
                var operatorActivity = await _eventRepo.GetOperatorActivityAsync(startDate, endDate);
                var totalEvents = await _eventRepo.GetStageEventsCountAsync(startDate, endDate);

                var result = new
                {
                    totalEvents = totalEvents,
                    stageEventStatistics = stageStats,
                    operatorActivity = operatorActivity.Select(o => new
                    {
                        operatorId = o.OperatorId,
                        operatorName = o.OperatorName,
                        eventCount = o.EventCount,
                        lastActivity = o.LastActivity
                    }).ToList(),
                    period = new
                    {
                        startDate = startDate,
                        endDate = endDate,
                        hours = hours
                    }
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Получение размера логов
        /// </summary>
        [HttpGet("size")]
        public async Task<IActionResult> GetLogSize()
        {
            try
            {
                var sizeInfo = await _eventRepo.GetEventLogSizeAsync();

                return Ok(new
                {
                    stageEvents = sizeInfo.StageEvents,
                    systemEvents = sizeInfo.SystemEvents,
                    totalSizeBytes = sizeInfo.TotalSizeBytes,
                    totalSizeMB = Math.Round(sizeInfo.TotalSizeBytes / (1024.0 * 1024.0), 2)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private string GetEventTypeName(string eventType)
        {
            return eventType switch
            {
                "Created" => "Создан",
                "Assigned" => "Назначен",
                "Started" => "Запущен",
                "Paused" => "Приостановлен",
                "Resumed" => "Возобновлен",
                "Completed" => "Завершен",
                "Cancelled" => "Отменен",
                "Reassigned" => "Переназначен",
                "Failed" => "Ошибка",
                _ => eventType
            };
        }
    }

    public class CleanupRequest
    {
        public int DaysOld { get; set; } = 7;
    }
}