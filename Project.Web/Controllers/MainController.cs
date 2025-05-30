using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
using Project.Domain.Repositories;
using Project.Web.ViewModels;
using System.Threading.Tasks;

namespace Project.Web.Controllers
{
    public class MainController : Controller
    {
        private readonly DetailService _detailService;
        private readonly MachineTypeService _machineTypeService;
        private readonly MachineService _machineService;
        private readonly RouteService _routeService;
        private readonly BatchService _batchService;
        private readonly StageExecutionService _stageService;
        private readonly ProductionSchedulerService _schedulerService;
        private readonly SetupTimeService _setupTimeService;
        private readonly IBatchRepository _batchRepo;

        public MainController(
            DetailService detailService,
            MachineTypeService machineTypeService,
            MachineService machineService,
            RouteService routeService,
            BatchService batchService,
            StageExecutionService stageService,
            ProductionSchedulerService schedulerService,
            SetupTimeService setupTimeService,
            IBatchRepository batchRepo)
        {
            _detailService = detailService;
            _machineTypeService = machineTypeService;
            _machineService = machineService;
            _routeService = routeService;
            _batchService = batchService;
            _stageService = stageService;
            _schedulerService = schedulerService;
            _setupTimeService = setupTimeService;
            _batchRepo = batchRepo;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // Загружаем основные данные для главной страницы
                var details = await _detailService.GetAllAsync();
                var machineTypes = await _machineTypeService.GetAllAsync();
                var machines = await _machineService.GetAllAsync();
                var batches = await _batchService.GetAllAsync();

                // Получаем данные диаграммы Ганта (может быть пустым при первом запуске)
                List<GanttStageDto> ganttData;
                List<StageQueueDto> queueForecast;

                try
                {
                    ganttData = await _stageService.GetAllStagesForGanttChart();
                    queueForecast = await _schedulerService.GetQueueForecastAsync();
                }
                catch (Exception ex)
                {
                    // Логируем ошибку, но не прерываем работу
                    ganttData = new List<GanttStageDto>();
                    queueForecast = new List<StageQueueDto>();
                }

                var viewModel = new MainDashboardViewModel
                {
                    Details = details ?? new List<DetailDto>(),
                    MachineTypes = machineTypes ?? new List<MachineTypeDto>(),
                    Machines = machines ?? new List<MachineDto>(),
                    Batches = batches ?? new List<BatchDto>(),
                    GanttStages = ganttData ?? new List<GanttStageDto>(),
                    QueueItems = queueForecast ?? new List<StageQueueDto>()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                // Возвращаем пустую модель при ошибке
                var emptyModel = new MainDashboardViewModel
                {
                    Details = new List<DetailDto>(),
                    MachineTypes = new List<MachineTypeDto>(),
                    Machines = new List<MachineDto>(),
                    Batches = new List<BatchDto>(),
                    GanttStages = new List<GanttStageDto>(),
                    QueueItems = new List<StageQueueDto>()
                };

                ViewBag.ErrorMessage = $"Ошибка при загрузке данных: {ex.Message}";
                return View(emptyModel);
            }
        }


        // API методы для модальных окон

        #region Details Management

