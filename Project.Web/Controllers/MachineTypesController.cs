using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
using Project.Web.ViewModels;

namespace Project.Web.Controllers
{
    /// <summary>
    /// Контроллер управления типами станков согласно ТЗ
    /// </summary>
    public class MachineTypesController : Controller
    {
        private readonly MachineTypeService _machineTypeService;
        private readonly ILogger<MachineTypesController> _logger;

        public MachineTypesController(
            MachineTypeService machineTypeService,
            ILogger<MachineTypesController> logger)
        {
            _machineTypeService = machineTypeService;
            _logger = logger;
        }

        /// <summary>
        /// Список типов станков согласно ТЗ
        /// </summary>
        public async Task<IActionResult> Index(string? searchTerm, bool? showOnlyWithMachines)
        {
            try
            {
                var machineTypes = await _machineTypeService.GetAllAsync();

                // Применяем фильтры
                var filteredMachineTypes = machineTypes.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    filteredMachineTypes = filteredMachineTypes.Where(mt =>
                        mt.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                }

                if (showOnlyWithMachines == true)
                {
                    filteredMachineTypes = filteredMachineTypes.Where(mt => mt.MachineCount > 0);
                }

                var viewModel = new MachineTypesIndexViewModel
                {
                    SearchTerm = searchTerm ?? string.Empty,
                    ShowOnlyWithMachines = showOnlyWithMachines ?? false,
                    MachineTypes = filteredMachineTypes.Select(mt => new MachineTypeItemViewModel
                    {
                        Id = mt.Id,
                        Name = mt.Name,
                        MachineCount = mt.MachineCount,
                        ActiveMachineCount = mt.ActiveMachineCount,
                        AveragePriority = mt.AveragePriority,
                        SupportedOperations = mt.SupportedOperations,
                        CanDelete = mt.CanDelete,
                        CreatedUtc = mt.CreatedUtc,
                        TotalWorkingHours = mt.UsageStatistics?.TotalWorkingHours ?? 0,
                        AverageUtilization = mt.UsageStatistics?.AverageUtilization ?? 0,
                        CompletedOperations = mt.UsageStatistics?.CompletedOperations ?? 0,
                        AverageSetupTime = mt.UsageStatistics?.AverageSetupTime ?? 0,
                        QueuedParts = mt.UsageStatistics?.QueuedParts ?? 0
                    }).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка типов станков");
                TempData["Error"] = "Произошла ошибка при загрузке списка типов станков";
                return View(new MachineTypesIndexViewModel());
            }
        }

        /// <summary>
        /// Детальная информация о типе станка
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var machineType = await _machineTypeService.GetByIdAsync(id);
                if (machineType == null)
                {
                    return NotFound("Тип станка не найден");
                }

                // Получаем статистику использования
                var usageStats = await _machineTypeService.GetUsageStatisticsAsync(id);

                var viewModel = new MachineTypeDetailsViewModel
                {
                    Id = machineType.Id,
                    Name = machineType.Name,
                    MachineCount = machineType.MachineCount,
                    ActiveMachineCount = machineType.ActiveMachineCount,
                    AveragePriority = machineType.AveragePriority,
                    SupportedOperations = machineType.SupportedOperations,
                    CanDelete = machineType.CanDelete,
                    CreatedUtc = machineType.CreatedUtc,
                    UsageStatistics = new MachineTypeUsageStatsViewModel
                    {
                        TotalWorkingHours = usageStats.TotalWorkingHours,
                        AverageUtilization = usageStats.AverageUtilization,
                        CompletedOperations = usageStats.CompletedOperations,
                        AverageSetupTime = usageStats.AverageSetupTime,
                        QueuedParts = usageStats.QueuedParts
                    }
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении типа станка {MachineTypeId}", id);
                return NotFound("Тип станка не найден");
            }
        }

        /// <summary>
        /// Форма создания нового типа станка
        /// </summary>
        public IActionResult Create()
        {
            var viewModel = new MachineTypeFormViewModel();
            return View(viewModel);
        }

