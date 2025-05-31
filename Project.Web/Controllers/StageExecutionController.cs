using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.Enums;
using Project.Web.ViewModels;

namespace Project.Web.Controllers
{
    /// <summary>
    /// Контроллер управления этапами выполнения согласно ТЗ
    /// </summary>
    public class StageExecutionController : Controller
    {
        private readonly StageExecutionService _stageService;
        private readonly ProductionSchedulerService _schedulerService;
        private readonly MachineService _machineService;
        private readonly DetailService _detailService;
        private readonly BatchService _batchService;
        private readonly ILogger<StageExecutionController> _logger;

        public StageExecutionController(
            StageExecutionService stageService,
            ProductionSchedulerService schedulerService,
            MachineService machineService,
            DetailService detailService,
            BatchService batchService,
            ILogger<StageExecutionController> logger)
        {
            _stageService = stageService;
            _schedulerService = schedulerService;
            _machineService = machineService;
            _detailService = detailService;
            _batchService = batchService;
            _logger = logger;
        }

        /// <summary>
        /// Список этапов выполнения согласно ТЗ
        /// </summary>
        public async Task<IActionResult> Index(StageExecutionFilterViewModel? filter, int page = 1, int pageSize = 20)
        {
            try
            {
                filter ??= new StageExecutionFilterViewModel();

                // Получаем статистику
                var statistics = await _stageService.GetExecutionStatisticsAsync(filter.StartDate, filter.EndDate);

                // Здесь должен быть вызов метода получения этапов с фильтрацией
                // Пока используем заглушку
                var allStages = new List<StageExecutionItemViewModel>();

                // Применяем фильтры и пагинацию
                var filteredStages = allStages.AsEnumerable();

                // Фильтрация по поисковому термину
                if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
                {
                    filteredStages = filteredStages.Where(s =>
                        s.DetailName.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                        s.StageName.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase));
                }

                // Фильтры по статусам
                if (filter.SelectedStatuses?.Any() == true)
                {
                    filteredStages = filteredStages.Where(s => filter.SelectedStatuses.Contains(s.Status));
                }

                // Фильтр по станку
                if (filter.MachineId.HasValue)
                {
                    filteredStages = filteredStages.Where(s => s.MachineName != null);
                }

                // Фильтр по приоритету
                if (filter.MinPriority.HasValue)
                {
                    filteredStages = filteredStages.Where(s => s.Priority >= filter.MinPriority.Value);
                }

                // Только переналадки
                if (filter.ShowSetupsOnly)
                {
                    filteredStages = filteredStages.Where(s => s.IsSetup);
                }

                // Только просроченные
                if (filter.ShowOverdueOnly)
                {
                    filteredStages = filteredStages.Where(s => s.IsOverdue);
                }

                // Только критические
                if (filter.ShowCriticalOnly)
                {
                    filteredStages = filteredStages.Where(s => s.IsCritical);
                }

                // Пагинация
                var totalItems = filteredStages.Count();
                var paginatedStages = filteredStages
                    .OrderByDescending(s => s.Priority)
                    .ThenBy(s => s.CreatedUtc)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Загружаем данные для фильтров
                await LoadFilterOptions(filter);

                var viewModel = new StageExecutionIndexViewModel
                {
                    Filter = filter,
                    Pagination = new PaginationViewModel
                    {
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalItems = totalItems
                    },
                    StageExecutions = paginatedStages
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка этапов выполнения");
                TempData["Error"] = "Произошла ошибка при загрузке списка этапов";
                return View(new StageExecutionIndexViewModel());
            }
        }

        /// <summary>
        /// Детальная информация об этапе выполнения
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                // Здесь должен быть вызов получения детальной информации об этапе
                // Пока используем заглушку
                var viewModel = new StageExecutionDetailsViewModel
                {
                    Id = id,
                    DetailName = "Деталь-001",
                    StageName = "Токарная обработка",
                    Status = StageStatus.InProgress,
                    CreatedUtc = DateTime.UtcNow.AddHours(-2)
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении этапа выполнения {StageId}", id);
                return NotFound("Этап выполнения не найден");
            }
        }

