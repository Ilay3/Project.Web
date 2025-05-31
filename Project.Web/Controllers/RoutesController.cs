using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
using Project.Web.ViewModels;

namespace Project.Web.Controllers
{
    public class RoutesController : Controller
    {
        private readonly RouteService _routeService;
        private readonly DetailService _detailService;
        private readonly MachineTypeService _machineTypeService;
        private readonly ILogger<RoutesController> _logger;

        public RoutesController(
            RouteService routeService,
            DetailService detailService,
            MachineTypeService machineTypeService,
            ILogger<RoutesController> logger)
        {
            _routeService = routeService;
            _detailService = detailService;
            _machineTypeService = machineTypeService;
            _logger = logger;
        }

        /// <summary>
        /// Список маршрутов согласно ТЗ
        /// </summary>
        public async Task<IActionResult> Index(string? searchTerm, bool? showOnlyEditable)
        {
            try
            {
                var routes = await _routeService.GetAllAsync();

                // Применяем фильтры
                var filteredRoutes = routes.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    filteredRoutes = filteredRoutes.Where(r =>
                        r.DetailName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                        r.DetailNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                }

                if (showOnlyEditable == true)
                {
                    filteredRoutes = filteredRoutes.Where(r => r.CanEdit);
                }

                var viewModel = new RoutesIndexViewModel
                {
                    SearchTerm = searchTerm ?? string.Empty,
                    ShowOnlyEditable = showOnlyEditable ?? false,
                    Routes = filteredRoutes.Select(r => new RouteItemViewModel
                    {
                        Id = r.Id,
                        DetailName = r.DetailName,
                        DetailNumber = r.DetailNumber,
                        StageCount = r.StageCount,
                        TotalNormTimeHours = r.TotalNormTimeHours,
                        TotalSetupTimeHours = r.TotalSetupTimeHours,
                        CanEdit = r.CanEdit,
                        CanDelete = r.CanDelete,
                        LastModifiedUtc = r.LastModifiedUtc
                    }).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка маршрутов");
                TempData["Error"] = "Произошла ошибка при загрузке списка маршрутов";
                return View(new RoutesIndexViewModel());
            }
        }

        /// <summary>
        /// Детальная информация о маршруте
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var route = await _routeService.GetByIdAsync(id);
                if (route == null)
                {
                    return NotFound("Маршрут не найден");
                }

                var viewModel = new RouteDetailsViewModel
                {
                    Id = route.Id,
                    DetailName = route.DetailName,
                    DetailNumber = route.DetailNumber,
                    StageCount = route.StageCount,
                    TotalNormTimeHours = route.TotalNormTimeHours,
                    TotalSetupTimeHours = route.TotalSetupTimeHours,
                    CanEdit = route.CanEdit,
                    CanDelete = route.CanDelete,
                    LastModifiedUtc = route.LastModifiedUtc,
                    Stages = route.Stages.Select(s => new RouteStageDetailsViewModel
                    {
                        Id = s.Id,
                        Order = s.Order,
                        Name = s.Name,
                        MachineTypeName = s.MachineTypeName,
                        NormTime = s.NormTime,
                        SetupTime = s.SetupTime,
                        Description = s.Description,
                        RequiredSkills = s.RequiredSkills,
                        RequiredTools = s.RequiredTools,
                        QualityParameters = s.QualityParameters
                    }).ToList(),
                    UsageStatistics = new RouteUsageStatisticsViewModel()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении маршрута {RouteId}", id);
                return NotFound("Маршрут не найден");
            }
        }

        /// <summary>
        /// Форма создания нового маршрута
        /// </summary>
        public async Task<IActionResult> Create()
        {
            try
            {
                var viewModel = new RouteFormViewModel();
                await LoadFormData(viewModel);

                // Добавляем один пустой этап по умолчанию
                viewModel.Stages.Add(new RouteStageFormViewModel
                {
                    Order = 1,
                    NormTime = 1.0,
                    SetupTime = 0.5
                });

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке формы создания маршрута");
                return View(new RouteFormViewModel());
            }
        }