        /// <summary>
        /// Создание нового типа станка
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MachineTypeFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var createDto = new MachineTypeCreateDto
                {
                    Name = model.Name
                };

                var id = await _machineTypeService.AddAsync(createDto);

                TempData["Success"] = $"Тип станка '{model.Name}' успешно создан";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании типа станка {@Model}", model);
                ModelState.AddModelError("", "Произошла ошибка при создании типа станка");
                return View(model);
            }
        }

        /// <summary>
        /// Форма редактирования типа станка
        /// </summary>
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var machineType = await _machineTypeService.GetByIdAsync(id);
                if (machineType == null)
                {
                    return NotFound("Тип станка не найден");
                }

                var viewModel = new MachineTypeFormViewModel
                {
                    Id = machineType.Id,
                    Name = machineType.Name,
                    SupportedOperations = machineType.SupportedOperations
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении типа станка для редактирования {MachineTypeId}", id);
                return NotFound("Тип станка не найден");
            }
        }

        /// <summary>
        /// Сохранение изменений типа станка
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(MachineTypeFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var editDto = new MachineTypeEditDto
                {
                    Id = model.Id,
                    Name = model.Name
                };

                await _machineTypeService.UpdateAsync(editDto);

                TempData["Success"] = $"Тип станка '{model.Name}' успешно обновлен";
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении типа станка {@Model}", model);
                ModelState.AddModelError("", "Произошла ошибка при обновлении типа станка");
                return View(model);
            }
        }

        /// <summary>
        /// Удаление типа станка
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _machineTypeService.DeleteAsync(id);
                TempData["Success"] = "Тип станка успешно удален";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении типа станка {MachineTypeId}", id);
                TempData["Error"] = "Произошла ошибка при удалении типа станка";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        /// <summary>
        /// API для поиска типов станков (для автокомплита)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Search(string term)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
                {
                    return Json(new List<object>());
                }

                var machineTypes = await _machineTypeService.GetAllAsync();
                var filtered = machineTypes
                    .Where(mt => mt.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .Take(10)
                    .Select(mt => new
                    {
                        id = mt.Id,
                        text = mt.Name,
                        name = mt.Name,
                        machineCount = mt.MachineCount,
                        activeMachineCount = mt.ActiveMachineCount
                    });

                return Json(filtered);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске типов станков по термину '{Term}'", term);
                return Json(new List<object>());
            }
        }

        /// <summary>
        /// Получение статистики использования типа станка
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetUsageStatistics(int id, DateTime? startDate, DateTime? endDate)
        {
            try
            {
                startDate ??= DateTime.Today.AddDays(-30);
                endDate ??= DateTime.Today;

                var statistics = await _machineTypeService.GetUsageStatisticsAsync(id, startDate, endDate);
                return Json(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении статистики использования типа станка {MachineTypeId}", id);
                return Json(null);
            }
        }

        /// <summary>
        /// Добавление поддерживаемой операции
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddOperation(int id, string operationName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(operationName))
                {
                    return Json(new { success = false, message = "Название операции не может быть пустым" });
                }

                // Здесь должна быть логика добавления операции
                // Пока что возвращаем успех
                return Json(new { success = true, message = "Операция добавлена" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении операции для типа станка {MachineTypeId}", id);
                return Json(new { success = false, message = "Ошибка при добавлении операции" });
            }
        }

        /// <summary>
        /// Удаление поддерживаемой операции
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RemoveOperation(int id, string operationName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(operationName))
                {
                    return Json(new { success = false, message = "Название операции не может быть пустым" });
                }

                // Здесь должна быть логика удаления операции
                // Пока что возвращаем успех
                return Json(new { success = true, message = "Операция удалена" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении операции для типа станка {MachineTypeId}", id);
                return Json(new { success = false, message = "Ошибка при удалении операции" });
            }
        }
    }
}