        [HttpPost]
        public async Task<IActionResult> CreateDetail([FromBody] DetailCreateDto dto)
        {
            try
            {
                await _detailService.AddAsync(dto);
                return Json(new { success = true, message = "Деталь успешно создана" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateDetail([FromBody] DetailEditDto dto)
        {
            try
            {
                await _detailService.UpdateAsync(dto);
                return Json(new { success = true, message = "Деталь успешно обновлена" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteDetail(int id)
        {
            try
            {
                await _detailService.DeleteAsync(id);
                return Json(new { success = true, message = "Деталь успешно удалена" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Machine Types Management

        [HttpPost]
        public async Task<IActionResult> CreateMachineType([FromBody] MachineTypeCreateDto dto)
        {
            try
            {
                await _machineTypeService.AddAsync(dto);
                return Json(new { success = true, message = "Тип станка успешно создан" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateMachineType([FromBody] MachineTypeEditDto dto)
        {
            try
            {
                await _machineTypeService.UpdateAsync(dto);
                return Json(new { success = true, message = "Тип станка успешно обновлен" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteMachineType(int id)
        {
            try
            {
                await _machineTypeService.DeleteAsync(id);
                return Json(new { success = true, message = "Тип станка успешно удален" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Machines Management

        [HttpPost]
        public async Task<IActionResult> CreateMachine([FromBody] MachineCreateDto dto)
        {
            try
            {
                await _machineService.AddAsync(dto);
                return Json(new { success = true, message = "Станок успешно создан" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateMachine([FromBody] MachineEditDto dto)
        {
            try
            {
                await _machineService.UpdateAsync(dto);
                return Json(new { success = true, message = "Станок успешно обновлен" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteMachine(int id)
        {
            try
            {
                await _machineService.DeleteAsync(id);
                return Json(new { success = true, message = "Станок успешно удален" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Routes Management

        [HttpGet]
        public async Task<IActionResult> GetRouteForDetail(int detailId)
        {
            try
            {
                var route = await _routeService.GetByDetailIdAsync(detailId);
                return Json(new { success = true, data = route });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateRoute([FromBody] RouteCreateDto dto)
        {
            try
            {
                await _routeService.AddAsync(dto);
                return Json(new { success = true, message = "Маршрут успешно создан" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateRoute([FromBody] RouteEditDto dto)
        {
            try
            {
                await _routeService.UpdateAsync(dto);
                return Json(new { success = true, message = "Маршрут успешно обновлен" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Batches Management

        [HttpPost]
        public async Task<IActionResult> CreateBatch([FromBody] BatchCreateDto dto)
        {
            try
            {
                // Дополнительная валидация
                if (dto.DetailId <= 0)
                {
                    return Json(new { success = false, message = "Не выбрана деталь" });
                }

                if (dto.Quantity <= 0)
                {
                    return Json(new { success = false, message = "Количество должно быть больше 0" });
                }

                // Проверяем, что деталь существует
                var detail = await _detailService.GetByIdAsync(dto.DetailId);
                if (detail == null)
                {
                    return Json(new { success = false, message = "Деталь не найдена" });
                }

                // Проверяем, что у детали есть маршрут
                var route = await _routeService.GetByDetailIdAsync(dto.DetailId);
                if (route == null)
                {
                    return Json(new { success = false, message = $"Для детали '{detail.Name}' не создан маршрут изготовления" });
                }

                if (route.Stages == null || !route.Stages.Any())
                {
                    return Json(new { success = false, message = $"Маршрут детали '{detail.Name}' не содержит этапов" });
                }

                // Валидация подпартий
                if (dto.SubBatches != null && dto.SubBatches.Any())
                {
                    var totalSubQuantity = dto.SubBatches.Sum(sb => sb.Quantity);
                    if (totalSubQuantity != dto.Quantity)
                    {
                        return Json(new { success = false, message = $"Сумма подпартий ({totalSubQuantity}) не равна общему количеству ({dto.Quantity})" });
                    }

                    if (dto.SubBatches.Any(sb => sb.Quantity <= 0))
                    {
                        return Json(new { success = false, message = "Количество в подпартиях должно быть больше 0" });
                    }
                }

                // Создаем партию через планировщик (он создаст этапы и запланирует их)
                var batchId = await _schedulerService.CreateBatchAsync(dto);

                return Json(new
                {
                    success = true,
                    message = "Партия успешно создана и запланирована",
                    batchId = batchId
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Stage Execution

        [HttpPost]
        public async Task<IActionResult> StartStage(int stageId)
        {
            try
            {
                await _stageService.StartStageExecution(stageId);
                return Json(new { success = true, message = "Этап запущен" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> PauseStage(int stageId)
        {
            try
            {
                await _stageService.PauseStageExecution(stageId);
                return Json(new { success = true, message = "Этап приостановлен" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ResumeStage(int stageId)
        {
            try
            {
                await _stageService.ResumeStageExecution(stageId);
                return Json(new { success = true, message = "Этап возобновлен" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CompleteStage(int stageId)
        {
            try
            {
                await _stageService.CompleteStageExecution(stageId);
                await _schedulerService.HandleStageCompletionAsync(stageId);
                return Json(new { success = true, message = "Этап завершен" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


        #endregion

        #region Setup Times Management

        [HttpPost]
        public async Task<IActionResult> CreateSetupTime([FromBody] SetupTimeDto dto)
        {
            try
            {
                await _setupTimeService.AddAsync(dto);
                return Json(new { success = true, message = "Время переналадки добавлено" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSetupTimes()
        {
            try
            {
                var setupTimes = await _setupTimeService.GetAllAsync();
                return Json(new { success = true, data = setupTimes });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Data Refresh

        [HttpGet]
        public async Task<IActionResult> RefreshData()
        {
            try
            {
                var ganttData = await _stageService.GetAllStagesForGanttChart();
                var queueForecast = await _schedulerService.GetQueueForecastAsync();
                var batches = await _batchService.GetAllAsync();

                return Json(new
                {
                    success = true,
                    ganttStages = ganttData,
                    queueItems = queueForecast,
                    batches = batches
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion


        #region Stage Management

        /// <summary>
        /// Приоритизация этапа
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> PrioritizeStage(int stageId, int machineId)
        {
            try
            {
                await _schedulerService.ReassignQueueAsync(machineId, stageId);
                return Json(new { success = true, message = "Этап успешно приоритизирован" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


        /// <summary>
        /// Переназначение этапа на другой станок
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ReassignStage(int stageId, int machineId)
        {
            try
            {
                await _schedulerService.ReassignStageToMachineAsync(stageId, machineId);
                return Json(new { success = true, message = "Этап успешно переназначен" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Получение доступных станков для этапа
        /// </summary>
        [HttpGet("{stageId}/available-machines")]
        public async Task<IActionResult> GetAvailableMachines(int stageId)
        {
            try
            {
                var machines = await _batchRepo.GetAvailableMachinesForStageAsync(stageId);

                var result = machines.Select(m => new {
                    id = m.Id,
                    name = m.Name,
                    inventoryNumber = m.InventoryNumber,
                    machineTypeName = m.MachineType?.Name,
                    priority = m.Priority,
                    isAvailable = true // Все возвращаемые станки доступны
                });

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }



        /// <summary>
        /// Автоматическое назначение станка для этапа
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AutoAssignStage(int stageId)
        {
            try
            {
                var result = await _schedulerService.AutoAssignMachineToStageAsync(stageId);
                if (result)
                {
                    return Json(new { success = true, message = "Станок автоматически назначен" });
                }
                else
                {
                    return Json(new { success = false, message = "Не удалось найти подходящий станок" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Запуск этапа в работу
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> StartStageExecution(int stageId)
        {
            try
            {
                // Проверяем, можно ли запустить этап
                var canStart = await _schedulerService.CanStartStageAsync(stageId);
                if (!canStart)
                {
                    // Пытаемся автоматически назначить станок, если он не назначен
                    var assigned = await _schedulerService.AutoAssignMachineToStageAsync(stageId);
                    if (!assigned)
                    {
                        return Json(new { success = false, message = "Невозможно назначить станок для этапа" });
                    }
                }

                // Запускаем этап
                await _stageService.StartStageExecution(stageId, "MANUAL_WEB", "WEB_INTERFACE");
                return Json(new { success = true, message = "Этап запущен в работу" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


        /// <summary>
        /// Получение статистики по станкам
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMachineStatistics()
        {
            try
            {
                var machines = await _machineService.GetAllAsync();
                var ganttData = await _stageService.GetAllStagesForGanttChart();

                var result = machines.Select(machine =>
                {
                    var machineStages = ganttData.Where(s => s.MachineId == machine.Id).ToList();
                    var currentStage = machineStages.FirstOrDefault(s => s.Status == "InProgress");

                    return new
                    {
                        id = machine.Id,
                        name = machine.Name,
                        inventoryNumber = machine.InventoryNumber,
                        machineTypeName = machine.MachineTypeName,
                        priority = machine.Priority,
                        status = currentStage != null ?
                            (currentStage.IsSetup ? "Переналадка" : "В работе") :
                            "Свободен",
                        currentStage = currentStage != null ? new
                        {
                            id = currentStage.Id,
                            stageName = currentStage.StageName,
                            detailName = currentStage.DetailName,
                            isSetup = currentStage.IsSetup,
                            startTime = currentStage.StartTime
                        } : null,
                        queuedStages = machineStages.Count(s => s.Status == "Waiting" || s.Status == "Pending")
                    };
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new List<object>());
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetSchedulerStatus()
        {
            try
            {
                var allStages = await _batchRepo.GetAllStageExecutionsAsync();

                var statistics = new
                {
                    totalStages = allStages.Count,
                    pendingStages = allStages.Count(s => s.Status == Domain.Entities.StageExecutionStatus.Pending),
                    inProgressStages = allStages.Count(s => s.Status == Domain.Entities.StageExecutionStatus.InProgress),
                    queuedStages = allStages.Count(s => s.Status == Domain.Entities.StageExecutionStatus.Waiting),
                    completedStages = allStages.Count(s => s.Status == Domain.Entities.StageExecutionStatus.Completed),
                    pausedStages = allStages.Count(s => s.Status == Domain.Entities.StageExecutionStatus.Paused),
                    errorStages = allStages.Count(s => s.Status == Domain.Entities.StageExecutionStatus.Error),
                    setupStages = allStages.Count(s => s.IsSetup),
                    overdueStages = allStages.Count(s => s.IsOverdue),
                    unassignedStages = allStages.Count(s => !s.MachineId.HasValue),

                    workingMachines = allStages
                        .Where(s => s.Status == Domain.Entities.StageExecutionStatus.InProgress && s.MachineId.HasValue)
                        .Select(s => s.MachineId.Value)
                        .Distinct()
                        .Count(),

                    lastUpdate = DateTime.UtcNow
                };

                return Json(new { success = true, data = statistics });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Batch Quick Actions

        /// <summary>
        /// Быстрое создание партии с автозапуском
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> QuickCreateBatch([FromBody] QuickBatchCreateDto dto)
        {
            try
            {
                // Создаем партию
                var batchDto = new BatchCreateDto
                {
                    DetailId = dto.DetailId,
                    Quantity = dto.Quantity,
                    SubBatches = dto.SubBatches?.Select(sb => new SubBatchCreateDto
                    {
                        Quantity = sb.Quantity
                    }).ToList() ?? new List<SubBatchCreateDto>()
                };

                var batchId = await _schedulerService.CreateBatchAsync(batchDto);

                // Если включен автозапуск, пытаемся запустить первые этапы
                if (dto.AutoStart)
                {
                    await _schedulerService.ScheduleSubBatchesAsync(batchId);

                    // Пытаемся автоматически запустить готовые этапы
                    var batch = await _batchService.GetByIdAsync(batchId);
                    foreach (var subBatch in batch.SubBatches)
                    {
                        var firstStage = subBatch.StageExecutions?.FirstOrDefault();
                        if (firstStage != null)
                        {
                            await _schedulerService.StartPendingStageAsync(firstStage.Id);
                        }
                    }
                }

                return Json(new
                {
                    success = true,
                    message = "Партия создана и запущена в производство",
                    batchId = batchId
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Получение краткой информации о партии
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetBatchSummary(int batchId)
        {
            try
            {
                var batch = await _batchService.GetByIdAsync(batchId);
                if (batch == null)
                    return NotFound();

                var stageExecutions = await _batchService.GetStageExecutionsForBatchAsync(batchId);

                var totalStages = stageExecutions.Count;
                var completedStages = stageExecutions.Count(se => se.Status == "Completed");
                var inProgressStages = stageExecutions.Count(se => se.Status == "InProgress");
                var pendingStages = stageExecutions.Count(se => se.Status == "Pending" || se.Status == "Waiting");

                return Json(new
                {
                    id = batch.Id,
                    detailName = batch.DetailName,
                    quantity = batch.Quantity,
                    created = batch.CreatedUtc,
                    totalStages = totalStages,
                    completedStages = completedStages,
                    inProgressStages = inProgressStages,
                    pendingStages = pendingStages,
                    completionPercent = totalStages > 0 ? Math.Round((double)completedStages / totalStages * 100, 1) : 0,
                    status = completedStages == totalStages && totalStages > 0 ? "Завершено" :
                             inProgressStages > 0 ? "В производстве" :
                             completedStages > 0 ? "Частично выполнено" : "Не начато"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        #endregion


    }
}