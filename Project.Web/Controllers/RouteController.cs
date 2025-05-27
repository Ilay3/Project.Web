using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
using Project.Web.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Web.Controllers
{
    public class RouteController : Controller
    {
        private readonly RouteService _routeService;
        private readonly DetailService _detailService;
        private readonly MachineTypeService _machineTypeService;

        public RouteController(
            RouteService routeService,
            DetailService detailService,
            MachineTypeService machineTypeService)
        {
            _routeService = routeService;
            _detailService = detailService;
            _machineTypeService = machineTypeService;
        }

        public async Task<IActionResult> Index()
        {
            var routes = await _routeService.GetAllAsync();
            var viewModels = routes.Select(r => new RouteViewModel
            {
                Id = r.Id,
                DetailName = r.DetailName,
                Stages = r.Stages.Select(s => new RouteStageViewModel
                {
                    Id = s.Id,
                    Order = s.Order,
                    Name = s.Name,
                    MachineTypeName = s.MachineTypeName,
                    NormTime = s.NormTime,
                    SetupTime = s.SetupTime,
                    StageType = s.StageType
                }).ToList()
            }).ToList();

            return View(viewModels);
        }

        public async Task<IActionResult> Details(int id)
        {
            var routeDto = await _routeService.GetByIdAsync(id);
            if (routeDto == null)
                return NotFound();

            return View(routeDto);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var details = await _detailService.GetAllAsync();
            var machineTypes = await _machineTypeService.GetAllAsync();

            ViewBag.Details = details;
            ViewBag.MachineTypes = machineTypes;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(RouteCreateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _routeService.AddAsync(dto);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);

                var details = await _detailService.GetAllAsync();
                var machineTypes = await _machineTypeService.GetAllAsync();

                ViewBag.Details = details;
                ViewBag.MachineTypes = machineTypes;

                return View(dto);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var route = await _routeService.GetByIdAsync(id);
            if (route == null)
                return NotFound();

            var details = await _detailService.GetAllAsync();
            var machineTypes = await _machineTypeService.GetAllAsync();

            ViewBag.Details = details;
            ViewBag.MachineTypes = machineTypes;

            var dto = new RouteEditDto
            {
                Id = route.Id,
                DetailId = route.DetailId,
                Stages = route.Stages.Select(s => new RouteStageEditDto
                {
                    Id = s.Id,
                    Order = s.Order,
                    Name = s.Name,
                    MachineTypeId = s.MachineTypeId,
                    NormTime = s.NormTime,
                    SetupTime = s.SetupTime,
                    StageType = s.StageType
                }).ToList()
            };

            return View(dto);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(RouteEditDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _routeService.UpdateAsync(dto);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);

                var details = await _detailService.GetAllAsync();
                var machineTypes = await _machineTypeService.GetAllAsync();

                ViewBag.Details = details;
                ViewBag.MachineTypes = machineTypes;

                return View(dto);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _routeService.DeleteAsync(id);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetRouteForDetail(int detailId)
        {
            var route = await _routeService.GetByDetailIdAsync(detailId);
            return Json(route);
        }
    }
}