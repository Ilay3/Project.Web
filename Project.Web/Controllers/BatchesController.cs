using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
using Project.Contracts.Enums;
using Project.Web.ViewModels;

namespace Project.Web.Controllers
{
    public class BatchesController : Controller
    {
        private readonly BatchService _batchService;
        private readonly DetailService _detailService;
        private readonly ProductionSchedulerService _schedulerService;
        private readonly ILogger<BatchesController> _logger;

        public BatchesController(
            BatchService batchService,
            DetailService detailService,
            ProductionSchedulerService schedulerService,
            ILogger<BatchesController> logger)
        {
            _batchService = batchService;
            _detailService = detailService;
            _schedulerService = schedulerService;
            _logger = logger;
        }

        /// <summary>
        /// Список партий согласно ТЗ
        /// </summary>
        public async Task<IActionResult> Index(BatchFilterViewModel? filter, int page = 1, int pageSize = 20)
        {
            try
            {
                filter ??= new BatchFilterViewModel();

                var allBatches = await _batchService.GetAllAsync();

                // Применяем фильтры
                var filteredBatches = allBatches.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
                {
                    filteredBatches = filteredBatches.Where(b =>
                        b.DetailName.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                        b.DetailNumber.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase));
                }

                if (filter.Priority.HasValue)
                {
                    filteredBatches = filteredBatches.Where(b =>
                        MapFromContractPriority(b.Priority) == filter.Priority.Value);
                }

                if (filter.CreatedFrom.HasValue)
                {
                    filteredBatches = filteredBatches.Where(b => b.CreatedUtc.Date >= filter.CreatedFrom.Value.Date);
                }

                if (filter.CreatedTo.HasValue)
                {
                    filteredBatches = filteredBatches.Where(b => b.CreatedUtc.Date <= filter.CreatedTo.Value.Date);
                }

                // Фильтры по статусу
                if (!filter.ShowCompleted)
                {
                    filteredBatches = filteredBatches.Where(b => b.CompletionPercentage < 100);
                }

                if (!filter.ShowInProgress)
                {
                    filteredBatches = filteredBatches.Where(b => b.StageStatistics.InProgressStages == 0);
                }

                if (!filter.ShowPending)
                {
                    filteredBatches = filteredBatches.Where(b => b.StageStatistics.AwaitingStartStages == 0);
                }

                // Пагинация
                var totalItems = filteredBatches.Count();
                var paginatedBatches = filteredBatches
                    .OrderByDescending(b => b.CreatedUtc)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var viewModel = new BatchesIndexViewModel
                {
                    Filter = filter,
                    Pagination = new PaginationViewModel
                    {
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalItems = totalItems
                    },
                    Batches = paginatedBatches.Select(b => new BatchItemViewModel
                    {
                        Id = b.Id,
                        DetailName = b.DetailName,
                        DetailNumber = b.DetailNumber,
                        Quantity = b.Quantity,
                        CreatedUtc = b.CreatedUtc,
                        Priority = MapFromContractPriority(b.Priority),
                        CompletionPercentage = b.CompletionPercentage,
                        TotalStages = b.StageStatistics.TotalStages,
                        CompletedStages = b.StageStatistics.CompletedStages,
                        InProgressStages = b.StageStatistics.InProgressStages,
                        QueuedStages = b.StageStatistics.InQueueStages,
                        TotalPlannedTimeHours = b.TotalPlannedTimeHours,
                        TotalActualTimeHours = b.TotalActualTimeHours,
                        EstimatedCompletionTimeUtc = b.EstimatedCompletionTimeUtc,
                        CanEdit = b.CanEdit,
                        CanDelete = b.CanDelete
                    }).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка партий");
                TempData["Error"] = "Произошла ошибка при загрузке списка партий";
                return View(new BatchesIndexViewModel());
            }
        }

        /// <summary>
        /// Детальная информация о партии
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var batch = await _batchService.GetByIdAsync(id);
                if (batch == null)
                {
                    return NotFound("Партия не найдена");
                }

                // Получаем этапы выполнения для партии
                var stageExecutions = await _batchService.GetStageExecutionsForBatchAsync(id);

                // Получаем статистику партии
                var statistics = await _batchService.GetBatchStatisticsAsync(id);

