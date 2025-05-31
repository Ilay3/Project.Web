using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
using Project.Web.ViewModels;

namespace Project.Web.Controllers
{
    /// <summary>
    /// Контроллер управления временами переналадки согласно ТЗ
    /// </summary>
    public class SetupTimesController : Controller
    {
        private readonly SetupTimeService _setupTimeService;
        private readonly MachineService _machineService;
        private readonly DetailService _detailService;
        private readonly ILogger<SetupTimesController> _logger;

        public SetupTimesController(
            SetupTimeService setupTimeService,
            MachineService machineService,
            DetailService detailService,
            ILogger<SetupTimesController> logger)
        {
            _setupTimeService = setupTimeService;
            _machineService = machineService;
            _detailService = detailService;
            _logger = logger;
        }

        /// <summary>
        /// Список времен переналадки согласно ТЗ
        /// </summary>
        public async Task<IActionResult> Index(SetupTimeFilterViewModel? filter, int page = 1, int pageSize = 20)
        {
            try
            {
                filter ??= new SetupTimeFilterViewModel();

                var setupTimes = await _setupTimeService.GetAllAsync();

                // Применяем фильтры
                var filteredSetupTimes = setupTimes.AsEnumerable();

                if (filter.MachineId.HasValue)
                {
                    filteredSetupTimes = filteredSetupTimes.Where(st => st.MachineId == filter.MachineId.Value);
                }

                if (filter.FromDetailId.HasValue)
                {
                    filteredSetupTimes = filteredSetupTimes.Where(st => st.FromDetailId == filter.FromDetailId.Value);
                }

                if (filter.ToDetailId.HasValue)
                {
                    filteredSetupTimes = filteredSetupTimes.Where(st => st.ToDetailId == filter.ToDetailId.Value);
                }

                if (filter.MinTime.HasValue)
                {
                    filteredSetupTimes = filteredSetupTimes.Where(st => st.Time >= filter.MinTime.Value);
                }

                if (filter.MaxTime.HasValue)
                {
                    filteredSetupTimes = filteredSetupTimes.Where(st => st.Time <= filter.MaxTime.Value);
                }

                if (filter.ShowOnlyUsed)
                {
                    filteredSetupTimes = filteredSetupTimes.Where(st => st.UsageCount > 0);
                }

                // Пагинация
                var totalItems = filteredSetupTimes.Count();
                var paginatedSetupTimes = filteredSetupTimes
                    .OrderBy(st => st.MachineName)
                    .ThenBy(st => st.FromDetailName)
                    .ThenBy(st => st.ToDetailName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Загружаем данные для фильтров
                await LoadFilterOptions(filter);

                var viewModel = new SetupTimesIndexViewModel
                {
                    Filter = filter,
                    Pagination = new PaginationViewModel
                    {
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalItems = totalItems
                    },
                    SetupTimes = paginatedSetupTimes.Select(st => new SetupTimeItemViewModel
                    {
                        Id = st.Id,
                        MachineName = st.MachineName,
                        FromDetailName = st.FromDetailName,
                        FromDetailNumber = st.FromDetailNumber,
                        ToDetailName = st.ToDetailName,
                        ToDetailNumber = st.ToDetailNumber,
                        Time = st.Time,
                        LastUsedUtc = st.LastUsedUtc,
                        UsageCount = st.UsageCount,
                        AverageActualTime = st.AverageActualTime
                    }).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка времен переналадки");
                TempData["Error"] = "Произошла ошибка при загрузке списка времен переналадки";
                return View(new SetupTimesIndexViewModel());
            }
        }

        /// <summary>
        /// Форма создания нового времени переналадки
        /// </summary>
        public async Task<IActionResult> Create()
        {
            try
            {
                var viewModel = new SetupTimeFormViewModel();
                await LoadFormData(viewModel);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке формы создания времени переналадки");
                return View(new SetupTimeFormViewModel());
            }
        }

        /// <summary>
        /// Создание нового времени переналадки
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SetupTimeFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadFormData(model);
                return View(model);
            }

            try
            {
                var createDto = new SetupTimeCreateDto
                {
                    MachineId = model.MachineId,
                    FromDetailId = model.FromDetailId,
                    ToDetailId = model.ToDetailId,
                    Time = model.Time,
                    SetupDescription = model.SetupDescription,
                    RequiredSkills = model.RequiredSkills,
                    RequiredTools = model.RequiredTools
                };

                var id = await _setupTimeService.AddAsync(createDto);

                TempData["Success"] = "Время переналадки успешно создано";
                return RedirectToAction(nameof(Index));
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError("", ex.Message);
                await LoadFormData(model);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании времени переналадки {@Model}", model);
                ModelState.AddModelError("", "Произошла ошибка при создании времени переналадки");
                await LoadFormData(model);
                return View(model);
            }
        }

