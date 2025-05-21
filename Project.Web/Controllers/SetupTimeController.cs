using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
using Project.Web.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Web.Controllers
{
    public class SetupTimeController : Controller
    {
        private readonly SetupTimeService _setupTimeService;
        private readonly MachineService _machineService;
        private readonly DetailService _detailService;

        public SetupTimeController(
            SetupTimeService setupTimeService,
            MachineService machineService,
            DetailService detailService)
        {
            _setupTimeService = setupTimeService;
            _machineService = machineService;
            _detailService = detailService;
        }

        public async Task<IActionResult> Index()
        {
            var setupTimes = await _setupTimeService.GetAllAsync();

            // Получаем списки машин и деталей для справочников
            var machines = await _machineService.GetAllAsync();
            var details = await _detailService.GetAllAsync();

            // Создаем словари для быстрого доступа
            var machineDict = machines.ToDictionary(m => m.Id, m => m.Name);
            var detailDict = details.ToDictionary(d => d.Id, d => d.Name);

            var viewModels = setupTimes.Select(st => new SetupTimeViewModel
            {
                Id = st.Id,
                MachineName = machineDict.ContainsKey(st.MachineId) ? machineDict[st.MachineId] : "Unknown",
                FromDetailName = detailDict.ContainsKey(st.FromDetailId) ? detailDict[st.FromDetailId] : "Unknown",
                ToDetailName = detailDict.ContainsKey(st.ToDetailId) ? detailDict[st.ToDetailId] : "Unknown",
                Time = st.Time
            }).ToList();

            // Сохраняем списки для выпадающих списков в форме создания
            ViewBag.Machines = machines;
            ViewBag.Details = details;

            return View(viewModels);
        }

        [HttpPost]
        public async Task<IActionResult> Create(SetupTimeDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _setupTimeService.AddAsync(dto);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);

                // Восстанавливаем списки для выпадающих списков
                var machines = await _machineService.GetAllAsync();
                var details = await _detailService.GetAllAsync();

                ViewBag.Machines = machines;
                ViewBag.Details = details;

                return View("Index", await GetSetupTimeViewModels());
            }
        }

        [HttpPost]
        public async Task<IActionResult> Edit(SetupTimeDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _setupTimeService.UpdateAsync(dto);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);

                // Восстанавливаем списки для выпадающих списков
                var machines = await _machineService.GetAllAsync();
                var details = await _detailService.GetAllAsync();

                ViewBag.Machines = machines;
                ViewBag.Details = details;

                return View("Index", await GetSetupTimeViewModels());
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _setupTimeService.DeleteAsync(id);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // API для проверки необходимости переналадки (AJAX)
        [HttpGet]
        public async Task<IActionResult> CheckSetupNeeded(int machineId, int detailId)
        {
            var setupInfo = await _setupTimeService.CheckSetupNeededAsync(machineId, detailId);
            return Json(setupInfo);
        }

        // Вспомогательный метод для получения моделей представления
        private async Task<List<SetupTimeViewModel>> GetSetupTimeViewModels()
        {
            var setupTimes = await _setupTimeService.GetAllAsync();

            // Получаем списки машин и деталей для справочников
            var machines = await _machineService.GetAllAsync();
            var details = await _detailService.GetAllAsync();

            // Создаем словари для быстрого доступа
            var machineDict = machines.ToDictionary(m => m.Id, m => m.Name);
            var detailDict = details.ToDictionary(d => d.Id, d => d.Name);

            return setupTimes.Select(st => new SetupTimeViewModel
            {
                Id = st.Id,
                MachineName = machineDict.ContainsKey(st.MachineId) ? machineDict[st.MachineId] : "Unknown",
                FromDetailName = detailDict.ContainsKey(st.FromDetailId) ? detailDict[st.FromDetailId] : "Unknown",
                ToDetailName = detailDict.ContainsKey(st.ToDetailId) ? detailDict[st.ToDetailId] : "Unknown",
                Time = st.Time
            }).ToList();
        }
    }
}