        /// <summary>
        /// Создание нового маршрута
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RouteFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadFormData(model);
                return View(model);
            }

            try
            {
                var createDto = new RouteCreateDto
                {
                    DetailId = model.DetailId,
                    Stages = model.Stages
                        .Where(s => !s.IsDeleted)
                        .Select(s => new RouteStageCreateDto
                        {
                            Order = s.Order,
                            Name = s.Name,
                            MachineTypeId = s.MachineTypeId,
                            NormTime = s.NormTime,
                            SetupTime = s.SetupTime,
                            Description = s.Description,
                            RequiredSkills = s.RequiredSkills,
                            RequiredTools = s.RequiredTools,
                            QualityParameters = s.QualityParameters,
                            StageType = s.StageType
                        }).ToList()
                };

                var id = await _routeService.AddAsync(createDto);

                TempData["Success"] = "Маршрут успешно создан";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError("", ex.Message);
                await LoadFormData(model);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании маршрута {@Model}", model);
                ModelState.AddModelError("", "Произошла ошибка при создании маршрута");
                await LoadFormData(model);
                return View(model);
            }
        }

        /// <summary>
        /// Форма редактирования маршрута
        /// </summary>
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var route = await _routeService.GetByIdAsync(id);
                if (route == null)
                {
                    return NotFound("Маршрут не найден");
                }

                if (!route.CanEdit)
                {
                    TempData["Error"] = "Маршрут нельзя редактировать - по нему выполняются производственные задания";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var viewModel = new RouteFormViewModel
                {
                    Id = route.Id,
                    DetailId = route.DetailId,
                    DetailName = route.DetailName,
                    CanEdit = route.CanEdit,
                    Stages = route.Stages.Select(s => new RouteStageFormViewModel
                    {
                        Id = s.Id,
                        Order = s.Order,
                        Name = s.Name,
                        MachineTypeId = s.MachineTypeId,
                        MachineTypeName = s.MachineTypeName,
                        NormTime = s.NormTime,
                        SetupTime = s.SetupTime,
                        Description = s.Description,
                        RequiredSkills = s.RequiredSkills,
                        RequiredTools = s.RequiredTools,
                        QualityParameters = s.QualityParameters,
                        StageType = s.StageType
                    }).ToList()
                };

                await LoadFormData(viewModel);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении маршрута для редактирования {RouteId}", id);
                return NotFound("Маршрут не найден");
            }
        }

        /// <summary>
        /// Сохранение изменений маршрута
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(RouteFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadFormData(model);
                return View(model);
            }

            try
            {
                var editDto = new RouteEditDto
                {
                    Id = model.Id,
                    DetailId = model.DetailId,
                    Stages = model.Stages
                        .Where(s => !s.IsDeleted)
                        .Select(s => new RouteStageEditDto
                        {
                            Id = s.Id,
                            Order = s.Order,
                            Name = s.Name,
                            MachineTypeId = s.MachineTypeId,
                            NormTime = s.NormTime,
                            SetupTime = s.SetupTime,
                            Description = s.Description,
                            RequiredSkills = s.RequiredSkills,
                            RequiredTools = s.RequiredTools,
                            QualityParameters = s.QualityParameters,
                            StageType = s.StageType
                        }).ToList()
                };

                await _routeService.UpdateAsync(editDto);

                TempData["Success"] = "Маршрут успешно обновлен";
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError("", ex.Message);
                await LoadFormData(model);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении маршрута {@Model}", model);
                ModelState.AddModelError("", "Произошла ошибка при обновлении маршрута");
                await LoadFormData(model);
                return View(model);
            }
        }

        /// <summary>
        /// Удаление маршрута
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _routeService.DeleteAsync(id);
                TempData["Success"] = "Маршрут успешно удален";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении маршрута {RouteId}", id);
                TempData["Error"] = "Произошла ошибка при удалении маршрута";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        /// <summary>
        /// Форма копирования маршрута
        /// </summary>
        public async Task<IActionResult> Copy(int id)
        {
            try
            {
                var sourceRoute = await _routeService.GetByIdAsync(id);
                if (sourceRoute == null)
                {
                    return NotFound("Исходный маршрут не найден");
                }

                // Получаем детали без маршрутов
                var allDetails = await _detailService.GetAllAsync();
                var availableDetails = allDetails
                    .Where(d => !d.HasRoute)
                    .Select(d => new DetailOptionViewModel
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Number = d.Number,
                        HasRoute = d.HasRoute
                    }).ToList();

                var viewModel = new RouteCopyViewModel
                {
                    SourceRouteId = id,
                    SourceRouteName = $"{sourceRoute.DetailNumber} - {sourceRoute.DetailName}",
                    AvailableTargetDetails = availableDetails
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при подготовке копирования маршрута {RouteId}", id);
                return NotFound("Маршрут не найден");
            }
        }

        /// <summary>
        /// Копирование маршрута
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Copy(RouteCopyViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadCopyFormData(model);
                return View(model);
            }

            try
            {
                var copyDto = new RouteCopyDto
                {
                    SourceRouteId = model.SourceRouteId,
                    TargetDetailId = model.TargetDetailId,
                    CopySetupTimes = model.CopySetupTimes
                };

                var newRouteId = await _routeService.CopyRouteAsync(copyDto);

                TempData["Success"] = "Маршрут успешно скопирован";
                return RedirectToAction(nameof(Details), new { id = newRouteId });
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError("", ex.Message);
                await LoadCopyFormData(model);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при копировании маршрута {@Model}", model);
                ModelState.AddModelError("", "Произошла ошибка при копировании маршрута");
                await LoadCopyFormData(model);
                return View(model);
            }
        }

        /// <summary>
        /// API для добавления этапа в форму
        /// </summary>
        [HttpPost]
        public IActionResult AddStage(int order)
        {
            var stage = new RouteStageFormViewModel
            {
                Order = order,
                NormTime = 1.0,
                SetupTime = 0.5
            };

            return PartialView("_RouteStageFormPartial", stage);
        }

        #region Вспомогательные методы

        private async Task LoadFormData(RouteFormViewModel model)
        {
            try
            {
                // Загружаем детали
                var details = await _detailService.GetAllAsync();
                model.AvailableDetails = details.Select(d => new DetailOptionViewModel
                {
                    Id = d.Id,
                    Name = d.Name,
                    Number = d.Number,
                    HasRoute = d.HasRoute
                }).ToList();

                // Загружаем типы станков
                var machineTypes = await _machineTypeService.GetAllAsync();
                model.AvailableMachineTypes = machineTypes.Select(mt => new MachineTypeOptionViewModel
                {
                    Id = mt.Id,
                    Name = mt.Name,
                    MachineCount = mt.MachineCount
                }).ToList();

                // Заполняем названия типов станков в этапах
                foreach (var stage in model.Stages)
                {
                    var machineType = machineTypes.FirstOrDefault(mt => mt.Id == stage.MachineTypeId);
                    stage.MachineTypeName = machineType?.Name ?? "";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке данных формы маршрута");
                model.AvailableDetails = new List<DetailOptionViewModel>();
                model.AvailableMachineTypes = new List<MachineTypeOptionViewModel>();
            }
        }

        private async Task LoadCopyFormData(RouteCopyViewModel model)
        {
            try
            {
                var allDetails = await _detailService.GetAllAsync();
                model.AvailableTargetDetails = allDetails
                    .Where(d => !d.HasRoute)
                    .Select(d => new DetailOptionViewModel
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Number = d.Number,
                        HasRoute = d.HasRoute
                    }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке данных для копирования маршрута");
                model.AvailableTargetDetails = new List<DetailOptionViewModel>();
            }
        }

        #endregion
    }
}