        /// <summary>
        /// Форма редактирования времени переналадки
        /// </summary>
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var setupTime = await _setupTimeService.GetByIdAsync(id);
                if (setupTime == null)
                {
                    return NotFound("Время переналадки не найдено");
                }

                var viewModel = new SetupTimeFormViewModel
                {
                    Id = setupTime.Id,
                    MachineId = setupTime.MachineId,
                    FromDetailId = setupTime.FromDetailId,
                    ToDetailId = setupTime.ToDetailId,
                    Time = setupTime.Time
                };

                await LoadFormData(viewModel);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении времени переналадки для редактирования {SetupTimeId}", id);
                return NotFound("Время переналадки не найдено");
            }
        }

        /// <summary>
        /// Сохранение изменений времени переналадки
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SetupTimeFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadFormData(model);
                return View(model);
            }

            try
            {
                var editDto = new SetupTimeEditDto
                {
                    Id = model.Id,
                    MachineId = model.MachineId,
                    FromDetailId = model.FromDetailId,
                    ToDetailId = model.ToDetailId,
                    Time = model.Time,
                    SetupDescription = model.SetupDescription,
                    RequiredSkills = model.RequiredSkills,
                    RequiredTools = model.RequiredTools
                };

                await _setupTimeService.UpdateAsync(editDto);

                TempData["Success"] = "Время переналадки успешно обновлено";
                return RedirectToAction(nameof(Index));
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError("", ex.Message);
                await LoadFormData(model);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении времени переналадки {@Model}", model);
                ModelState.AddModelError("", "Произошла ошибка при обновлении времени переналадки");
                await LoadFormData(model);
                return View(model);
            }
        }

        /// <summary>
        /// Удаление времени переналадки
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _setupTimeService.DeleteAsync(id);
                TempData["Success"] = "Время переналадки успешно удалено";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении времени переналадки {SetupTimeId}", id);
                TempData["Error"] = "Произошла ошибка при удалении времени переналадки";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Проверка необходимости переналадки
        /// </summary>
        public async Task<IActionResult> CheckSetup()
        {
            try
            {
                var viewModel = new SetupCheckViewModel();
                await LoadSetupCheckData(viewModel);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке формы проверки переналадки");
                return View(new SetupCheckViewModel());
            }
        }

        /// <summary>
        /// Выполнение проверки необходимости переналадки
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckSetup(SetupCheckViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadSetupCheckData(model);
                return View(model);
            }

            try
            {
                var setupInfo = await _setupTimeService.CheckSetupNeededAsync(model.MachineId, model.DetailId);

                model.Result = new SetupInfoResultViewModel
                {
                    SetupNeeded = setupInfo.SetupNeeded,
                    FromDetailName = setupInfo.FromDetailName,
                    FromDetailNumber = setupInfo.FromDetailNumber,
                    ToDetailName = setupInfo.ToDetailName ?? "",
                    ToDetailNumber = setupInfo.ToDetailNumber ?? "",
                    SetupTime = setupInfo.SetupTime
                };

                await LoadSetupCheckData(model);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке необходимости переналадки");
                ModelState.AddModelError("", "Произошла ошибка при проверке переналадки");
                await LoadSetupCheckData(model);
                return View(model);
            }
        }

        /// <summary>
        /// Массовый импорт времен переналадки
        /// </summary>
        public IActionResult BulkImport()
        {
            var viewModel = new SetupTimeBulkImportViewModel();
            return View(viewModel);
        }

        /// <summary>
        /// Выполнение массового импорта
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkImport(SetupTimeBulkImportViewModel model)
        {
            try
            {
                string csvData = model.CsvData ?? "";

                // Если загружен файл, читаем его содержимое
                if (model.CsvFile != null && model.CsvFile.Length > 0)
                {
                    using var reader = new StreamReader(model.CsvFile.OpenReadStream());
                    csvData = await reader.ReadToEndAsync();
                }

                if (string.IsNullOrWhiteSpace(csvData))
                {
                    ModelState.AddModelError("", "Необходимо предоставить CSV данные или загрузить файл");
                    return View(model);
                }

                // Парсим CSV и создаем DTO для импорта
                var setupTimes = ParseCsvData(csvData);

                var importDto = new BulkSetupTimeImportDto
                {
                    SetupTimes = setupTimes,
                    OverwriteExisting = model.OverwriteExisting
                };

                var result = await _setupTimeService.BulkAddSetupTimesAsync(importDto);

                model.ImportResult = new SetupTimeBulkImportResultViewModel
                {
                    SuccessCount = result.SuccessCount,
                    FailureCount = result.FailureCount,
                    SkippedCount = result.SkippedCount,
                    Errors = result.Errors,
                    Warnings = result.Warnings
                };

                if (result.SuccessCount > 0)
                {
                    TempData["Success"] = $"Успешно импортировано {result.SuccessCount} записей";
                }

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при массовом импорте времен переналадки");
                ModelState.AddModelError("", "Произошла ошибка при импорте");
                return View(model);
            }
        }

        /// <summary>
        /// API для получения времен переналадки для станка
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSetupTimesForMachine(int machineId)
        {
            try
            {
                var setupTimes = await _setupTimeService.GetSetupTimesForMachineAsync(machineId);
                var result = setupTimes.Select(st => new
                {
                    id = st.Id,
                    fromDetailName = st.FromDetailName,
                    toDetailName = st.ToDetailName,
                    time = st.Time,
                    usageCount = st.UsageCount
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении времен переналадки для станка {MachineId}", machineId);
                return Json(new List<object>());
            }
        }

        /// <summary>
        /// API для получения времен переналадки для детали
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSetupTimesForDetail(int detailId)
        {
            try
            {
                var setupTimes = await _setupTimeService.GetSetupTimesForDetailAsync(detailId);
                var result = setupTimes.Select(st => new
                {
                    id = st.Id,
                    machineName = st.MachineName,
                    fromDetailName = st.FromDetailName,
                    toDetailName = st.ToDetailName,
                    time = st.Time
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении времен переналадки для детали {DetailId}", detailId);
                return Json(new List<object>());
            }
        }

        #region Вспомогательные методы

        private async Task LoadFormData(SetupTimeFormViewModel model)
        {
            try
            {
                var machines = await _machineService.GetAllAsync();
                model.AvailableMachines = machines.Select(m => new SelectOptionViewModel
                {
                    Id = m.Id,
                    Name = $"{m.Name} ({m.InventoryNumber})"
                }).ToList();

                var details = await _detailService.GetAllAsync();
                model.AvailableDetails = details.Select(d => new SelectOptionViewModel
                {
                    Id = d.Id,
                    Name = $"{d.Number} - {d.Name}"
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке данных формы времени переналадки");
                model.AvailableMachines = new List<SelectOptionViewModel>();
                model.AvailableDetails = new List<SelectOptionViewModel>();
            }
        }

        private async Task LoadFilterOptions(SetupTimeFilterViewModel filter)
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке опций фильтра");
                filter.AvailableMachines = new List<SelectOptionViewModel>();
                filter.AvailableDetails = new List<SelectOptionViewModel>();
            }
        }

        private async Task LoadSetupCheckData(SetupCheckViewModel model)
        {
            try
            {
                var machines = await _machineService.GetAllAsync();
                model.AvailableMachines = machines.Select(m => new SelectOptionViewModel
                {
                    Id = m.Id,
                    Name = $"{m.Name} ({m.InventoryNumber})"
                }).ToList();

                var details = await _detailService.GetAllAsync();
                model.AvailableDetails = details.Select(d => new SelectOptionViewModel
                {
                    Id = d.Id,
                    Name = $"{d.Number} - {d.Name}"
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке данных для проверки переналадки");
                model.AvailableMachines = new List<SelectOptionViewModel>();
                model.AvailableDetails = new List<SelectOptionViewModel>();
            }
        }

        private List<SetupTimeCreateDto> ParseCsvData(string csvData)
        {
            var setupTimes = new List<SetupTimeCreateDto>();

            try
            {
                var lines = csvData.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                // Пропускаем заголовок, если есть
                var dataLines = lines.Skip(1);

                foreach (var line in dataLines)
                {
                    var fields = line.Split(',');

                    if (fields.Length >= 4)
                    {
                        var setupTime = new SetupTimeCreateDto
                        {
                            // Здесь нужно будет сопоставить названия с ID
                            // Пока что используем простые значения
                            MachineId = 1, // Нужно найти по названию
                            FromDetailId = 1, // Нужно найти по названию
                            ToDetailId = 2, // Нужно найти по названию
                            Time = double.TryParse(fields[3].Trim(), out var time) ? time : 0.5,
                            SetupDescription = fields.Length > 4 ? fields[4].Trim() : null
                        };

                        setupTimes.Add(setupTime);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при парсинге CSV данных");
                throw new ArgumentException("Ошибка в формате CSV данных");
            }

            return setupTimes;
        }

        #endregion
    }
}