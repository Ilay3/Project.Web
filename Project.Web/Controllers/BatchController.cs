using Microsoft.AspNetCore.Mvc;
using Project.Web.ViewModels;
using Project.Application.Services;
using Project.Contracts.ModelDTO;

namespace Project.Web.Controllers
{
    public class BatchController : Controller
    {
        private readonly BatchService _batchService;
        private readonly DetailService _detailService;
        private readonly StageExecutionService _stageService;

        public BatchController(
            BatchService batchService,
            DetailService detailService,
            StageExecutionService stageService)
        {
            _batchService = batchService;
            _detailService = detailService;
            _stageService = stageService;
        }

        // Список партий
        public async Task<IActionResult> Index()
        {
            var dtos = await _batchService.GetAllAsync();
            var vms = dtos.Select(b => new BatchViewModel
            {
                Id = b.Id,
                DetailName = b.DetailName,
                Quantity = b.Quantity,
                Created = b.CreatedUtc,
                SubBatches = b.SubBatches.Select(sb => new SubBatchViewModel
                {
                    Id = sb.Id,
                    Quantity = sb.Quantity,
                    StageExecutions = sb.StageExecutions?.Select(se => new StageExecutionViewModel
                    {
                        Id = se.Id,
                        StageName = se.StageName,
                        MachineName = se.MachineName,
                        Status = se.Status,
                        StartTime = se.StartTimeUtc,
                        EndTime = se.EndTimeUtc,
                        IsSetup = se.IsSetup
                    }).ToList() ?? new List<StageExecutionViewModel>()
                }).ToList()
            }).ToList();
            return View(vms);
        }

        // Детальная страница партии
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var batch = await _batchService.GetByIdAsync(id);
                var stageExecutions = await _batchService.GetStageExecutionsForBatchAsync(id);

                // Преобразуем в ViewModel
                var viewModel = new BatchDetailsViewModel
                {
                    Id = batch.Id,
                    DetailId = batch.DetailId,
                    DetailName = batch.DetailName,
                    Quantity = batch.Quantity,
                    Created = batch.CreatedUtc,
                    SubBatches = batch.SubBatches.Select(sb => new SubBatchViewModel
                    {
                        Id = sb.Id,
                        Quantity = sb.Quantity,
                        StageExecutions = sb.StageExecutions?.Select(se => new StageExecutionViewModel
                        {
                            Id = se.Id,
                            StageName = se.StageName,
                            MachineName = se.MachineName,
                            Status = se.Status,
                            StartTime = se.StartTimeUtc,
                            EndTime = se.EndTimeUtc,
                            IsSetup = se.IsSetup
                        }).ToList() ?? new List<StageExecutionViewModel>()
                    }).ToList()
                };

                // Рассчитываем статистику выполнения
                var allStages = viewModel.SubBatches.SelectMany(sb => sb.StageExecutions).ToList();
                viewModel.TotalStages = allStages.Count;
                viewModel.CompletedStages = allStages.Count(se => se.Status == "Completed");
                viewModel.InProgressStages = allStages.Count(se => se.Status == "InProgress");
                viewModel.PendingStages = allStages.Count(se => se.Status == "Pending" || se.Status == "Waiting");

                // Рассчитываем выполнение в процентах
                viewModel.CompletionPercent = viewModel.TotalStages > 0
                    ? Math.Round(((double)viewModel.CompletedStages / viewModel.TotalStages) * 100, 1)
                    : 0;

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // Создание партии
        [HttpPost]
        public async Task<IActionResult> Create(BatchCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                await _batchService.CreateAsync(dto);
                TempData["SuccessMessage"] = "Партия успешно создана";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при создании партии: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // Редактирование партии
        [HttpPost]
        public async Task<IActionResult> Edit(BatchEditDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                await _batchService.UpdateAsync(dto);
                TempData["SuccessMessage"] = "Партия успешно обновлена";
                return RedirectToAction(nameof(Details), new { id = dto.Id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при обновлении партии: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id = dto.Id });
            }
        }

        // Удаление партии
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _batchService.DeleteAsync(id);
                TempData["SuccessMessage"] = "Партия успешно удалена";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при удалении партии: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // API для получения этапов партии
        [HttpGet]
        public async Task<IActionResult> GetStageExecutions(int id)
        {
            try
            {
                var stageExecutions = await _batchService.GetStageExecutionsForBatchAsync(id);
                return Json(stageExecutions);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}