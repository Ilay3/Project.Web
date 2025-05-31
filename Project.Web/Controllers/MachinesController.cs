using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
using Project.Contracts.Enums;
using Project.Web.ViewModels;

namespace Project.Web.Controllers
{
    public class MachinesController : Controller
    {
        private readonly MachineService _machineService;
        private readonly MachineTypeService _machineTypeService;
        private readonly ILogger<MachinesController> _logger;

        public MachinesController(
            MachineService machineService,
            MachineTypeService machineTypeService,
            ILogger<MachinesController> logger)
        {
            _machineService = machineService;
            _machineTypeService = machineTypeService;
            _logger = logger;
        }

        /// <summary>
        /// Список станков согласно ТЗ
        /// </summary>
        public async Task<IActionResult> Index(int? machineTypeId, MachineStatus? status, string? searchTerm)
        {
            try
            {
                var machines = await _machineService.GetAllAsync();
                var machineTypes = await _machineTypeService.GetAllAsync();

                // Применяем фильтры
                var filteredMachines = machines.AsEnumerable();

                if (machineTypeId.HasValue)
                {
                    filteredMachines = filteredMachines.Where(m => m.MachineTypeId == machineTypeId.Value);
                }

                if (status.HasValue)
                {
                    filteredMachines = filteredMachines.Where(m => m.Status == status.Value);
                }

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    filteredMachines = filteredMachines.Where(m =>
                        m.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                        m.InventoryNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                }

                var viewModel = new MachinesIndexViewModel
                {
                    SearchTerm = searchTerm ?? string.Empty,
                    SelectedMachineTypeId = machineTypeId,
                    StatusFilter = status,
                    MachineTypes = machineTypes.Select(mt => new MachineTypeFilterViewModel
                    {
                        Id = mt.Id,
                        Name = mt.Name,
                        MachineCount = mt.MachineCount
                    }).ToList(),
                    Machines = filteredMachines.Select(m => new MachineItemViewModel
                    {
                        Id = m.Id,
                        Name = m.Name,
                        InventoryNumber = m.InventoryNumber,
                        MachineTypeName = m.MachineTypeName,
                        Priority = m.Priority,
                        Status = m.Status,
                        CurrentStageDescription = m.CurrentStageDescription,
                        TimeToFree = m.TimeToFree,
                        QueueLength = m.QueueLength,
                        TodayUtilizationPercent = m.TodayUtilizationPercent
                    }).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка станков");
                TempData["Error"] = "Произошла ошибка при загрузке списка станков";
                return View(new MachinesIndexViewModel());
            }
        }

        /// <summary>
        /// Детальная информация о станке
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var machine = await _machineService.GetByIdAsync(id);
                if (machine == null)
                {
                    return NotFound("Станок не найден");
                }

                // Получаем статистику загрузки станка
                var utilization = await _machineService.GetMachineUtilizationAsync(id);

                // Получаем календарный отчет за последнюю неделю
                var calendarReport = await _machineService.GetMachineCalendarReportAsync(
                    id, DateTime.Today.AddDays(-7), DateTime.Today);

                ViewBag.Utilization = utilization;
                ViewBag.CalendarReport = calendarReport;

                return View(machine);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении станка {MachineId}", id);
                return NotFound("Станок не найден");
            }
        }

        /// <summary>
        /// Форма создания нового станка
        /// </summary>
        public async Task<IActionResult> Create()
        {
            try
            {
                var machineTypes = await _machineTypeService.GetAllAsync();

                var viewModel = new MachineFormViewModel
                {
                    AvailableMachineTypes = machineTypes.Select(mt => new MachineTypeOption
                    {
                        Id = mt.Id,
                        Name = mt.Name
                    }).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке формы создания станка");
                return View(new MachineFormViewModel());
            }
        }

        /// <summary>
        /// Создание нового станка
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MachineFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadMachineTypes(model);
                return View(model);
            }

            try
            {
                var createDto = new MachineCreateDto
                {
                    Name = model.Name,
                    InventoryNumber = model.InventoryNumber,
                    MachineTypeId = model.MachineTypeId,
                    Priority = model.Priority
                };

                var id = await _machineService.CreateAsync(createDto);

                TempData["Success"] = $"Станок '{model.Name}' успешно создан";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError("", ex.Message);
                await LoadMachineTypes(model);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании станка {@Model}", model);
                ModelState.AddModelError("", "Произошла ошибка при создании станка");
                await LoadMachineTypes(model);
                return View(model);
            }
        }

        /// <summary>
        /// Форма редактирования станка
        /// </summary>
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var machine = await _machineService.GetByIdAsync(id);
                if (machine == null)
                {
                    return NotFound("Станок не найден");
                }

                var viewModel = new MachineFormViewModel
                {
                    Id = machine.Id,
                    Name = machine.Name,
                    InventoryNumber = machine.InventoryNumber,
                    MachineTypeId = machine.MachineTypeId,
                    Priority = machine.Priority
                };

                await LoadMachineTypes(viewModel);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении станка для редактирования {MachineId}", id);
                return NotFound("Станок не найден");
            }
        }

        /// <summary>
        /// Сохранение изменений станка
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(MachineFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadMachineTypes(model);
                return View(model);
            }

            try
            {
                var editDto = new MachineEditDto
                {
                    Id = model.Id,
                    Name = model.Name,
                    InventoryNumber = model.InventoryNumber,
                    MachineTypeId = model.MachineTypeId,
                    Priority = model.Priority
                };

                await _machineService.UpdateAsync(editDto);

                TempData["Success"] = $"Станок '{model.Name}' успешно обновлен";
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError("", ex.Message);
                await LoadMachineTypes(model);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении станка {@Model}", model);
                ModelState.AddModelError("", "Произошла ошибка при обновлении станка");
                await LoadMachineTypes(model);
                return View(model);
            }
        }

        /// <summary>
        /// Удаление станка
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _machineService.DeleteAsync(id);
                TempData["Success"] = "Станок успешно удален";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении станка {MachineId}", id);
                TempData["Error"] = "Произошла ошибка при удалении станка";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        /// <summary>
        /// API для получения доступных станков по типу
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAvailableByType(int machineTypeId)
        {
            try
            {
                var machines = await _machineService.GetAvailableMachinesAsync(machineTypeId);
                var result = machines.Select(m => new
                {
                    id = m.Id,
                    text = $"{m.Name} ({m.InventoryNumber})",
                    name = m.Name,
                    inventoryNumber = m.InventoryNumber,
                    priority = m.Priority,
                    status = m.Status.ToString(),
                    queueLength = m.QueueLength
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении доступных станков типа {MachineTypeId}", machineTypeId);
                return Json(new List<object>());
            }
        }

        /// <summary>
        /// Получение статистики загрузки станка
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetUtilizationStats(int id, DateTime? startDate, DateTime? endDate)
        {
            try
            {
                startDate ??= DateTime.Today.AddDays(-7);
                endDate ??= DateTime.Today;

                var utilization = await _machineService.GetMachineUtilizationAsync(id, startDate, endDate);
                return Json(utilization);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении статистики загрузки станка {MachineId}", id);
                return Json(null);
            }
        }

        #region Вспомогательные методы

        private async Task LoadMachineTypes(MachineFormViewModel model)
        {
            try
            {
                var machineTypes = await _machineTypeService.GetAllAsync();
                model.AvailableMachineTypes = machineTypes.Select(mt => new MachineTypeOption
                {
                    Id = mt.Id,
                    Name = mt.Name
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке типов станков");
                model.AvailableMachineTypes = new List<MachineTypeOption>();
            }
        }

        #endregion
    }
}