        /// <summary>
        /// Запуск этапа
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int id, string? reasonNote)
        {
            try
            {
                await _stageService.StartStageExecution(id,
                    operatorId: User.Identity?.Name,
                    deviceId: "WEB");

                TempData["Success"] = "Этап успешно запущен";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при запуске этапа {StageId}", id);
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        /// <summary>
        /// Приостановка этапа
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pause(int id, string? reasonNote)
        {
            try
            {
                await _stageService.PauseStageExecution(id,
                    operatorId: User.Identity?.Name,
                    reasonNote: reasonNote ?? "Приостановлен пользователем",
                    deviceId: "WEB");

                TempData["Success"] = "Этап успешно приостановлен";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при приостановке этапа {StageId}", id);
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        /// <summary>
        /// Возобновление этапа
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resume(int id, string? reasonNote)
        {
            try
            {
                await _stageService.ResumeStageExecution(id,
                    operatorId: User.Identity?.Name,
                    reasonNote: reasonNote ?? "Возобновлен пользователем",
                    deviceId: "WEB");

                TempData["Success"] = "Этап успешно возобновлен";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при возобновлении этапа {StageId}", id);
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        /// <summary>
        /// Завершение этапа
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id, string? reasonNote)
        {
            try
            {
                await _stageService.CompleteStageExecution(id,
                    operatorId: User.Identity?.Name,
                    reasonNote: reasonNote ?? "Завершен пользователем",
                    deviceId: "WEB");

                TempData["Success"] = "Этап успешно завершен";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при завершении этапа {StageId}", id);
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        /// <summary>
        /// Отмена этапа
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string reason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason))
                {
                    TempData["Error"] = "Необходимо указать причину отмены";
                    return RedirectToAction(nameof(Details), new { id });
                }

                await _stageService.CancelStageExecution(id, reason,
                    operatorId: User.Identity?.Name,
                    deviceId: "WEB");

                TempData["Success"] = "Этап успешно отменен";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отмене этапа {StageId}", id);
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        /// <summary>
        /// Переназначение этапа на другой станок
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reassign(int id, int newMachineId)
        {
            try
            {
                await _schedulerService.ReassignStageToMachineAsync(id, newMachineId);

                TempData["Success"] = "Этап успешно переназначен";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при переназначении этапа {StageId}", id);
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        /// <summary>
        /// Форма управления этапом
        /// </summary>
        public async Task<IActionResult> Control(int id)
        {
            try
            {
                // Здесь должна быть загрузка информации об этапе
                var viewModel = new StageExecutionControlViewModel
                {
                    Id = id,
                    DetailName = "Деталь-001",
                    StageName = "Токарная обработка",
                    Status = StageStatus.AwaitingStart,
                    CanStart = true,
                    CanReassign = true
                };

                await LoadControlFormData(viewModel);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке формы управления этапом {StageId}", id);
                return NotFound("Этап выполнения не найден");
            }
        }

        /// <summary>
        /// Массовые операции над этапами
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkAction(List<int> stageIds, string action, string? reason)
        {
            try
            {
                if (!stageIds?.Any() == true)
                {
                    TempData["Error"] = "Не выбраны этапы для выполнения операции";
                    return RedirectToAction(nameof(Index));
                }

                var successCount = 0;
                var errorCount = 0;

                foreach (var stageId in stageIds)
                {
                    try
                    {
                        switch (action.ToLower())
                        {
                            case "start":
                                await _stageService.StartStageExecution(stageId, User.Identity?.Name, "WEB");
                                break;
                            case "pause":
                                await _stageService.PauseStageExecution(stageId, User.Identity?.Name, reason, "WEB");
                                break;
                            case "resume":
                                await _stageService.ResumeStageExecution(stageId, User.Identity?.Name, reason, "WEB");
                                break;
                            case "complete":
                                await _stageService.CompleteStageExecution(stageId, User.Identity?.Name, reason, "WEB");
                                break;
                            case "cancel":
                                await _stageService.CancelStageExecution(stageId, reason ?? "Массовая отмена", User.Identity?.Name, "WEB");
                                break;
                        }
                        successCount++;
                    }
                    catch (Exception)
                    {
                        errorCount++;
                    }
                }

                TempData["Success"] = $"Успешно обработано {successCount} этапов";
                if (errorCount > 0)
                {
                    TempData["Warning"] = $"Не удалось обработать {errorCount} этапов";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при массовых операциях над этапами");
                TempData["Error"] = "Произошла ошибка при выполнении массовых операций";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// API для получения статуса этапа
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetStageStatus(int id)
        {
            try
            {
                // Здесь должен быть вызов получения актуального статуса этапа
                return Json(new
                {
                    success = true,
                    stageId = id,
                    status = "InProgress",
                    canStart = false,
                    canPause = true,
                    canResume = false,
                    canComplete = true,
                    canCancel = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении статуса этапа {StageId}", id);
                return Json(new { success = false, message = "Ошибка при получении статуса" });
            }
        }

        /// <summary>
        /// API для получения доступных действий для этапа
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAvailableActions(int id)
        {
            try
            {
                // Здесь должна быть проверка доступных действий для этапа
                var actions = new List<object>
                {
                    new { action = "start", name = "Запустить", icon = "fas fa-play", cssClass = "btn-success" },
                    new { action = "pause", name = "Приостановить", icon = "fas fa-pause", cssClass = "btn-warning" },
                    new { action = "complete", name = "Завершить", icon = "fas fa-check", cssClass = "btn-primary" },
                    new { action = "cancel", name = "Отменить", icon = "fas fa-times", cssClass = "btn-danger" }
                };

                return Json(actions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении доступных действий для этапа {StageId}", id);
                return Json(new List<object>());
            }
        }

        #region Вспомогательные методы

        private async Task LoadFilterOptions(StageExecutionFilterViewModel filter)
        {
            try
            {
                var machines = await _machineService.GetAllAsync();
                filter.AvailableMachines = machines.Select(m => new SelectOptionViewModel
                {
                    Id = m.Id,
                    Name = $"{m.Name} ({m.InventoryNumber})"
                }).ToList();

                var details = await _detailService.GetAllAsync();
                filter.AvailableDetails = details.Select(d => new SelectOptionViewModel
                {
                    Id = d.Id,
                    Name = $"{d.Number} - {d.Name}"
                }).ToList();

                var batches = await _batchService.GetAllAsync();
                filter.AvailableBatches = batches.Select(b => new SelectOptionViewModel
                {
                    Id = b.Id,
                    Name = $"Партия #{b.Id} - {b.DetailName}"
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке опций фильтра");
                filter.AvailableMachines = new List<SelectOptionViewModel>();
                filter.AvailableDetails = new List<SelectOptionViewModel>();
                filter.AvailableBatches = new List<SelectOptionViewModel>();
            }
        }

        private async Task LoadControlFormData(StageExecutionControlViewModel model)
        {
            try
            {
                var machines = await _machineService.GetAllAsync();
                model.AvailableMachines = machines.Select(m => new SelectOptionViewModel
                {
                    Id = m.Id,
                    Name = $"{m.Name} ({m.InventoryNumber})"
                }).ToList();

                // Здесь можно добавить список операторов
                model.AvailableOperators = new List<SelectOptionViewModel>
                {
                    new() { Id = 1, Name = "Оператор 1" },
                    new() { Id = 2, Name = "Оператор 2" }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке данных формы управления");
                model.AvailableMachines = new List<SelectOptionViewModel>();
                model.AvailableOperators = new List<SelectOptionViewModel>();
            }
        }

        #endregion
    }
}