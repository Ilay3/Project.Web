using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
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

        public MainController(
            DetailService detailService,
            MachineTypeService machineTypeService,
            MachineService machineService,
            RouteService routeService,
            BatchService batchService,
            StageExecutionService stageService,
            ProductionSchedulerService schedulerService,
            SetupTimeService setupTimeService)
        {
            _detailService = detailService;
            _machineTypeService = machineTypeService;
            _machineService = machineService;
            _routeService = routeService;
            _batchService = batchService;
            _stageService = stageService;
            _schedulerService = schedulerService;
            _setupTimeService = setupTimeService;
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
                var batchId = await _schedulerService.CreateBatchAsync(dto);
                return Json(new { success = true, message = "Партия успешно создана", batchId = batchId });
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

        [HttpPost]
        public async Task<IActionResult> ReassignStage(int stageId, int machineId)
        {
            try
            {
                await _schedulerService.ReassignStageToMachineAsync(stageId, machineId);
                return Json(new { success = true, message = "Этап переназначен" });
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
    }

}