                var viewModel = new BatchDetailsViewModel
                {
                    Id = batch.Id,
                    DetailName = batch.DetailName,
                    DetailNumber = batch.DetailNumber,
                    Quantity = batch.Quantity,
                    CreatedUtc = batch.CreatedUtc,
                    Priority = MapFromContractPriority(batch.Priority),
                    CompletionPercentage = batch.CompletionPercentage,
                    CanEdit = batch.CanEdit,
                    CanDelete = batch.CanDelete,
                    SubBatches = batch.SubBatches.Select(sb => new SubBatchViewModel
                    {
                        Id = sb.Id,
                        Quantity = sb.Quantity,
                        CompletionPercentage = sb.CompletionPercentage,
                        CurrentStageName = sb.CurrentStageName,
                        NextStageName = sb.NextStageName,
                        Stages = sb.StageExecutions.Select(se => new StageExecutionSummaryViewModel
                        {
                            Id = se.Id,
                            StageName = se.StageName,
                            MachineName = se.MachineName,
                            Status = se.Status,
                            IsSetup = se.IsSetup,
                            Priority = MapFromContractPriority(se.Priority),
                            StartTimeUtc = se.StartTimeUtc,
                            EndTimeUtc = se.EndTimeUtc,
                            PlannedDurationHours = se.PlannedDurationHours,
                            ActualDurationHours = se.ActualDurationHours
                        }).ToList()
                    }).ToList(),
                    Statistics = new BatchStatisticsViewModel
                    {
                        TotalStages = statistics.TotalStages,
                        CompletedStages = statistics.CompletedStages,
                        InProgressStages = statistics.InProgressStages,
                        QueuedStages = statistics.QueuedStages,
                        PausedStages = statistics.PausedStages,
                        TotalPlannedHours = statistics.PlannedTotalHours,
                        TotalActualHours = statistics.TotalWorkHours + statistics.TotalSetupHours,
                        EfficiencyPercentage = (double?)statistics.EfficiencyPercentage,
                        EstimatedCompletionTime = statistics.EstimatedCompletionTime
                    }
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении партии {BatchId}", id);
                return NotFound("Партия не найдена");
            }
        }

        /// <summary>
        /// Форма создания новой партии согласно ТЗ
        /// </summary>
        public async Task<IActionResult> Create()
        {
            try
            {
                var details = await _detailService.GetDetailsForBatchCreationAsync();

                var viewModel = new BatchCreateViewModel
                {
                    AvailableDetails = details.Select(d => new DetailForBatchOption
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Number = d.Number,
                        HasRoute = d.HasRoute,
                        RouteStagesCount = d.RouteStagesCount,
                        EstimatedTimePerUnit = d.EstimatedDuration?.TotalHours
                    }).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке формы создания партии");
                return View(new BatchCreateViewModel());
            }
        }

        /// <summary>
        /// Создание новой партии
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BatchCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadAvailableDetails(model);
                return View(model);
            }

            try
            {
                var createDto = new BatchCreateDto
                {
                    DetailId = model.DetailId,
                    Quantity = model.Quantity,
                    Priority = MapToContractPriority(model.Priority),
                    AutoStartPlanning = model.AutoStartPlanning
                };

                // Создаем подпартии если нужно
                if (model.SplitIntoBatches && model.SubBatchSize.HasValue && model.SubBatchSize.Value > 0)
                {
                    createDto.SubBatches = new List<SubBatchCreateDto>();
                    var remainingQuantity = model.Quantity;

                    while (remainingQuantity > 0)
                    {
                        var batchSize = Math.Min(remainingQuantity, model.SubBatchSize.Value);
                        createDto.SubBatches.Add(new SubBatchCreateDto { Quantity = batchSize });
                        remainingQuantity -= batchSize;
                    }
                }

                var id = await _batchService.CreateAsync(createDto);

                TempData["Success"] = "Партия успешно создана";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError("", ex.Message);
                await LoadAvailableDetails(model);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании партии {@Model}", model);
                ModelState.AddModelError("", "Произошла ошибка при создании партии");
                await LoadAvailableDetails(model);
                return View(model);
            }
        }

        /// <summary>
        /// Удаление партии
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _batchService.DeleteAsync(id);
                TempData["Success"] = "Партия успешно удалена";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении партии {BatchId}", id);
                TempData["Error"] = "Произошла ошибка при удалении партии";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        /// <summary>
        /// Прогноз расписания для партии
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetScheduleForecast(int detailId, int quantity)
        {
            try
            {
                var forecast = await _schedulerService.PredictOptimalScheduleForDetailAsync(detailId, quantity);
                return Json(forecast);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении прогноза расписания для детали {DetailId}", detailId);
                return Json(null);
            }
        }

        #region Вспомогательные методы

        private async Task LoadAvailableDetails(BatchCreateViewModel model)
        {
            try
            {
                var details = await _detailService.GetDetailsForBatchCreationAsync();
                model.AvailableDetails = details.Select(d => new DetailForBatchOption
                {
                    Id = d.Id,
                    Name = d.Name,
                    Number = d.Number,
                    HasRoute = d.HasRoute,
                    RouteStagesCount = d.RouteStagesCount,
                    EstimatedTimePerUnit = d.EstimatedDuration?.TotalHours
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке деталей для создания партии");
                model.AvailableDetails = new List<DetailForBatchOption>();
            }
        }

        private Priority MapFromContractPriority(Project.Contracts.Enums.Priority priority)
        {
            return priority switch
            {
                Project.Contracts.Enums.Priority.Low => Priority.Low,
                Project.Contracts.Enums.Priority.Normal => Priority.Normal,
                Project.Contracts.Enums.Priority.High => Priority.High,
                Project.Contracts.Enums.Priority.Critical => Priority.Critical,
                _ => Priority.Normal
            };
        }

        private Project.Contracts.Enums.Priority MapToContractPriority(Priority priority)
        {
            return priority switch
            {
                Priority.Low => Project.Contracts.Enums.Priority.Low,
                Priority.Normal => Project.Contracts.Enums.Priority.Normal,
                Priority.High => Project.Contracts.Enums.Priority.High,
                Priority.Critical => Project.Contracts.Enums.Priority.Critical,
                _ => Project.Contracts.Enums.Priority.Normal
            };
        }

        #endregion
    }
}