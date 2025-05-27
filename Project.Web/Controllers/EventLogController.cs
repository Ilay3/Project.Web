using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
using Project.Domain.Entities;
using Project.Domain.Repositories;
using Project.Web.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Web.Controllers
{
    public class EventLogController : Controller
    {
        private readonly EventLogService _eventLogService;
        private readonly IEventRepository _eventRepository;
        private readonly MachineService _machineService;
        private readonly DetailService _detailService;

        public EventLogController(
            EventLogService eventLogService,
            IEventRepository eventRepository,
            MachineService machineService,
            DetailService detailService)
        {
            _eventLogService = eventLogService;
            _eventRepository = eventRepository;
            _machineService = machineService;
            _detailService = detailService;
        }

        /// <summary>
        /// Главная страница истории событий
        /// </summary>
        public async Task<IActionResult> Index(EventFilterDto filter = null)
        {
            filter ??= new EventFilterDto
            {
                StartDate = DateTime.Today.AddDays(-7),
                EndDate = DateTime.Today.AddDays(1),
                Page = 1,
                PageSize = 50
            };

            // Получаем данные для фильтров
            var machines = await _machineService.GetAllAsync();
            var details = await _detailService.GetAllAsync();

            ViewBag.Machines = machines;
            ViewBag.Details = details;
            ViewBag.EventTypes = GetEventTypes();
            ViewBag.Categories = GetCategories();
            ViewBag.Severities = GetSeverities();

            // Получаем события этапов
            var stageEvents = await _eventRepository.GetStageEventsAsync(
                startDate: filter.StartDate,
                endDate: filter.EndDate,
                machineId: filter.MachineId,
                batchId: filter.BatchId,
                eventType: filter.EventType,
                operatorId: filter.OperatorId,
                isAutomatic: filter.IsAutomatic,
                skip: (filter.Page - 1) * filter.PageSize,
                take: filter.PageSize);

            var stageEventsCount = await _eventRepository.GetStageEventsCountAsync(
                startDate: filter.StartDate,
                endDate: filter.EndDate,
                machineId: filter.MachineId,
                batchId: filter.BatchId,
                eventType: filter.EventType,
                operatorId: filter.OperatorId,
                isAutomatic: filter.IsAutomatic);

            // Получаем системные события
            var systemEvents = await _eventRepository.GetSystemEventsAsync(
                startDate: filter.StartDate,
                endDate: filter.EndDate,
                category: filter.Category,
                eventType: filter.EventType,
                severity: filter.Severity,
                userId: filter.UserId,
                isProcessed: filter.IsProcessed,
                skip: (filter.Page - 1) * filter.PageSize,
                take: filter.PageSize);

            var systemEventsCount = await _eventRepository.GetSystemEventsCountAsync(
                startDate: filter.StartDate,
                endDate: filter.EndDate,
                category: filter.Category,
                eventType: filter.EventType,
                severity: filter.Severity,
                userId: filter.UserId,
                isProcessed: filter.IsProcessed);

            var viewModel = new EventLogViewModel
            {
                Filter = filter,
                StageEvents = new PagedEventsDto<StageEventDto>
                {
                    Events = stageEvents.Select(ConvertToStageEventDto).ToList(),
                    TotalCount = stageEventsCount,
                    Page = filter.Page,
                    PageSize = filter.PageSize
                },
                SystemEvents = new PagedEventsDto<SystemEventDto>
                {
                    Events = systemEvents.Select(ConvertToSystemEventDto).ToList(),
                    TotalCount = systemEventsCount,
                    Page = filter.Page,
                    PageSize = filter.PageSize
                }
            };

            return View(viewModel);
        }

        /// <summary>
        /// Детали временной линии этапа
        /// </summary>
        public async Task<IActionResult> StageTimeline(int stageId)
        {
            var events = await _eventLogService.GetStageEventsAsync(stageId);

            if (!events.Any())
            {
                TempData["ErrorMessage"] = "События для данного этапа не найдены";
                return RedirectToAction(nameof(Index));
            }

            var timeline = new StageTimelineDto
            {
                StageExecutionId = stageId,
                StageName = events.First().StageName,
                DetailName = events.First().DetailName,
                Events = events,
                StartTime = events.Where(e => e.EventType == "Started").FirstOrDefault()?.EventTime,
                EndTime = events.Where(e => e.EventType == "Completed").FirstOrDefault()?.EventTime
            };

            // Вычисляем время в каждом статусе
            timeline.TimeInEachStatus = CalculateTimeInEachStatus(events);

            if (timeline.StartTime.HasValue && timeline.EndTime.HasValue)
            {
                timeline.TotalDuration = timeline.EndTime.Value - timeline.StartTime.Value;
            }

            return View(timeline);
        }

        

        /// <summary>
        /// Dashboard событий
        /// </summary>
        public async Task<IActionResult> Dashboard()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // Получаем статистику за сегодня
            var todayStageEventsCount = await _eventRepository.GetStageEventsCountAsync(today, tomorrow);
            var todaySystemEventsCount = await _eventRepository.GetSystemEventsCountAsync(today, tomorrow);
            var todayErrors = await _eventRepository.GetSystemEventsCountAsync(today, tomorrow, severity: "Error");
            var todayWarnings = await _eventRepository.GetSystemEventsCountAsync(today, tomorrow, severity: "Warning");
            var criticalEvents = await _eventRepository.GetUnprocessedCriticalEventsAsync();

            // Получаем последние события
            var recentStageEvents = await _eventRepository.GetStageEventsAsync(
                startDate: today,
                take: 10);

            var recentSystemEvents = await _eventRepository.GetSystemEventsAsync(
                startDate: today,
                take: 10);

            // Получаем активность операторов
            var operatorActivity = await _eventRepository.GetOperatorActivityAsync(today, tomorrow);

            // События по часам
            var eventsByHour = await GetEventsByHour(today);

            var dashboard = new EventDashboardDto
            {
                TodayStageEvents = todayStageEventsCount,
                TodaySystemEvents = todaySystemEventsCount,
                TodayErrors = todayErrors,
                TodayWarnings = todayWarnings,
                UnprocessedCriticalEvents = criticalEvents.Count,
                ActiveOperators = operatorActivity.Count,
                RecentStageEvents = recentStageEvents.Select(ConvertToStageEventDto).ToList(),
                RecentSystemEvents = recentSystemEvents.Select(ConvertToSystemEventDto).ToList(),
                CriticalEvents = criticalEvents.Select(ConvertToSystemEventDto).ToList(),
                EventsByHour = eventsByHour,
                TopOperators = operatorActivity.Take(5).Select(o => new OperatorActivityDto
                {
                    OperatorId = o.OperatorId,
                    OperatorName = o.OperatorName,
                    EventCount = o.EventCount,
                    LastActivity = o.LastActivity
                }).ToList()
            };

            return View(dashboard);
        }

        /// <summary>
        /// API: Получить события этапа
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetStageEvents(int stageId)
        {
            try
            {
                var events = await _eventLogService.GetStageEventsAsync(stageId);
                return Json(events);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// API: Отметить системное событие как обработанное
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> MarkAsProcessed(int eventId, string processedBy)
        {
            try
            {
                await _eventRepository.MarkSystemEventAsProcessedAsync(eventId, processedBy);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// API: Экспорт событий
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Export(EventFilterDto filter, string format = "csv")
        {
            try
            {
                filter ??= new EventFilterDto
                {
                    StartDate = DateTime.Today.AddDays(-30),
                    EndDate = DateTime.Today.AddDays(1)
                };

                var stageEvents = await _eventRepository.GetStageEventsAsync(
                    startDate: filter.StartDate,
                    endDate: filter.EndDate,
                    machineId: filter.MachineId,
                    batchId: filter.BatchId,
                    eventType: filter.EventType,
                    operatorId: filter.OperatorId,
                    isAutomatic: filter.IsAutomatic,
                    take: 10000); // Ограничиваем экспорт

                var exportData = stageEvents.Select(e => new EventExportDto
                {
                    EventTime = e.EventTimeUtc,
                    EventType = e.EventType,
                    Category = "StageExecution",
                    Title = $"{e.StageExecution?.RouteStage?.Name}",
                    Description = e.Comment,
                    OperatorName = e.OperatorName,
                    MachineName = e.NewMachine?.Name ?? e.StageExecution?.Machine?.Name,
                    DetailName = e.StageExecution?.SubBatch?.Batch?.Detail?.Name,
                    StageName = e.StageExecution?.RouteStage?.Name,
                    Source = e.IsAutomatic ? "System" : "User",
                    IsAutomatic = e.IsAutomatic,
                    Comment = e.Comment,
                    Duration = e.DurationInPreviousState?.ToString(@"hh\:mm\:ss") ?? ""
                }).ToList();

                if (format.ToLower() == "csv")
                {
                    var csv = GenerateCsv(exportData);
                    var fileName = $"events_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                    return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
                }

                return BadRequest("Неподдерживаемый формат экспорта");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Вспомогательные методы

        private StageEventDto ConvertToStageEventDto(StageEvent e)
        {
            return new StageEventDto
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
            };
        }

        private SystemEventDto ConvertToSystemEventDto(SystemEvent e)
        {
            return new SystemEventDto
            {
                Id = e.Id,
                Category = e.Category,
                EventType = e.EventType,
                Severity = e.Severity,
                Title = e.Title,
                Description = e.Description,
                EventTime = e.EventTimeUtc,
                Source = e.Source,
                UserId = e.UserId,
                UserName = e.UserName,
                IpAddress = e.IpAddress,
                RelatedEntityId = e.RelatedEntityId,
                RelatedEntityType = e.RelatedEntityType,
                IsProcessed = e.IsProcessed,
                ProcessedTime = e.ProcessedTimeUtc,
                ProcessedBy = e.ProcessedBy
            };
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
                "Prioritized" => "Приоритизирован",
                _ => eventType
            };
        }

        private Dictionary<string, TimeSpan> CalculateTimeInEachStatus(List<StageEventDto> events)
        {
            var result = new Dictionary<string, TimeSpan>();

            for (int i = 0; i < events.Count - 1; i++)
            {
                var currentEvent = events[i];
                var nextEvent = events[i + 1];

                if (!string.IsNullOrEmpty(currentEvent.NewStatus))
                {
                    var duration = nextEvent.EventTime - currentEvent.EventTime;

                    if (result.ContainsKey(currentEvent.NewStatus))
                        result[currentEvent.NewStatus] = result[currentEvent.NewStatus].Add(duration);
                    else
                        result[currentEvent.NewStatus] = duration;
                }
            }

            return result;
        }

        private async Task<Dictionary<string, int>> GetEventsByHour(DateTime date)
        {
            var result = new Dictionary<string, int>();

            for (int hour = 0; hour < 24; hour++)
            {
                var hourStart = date.AddHours(hour);
                var hourEnd = hourStart.AddHours(1);

                var count = await _eventRepository.GetStageEventsCountAsync(
                    startDate: hourStart,
                    endDate: hourEnd);

                result[$"{hour:D2}:00"] = count;
            }

            return result;
        }

        private string GenerateCsv(List<EventExportDto> data)
        {
            var csv = new System.Text.StringBuilder();

            // Заголовки
            csv.AppendLine("Время,Тип события,Категория,Заголовок,Описание,Оператор,Станок,Деталь,Этап,Источник,Автоматическое,Комментарий,Длительность");

            // Данные
            foreach (var item in data)
            {
                csv.AppendLine($"{item.EventTime:yyyy-MM-dd HH:mm:ss}," +
                              $"{item.EventType}," +
                              $"{item.Category}," +
                              $"\"{item.Title}\"," +
                              $"\"{item.Description}\"," +
                              $"{item.OperatorName}," +
                              $"{item.MachineName}," +
                              $"{item.DetailName}," +
                              $"{item.StageName}," +
                              $"{item.Source}," +
                              $"{(item.IsAutomatic ? "Да" : "Нет")}," +
                              $"\"{item.Comment}\"," +
                              $"{item.Duration}");
            }

            return csv.ToString();
        }

        private Dictionary<string, string> GetEventTypes()
        {
            return new Dictionary<string, string>
            {
                { StageEventTypes.Created, "Создан" },
                { StageEventTypes.Assigned, "Назначен" },
                { StageEventTypes.Started, "Запущен" },
                { StageEventTypes.Paused, "Приостановлен" },
                { StageEventTypes.Resumed, "Возобновлен" },
                { StageEventTypes.Completed, "Завершен" },
                { StageEventTypes.Cancelled, "Отменен" },
                { StageEventTypes.Reassigned, "Переназначен" },
                { StageEventTypes.Failed, "Ошибка" },
                { StageEventTypes.Prioritized, "Приоритизирован" }
            };
        }

        private Dictionary<string, string> GetCategories()
        {
            return new Dictionary<string, string>
            {
                { SystemEventCategories.System, "Система" },
                { SystemEventCategories.Security, "Безопасность" },
                { SystemEventCategories.Configuration, "Конфигурация" },
                { SystemEventCategories.Performance, "Производительность" },
                { SystemEventCategories.Integration, "Интеграция" },
                { SystemEventCategories.Maintenance, "Обслуживание" },
                { SystemEventCategories.UserActivity, "Действия пользователей" }
            };
        }

        private Dictionary<string, string> GetSeverities()
        {
            return new Dictionary<string, string>
            {
                { EventSeverity.Critical, "Критическая" },
                { EventSeverity.Error, "Ошибка" },
                { EventSeverity.Warning, "Предупреждение" },
                { EventSeverity.Info, "Информация" },
                { EventSeverity.Debug, "Отладка" }
            };
        }
    }
}