using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
using Project.Contracts.Enums;
using Project.Web.ViewModels;

namespace Project.Web.Controllers
{
    public class GanttController : Controller
    {
        private readonly StageExecutionService _stageService;
        private readonly MachineService _machineService;
        private readonly MachineTypeService _machineTypeService;
        private readonly DetailService _detailService;
        private readonly ProductionSchedulerService _schedulerService;
        private readonly ILogger<GanttController> _logger;

        public GanttController(
            StageExecutionService stageService,
            MachineService machineService,
            MachineTypeService machineTypeService,
            DetailService detailService,
            ProductionSchedulerService schedulerService,
            ILogger<GanttController> logger)
        {
            _stageService = stageService;
            _machineService = machineService;
            _machineTypeService = machineTypeService;
            _detailService = detailService;
            _schedulerService = schedulerService;
            _logger = logger;
        }

        /// <summary>
        /// Диаграмма Ганта согласно ТЗ
        /// </summary>
        public async Task<IActionResult> Index(GanttFilterViewModel? filter)
        {
            try
            {
                filter ??= new GanttFilterViewModel();

                // Загружаем списки для фильтров
                await LoadFilterOptions(filter);

                // Получаем данные для диаграммы
                var ganttData = await BuildGanttData(filter);

                return View(ganttData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке диаграммы Ганта");
                TempData["Error"] = "Произошла ошибка при загрузке диаграммы Ганта";
                return View(new GanttViewModel());
            }
        }

        /// <summary>
        /// API для получения данных диаграммы Ганта
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetGanttData(GanttFilterViewModel filter)
        {
            try
            {
                var ganttData = await BuildGanttData(filter);
                return Json(ganttData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении данных диаграммы Ганта");
                return Json(new { error = "Ошибка при загрузке данных" });
            }
        }

        /// <summary>
        /// Управление задачей в диаграмме Ганта
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ManageTask(int stageExecutionId, string action, string? reason, int? newMachineId)
        {
            try
            {
                switch (action.ToLower())
                {
                    case "start":
                        await _stageService.StartStageExecution(stageExecutionId,
                            operatorId: User.Identity?.Name, deviceId: "WEB");
                        return Json(new { success = true, message = "Этап запущен" });

                    case "pause":
                        await _stageService.PauseStageExecution(stageExecutionId,
                            operatorId: User.Identity?.Name, reasonNote: reason, deviceId: "WEB");
                        return Json(new { success = true, message = "Этап приостановлен" });

                    case "resume":
                        await _stageService.ResumeStageExecution(stageExecutionId,
                            operatorId: User.Identity?.Name, reasonNote: reason, deviceId: "WEB");
                        return Json(new { success = true, message = "Этап возобновлен" });

                    case "complete":
                        await _stageService.CompleteStageExecution(stageExecutionId,
                            operatorId: User.Identity?.Name, reasonNote: reason, deviceId: "WEB");
                        return Json(new { success = true, message = "Этап завершен" });

                    case "cancel":
                        await _stageService.CancelStageExecution(stageExecutionId,
                            reason ?? "Отменен пользователем", operatorId: User.Identity?.Name, deviceId: "WEB");
                        return Json(new { success = true, message = "Этап отменен" });

                    case "reassign":
                        if (newMachineId.HasValue)
                        {
                            await _schedulerService.ReassignStageToMachineAsync(stageExecutionId, newMachineId.Value);
                            return Json(new { success = true, message = "Этап переназначен" });
                        }
                        return Json(new { success = false, message = "Не указан новый станок" });

                    default:
                        return Json(new { success = false, message = "Неизвестное действие" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при управлении задачей {StageId}, действие: {Action}",
                    stageExecutionId, action);
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Получение информации о задаче для модального окна
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetTaskDetails(int stageExecutionId)
        {
            try
            {
                // Здесь можно получить детальную информацию об этапе
                // Пока возвращаем заглушку
                return Json(new
                {
                    success = true,
                    stageId = stageExecutionId,
                    // Добавить детальную информацию
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении деталей задачи {StageId}", stageExecutionId);
                return Json(new { success = false, message = "Ошибка при загрузке данных" });
            }
        }

        /// <summary>
        /// Изменение приоритета в очереди
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ReorderQueue(int machineId, int priorityStageId)
        {
            try
            {
                await _schedulerService.ReassignQueueAsync(machineId, priorityStageId);
                return Json(new { success = true, message = "Очередь обновлена" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при изменении приоритета в очереди");
                return Json(new { success = false, message = ex.Message });
            }
        }

        #region Приватные методы

        private async Task<GanttViewModel> BuildGanttData(GanttFilterViewModel filter)
        {
            // Получаем все этапы для указанного периода
            var allStages = await _stageService.GetAllStagesForGanttChartAsync(
                filter.StartDate, filter.EndDate);

            // Применяем фильтры
            var filteredStages = ApplyFilters(allStages, filter);

            // Получаем станки
            var machines = await _machineService.GetAllAsync();
            var filteredMachines = machines.AsEnumerable();

            if (filter.SelectedMachineIds?.Any() == true)
            {
                filteredMachines = filteredMachines.Where(m => filter.SelectedMachineIds.Contains(m.Id));
            }

            if (filter.SelectedMachineTypeIds?.Any() == true)
            {
                filteredMachines = filteredMachines.Where(m => filter.SelectedMachineTypeIds.Contains(m.MachineTypeId));
            }

            // Строим строки станков для диаграммы
            var machineRows = filteredMachines.Select(m => new GanttMachineRowViewModel
            {
                MachineId = m.Id,
                MachineName = m.Name,
                MachineTypeName = m.MachineTypeName,
                Status = m.Status,
                UtilizationPercentage = m.TodayUtilizationPercent ?? 0,
                QueueLength = m.QueueLength
            }).ToList();

            // Строим задачи для диаграммы
            var tasks = BuildGanttTasks(filteredStages, filter);

            // Строим временную шкалу
            var timeline = BuildTimeline(filter.StartDate, filter.EndDate);

            return new GanttViewModel
            {
                Filter = filter,
                MachineRows = machineRows,
                Tasks = tasks,
                Timeline = timeline
            };
        }

        private List<GanttDto> ApplyFilters(List<GanttDto> stages, GanttFilterViewModel filter)
        {
            var filtered = stages.AsEnumerable();

            if (filter.SelectedMachineIds?.Any() == true)
            {
                filtered = filtered.Where(s => s.MachineId.HasValue &&
                    filter.SelectedMachineIds.Contains(s.MachineId.Value));
            }

            if (filter.SelectedDetailIds?.Any() == true)
            {
                filtered = filtered.Where(s => filter.SelectedDetailIds.Contains(s.BatchId));
            }

            if (filter.SelectedStatuses?.Any() == true)
            {
                filtered = filtered.Where(s => filter.SelectedStatuses.Contains(s.Status));
            }

            if (filter.ShowSetupsOnly)
            {
                filtered = filtered.Where(s => s.IsSetup);
            }

            if (filter.ShowOperationsOnly)
            {
                filtered = filtered.Where(s => !s.IsSetup);
            }

            if (filter.ShowOverdueOnly)
            {
                filtered = filtered.Where(s => s.IsOverdue);
            }

            if (filter.MinPriority.HasValue)
            {
                filtered = filtered.Where(s => s.Priority >= filter.MinPriority.Value);
            }

            return filtered.ToList();
        }

        private List<GanttTaskViewModel> BuildGanttTasks(List<GanttDto> stages, GanttFilterViewModel filter)
        {
            var tasks = new List<GanttTaskViewModel>();
            var timelineDuration = filter.EndDate - filter.StartDate;

            foreach (var stage in stages)
            {
                if (!stage.MachineId.HasValue) continue;

                var task = new GanttTaskViewModel
                {
                    Id = stage.Id,
                    MachineId = stage.MachineId.Value,
                    BatchId = stage.BatchId,
                    SubBatchId = stage.SubBatchId,
                    DetailName = stage.DetailName,
                    DetailNumber = stage.DetailNumber,
                    StageName = stage.StageName,
                    PlannedStartTime = stage.PlannedStartTime,
                    PlannedEndTime = stage.PlannedEndTime,
                    ActualStartTime = stage.ActualStartTime,
                    ActualEndTime = stage.ActualEndTime,
                    Status = stage.Status,
                    IsSetup = stage.IsSetup,
                    Priority = stage.Priority,
                    IsCritical = stage.IsCritical,
                    IsOverdue = stage.IsOverdue,
                    Quantity = stage.Quantity,
                    OperatorId = stage.OperatorId,
                    CompletionPercentage = stage.CompletionPercentage
                };

                // Рассчитываем позицию и ширину на диаграмме
                var startTime = task.DisplayStartTime;
                var endTime = task.DisplayEndTime;

                if (startTime < filter.EndDate && endTime > filter.StartDate)
                {
                    // Обрезаем время по границам диаграммы
                    var clampedStart = startTime < filter.StartDate ? filter.StartDate : startTime;
                    var clampedEnd = endTime > filter.EndDate ? filter.EndDate : endTime;

                    var leftOffset = clampedStart - filter.StartDate;
                    var duration = clampedEnd - clampedStart;

                    task.LeftPositionPercent = (leftOffset.TotalMinutes / timelineDuration.TotalMinutes) * 100;
                    task.WidthPercent = (duration.TotalMinutes / timelineDuration.TotalMinutes) * 100;

                    tasks.Add(task);
                }
            }

            return tasks;
        }

        private GanttTimelineViewModel BuildTimeline(DateTime startDate, DateTime endDate)
        {
            var timeline = new GanttTimelineViewModel
            {
                StartDate = startDate,
                EndDate = endDate,
                TimeMarks = new List<GanttTimeMarkViewModel>()
            };

            var duration = endDate - startDate;
            var current = startDate;

            // Генерируем временные метки
            while (current <= endDate)
            {
                var isMajor = current.Hour == 0 || current.Hour == 8 || current.Hour == 12 || current.Hour == 18;
                var positionPercent = ((current - startDate).TotalMinutes / duration.TotalMinutes) * 100;

                timeline.TimeMarks.Add(new GanttTimeMarkViewModel
                {
                    Time = current,
                    DisplayText = isMajor ? current.ToString("dd.MM HH:mm") : current.ToString("HH:mm"),
                    IsMajor = isMajor,
                    PositionPercent = positionPercent
                });

                current = current.AddHours(1);
            }

            return timeline;
        }

        private async Task LoadFilterOptions(GanttFilterViewModel filter)
        {
            try
            {
                // Загружаем станки
                var machines = await _machineService.GetAllAsync();
                filter.AvailableMachines = machines.Select(m => new SelectOptionViewModel
                {
                    Id = m.Id,
                    Name = $"{m.Name} ({m.InventoryNumber})"
                }).ToList();

                // Загружаем типы станков
                var machineTypes = await _machineTypeService.GetAllAsync();
                filter.AvailableMachineTypes = machineTypes.Select(mt => new SelectOptionViewModel
                {
                    Id = mt.Id,
                    Name = mt.Name
                }).ToList();

                // Загружаем детали
                var details = await _detailService.GetAllAsync();
                filter.AvailableDetails = details.Select(d => new SelectOptionViewModel
                {
                    Id = d.Id,
                    Name = $"{d.Number} - {d.Name}"
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке опций фильтра");
                filter.AvailableMachines = new List<SelectOptionViewModel>();
                filter.AvailableMachineTypes = new List<SelectOptionViewModel>();
                filter.AvailableDetails = new List<SelectOptionViewModel>();
            }
        }

        #endregion
    }
}