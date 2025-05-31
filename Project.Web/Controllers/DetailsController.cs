using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
using Project.Web.ViewModels;

namespace Project.Web.Controllers
{
    public class DetailsController : Controller
    {
        private readonly DetailService _detailService;
        private readonly RouteService _routeService;
        private readonly ILogger<DetailsController> _logger;

        public DetailsController(
            DetailService detailService,
            RouteService routeService,
            ILogger<DetailsController> logger)
        {
            _detailService = detailService;
            _routeService = routeService;
            _logger = logger;
        }

        /// <summary>
        /// Список всех деталей согласно ТЗ
        /// </summary>
        public async Task<IActionResult> Index(string? searchTerm, bool? showOnlyWithRoutes, bool? showOnlyWithoutRoutes)
        {
            try
            {
                var details = await _detailService.GetAllAsync();

                // Применяем фильтры
                var filteredDetails = details.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    filteredDetails = filteredDetails.Where(d =>
                        d.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                        d.Number.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                }

                if (showOnlyWithRoutes == true)
                {
                    filteredDetails = filteredDetails.Where(d => d.HasRoute);
                }

                if (showOnlyWithoutRoutes == true)
                {
                    filteredDetails = filteredDetails.Where(d => !d.HasRoute);
                }

                var viewModel = new DetailsIndexViewModel
                {
                    SearchTerm = searchTerm ?? string.Empty,
                    ShowOnlyWithRoutes = showOnlyWithRoutes ?? false,
                    ShowOnlyWithoutRoutes = showOnlyWithoutRoutes ?? false,
                    Details = filteredDetails.Select(d => new DetailItemViewModel
                    {
                        Id = d.Id,
                        Number = d.Number,
                        Name = d.Name,
                        HasRoute = d.HasRoute,
                        RouteStageCount = d.RouteStageCount,
                        TotalManufacturingTimeHours = d.TotalManufacturingTimeHours,
                        TotalManufactured = d.UsageStatistics?.TotalManufactured ?? 0,
                        ActiveBatches = d.UsageStatistics?.ActiveBatches ?? 0,
                        LastManufacturedDate = d.UsageStatistics?.LastManufacturedDate,
                        CanDelete = d.CanDelete
                    }).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка деталей");
                TempData["Error"] = "Произошла ошибка при загрузке списка деталей";
                return View(new DetailsIndexViewModel());
            }
        }

        /// <summary>
        /// Детальная информация о детали
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var detail = await _detailService.GetByIdAsync(id);
                if (detail == null)
                {
                    return NotFound("Деталь не найдена");
                }

                // Получаем статистику производства
                var productionStats = await _detailService.GetProductionStatisticsAsync(id);

                var viewModel = new DetailDetailsViewModel
                {
                    Id = detail.Id,
                    Number = detail.Number,
                    Name = detail.Name,
                    HasRoute = detail.HasRoute,
                    TotalManufacturingTimeHours = detail.TotalManufacturingTimeHours,
                    RouteStageCount = detail.RouteStageCount,
                    CanDelete = detail.CanDelete,
                    TotalManufactured = detail.UsageStatistics?.TotalManufactured ?? 0,
                    ActiveBatches = detail.UsageStatistics?.ActiveBatches ?? 0,
                    LastManufacturedDate = detail.UsageStatistics?.LastManufacturedDate,
                    AverageManufacturingTime = detail.UsageStatistics?.AverageManufacturingTime
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении детали {DetailId}", id);
                return NotFound("Деталь не найдена");
            }
        }

        /// <summary>
        /// Форма создания новой детали
        /// </summary>
        public IActionResult Create()
        {
            var viewModel = new DetailFormViewModel();
            return View(viewModel);
        }

        /// <summary>
        /// Создание новой детали
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DetailFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var createDto = new DetailCreateDto
                {
                    Number = model.Number,
                    Name = model.Name,
                    Description = model.Description,
                    Material = model.Material,
                    Unit = model.Unit,
                    Weight = model.Weight,
                    Category = model.Category
                };

                var id = await _detailService.CreateAsync(createDto);

                TempData["Success"] = $"Деталь '{model.Name}' успешно создана";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании детали {@Model}", model);
                ModelState.AddModelError("", "Произошла ошибка при создании детали");
                return View(model);
            }
        }

        /// <summary>
        /// Форма редактирования детали
        /// </summary>
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var detail = await _detailService.GetByIdAsync(id);
                if (detail == null)
                {
                    return NotFound("Деталь не найдена");
                }

                var viewModel = new DetailFormViewModel
                {
                    Id = detail.Id,
                    Number = detail.Number,
                    Name = detail.Name,
                    Description = detail.Description,
                    Material = detail.Material,
                    Unit = detail.Unit,
                    Weight = detail.Weight,
                    Category = detail.Category
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении детали для редактирования {DetailId}", id);
                return NotFound("Деталь не найдена");
            }
        }

        /// <summary>
        /// Сохранение изменений детали
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(DetailFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var editDto = new DetailEditDto
                {
                    Id = model.Id,
                    Number = model.Number,
                    Name = model.Name,
                    Description = model.Description,
                    Material = model.Material,
                    Unit = model.Unit,
                    Weight = model.Weight,
                    Category = model.Category
                };

                await _detailService.UpdateAsync(editDto);

                TempData["Success"] = $"Деталь '{model.Name}' успешно обновлена";
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении детали {@Model}", model);
                ModelState.AddModelError("", "Произошла ошибка при обновлении детали");
                return View(model);
            }
        }

        /// <summary>
        /// Удаление детали
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _detailService.DeleteAsync(id);
                TempData["Success"] = "Деталь успешно удалена";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении детали {DetailId}", id);
                TempData["Error"] = "Произошла ошибка при удалении детали";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        /// <summary>
        /// API для поиска деталей (для автокомплита)
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

                var details = await _detailService.GetAllAsync();
                var filtered = details
                    .Where(d => d.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                               d.Number.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .Take(10)
                    .Select(d => new
                    {
                        id = d.Id,
                        text = $"{d.Number} - {d.Name}",
                        number = d.Number,
                        name = d.Name,
                        hasRoute = d.HasRoute
                    });

                return Json(filtered);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске деталей по термину '{Term}'", term);
                return Json(new List<object>());
            }
        }

        /// <summary>
        /// Получение деталей для создания партий
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetDetailsForBatch()
        {
            try
            {
                var details = await _detailService.GetDetailsForBatchCreationAsync();
                var result = details.Select(d => new
                {
                    id = d.Id,
                    text = $"{d.Number} - {d.Name}",
                    number = d.Number,
                    name = d.Name,
                    hasRoute = d.HasRoute,
                    stagesCount = d.RouteStagesCount,
                    averageQuantity = d.AverageQuantity,
                    estimatedDuration = d.EstimatedDuration?.TotalHours
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении деталей для создания партий");
                return Json(new List<object>());
            }
        }
    }
}