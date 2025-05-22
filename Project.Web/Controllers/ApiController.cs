using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;

namespace Project.Web.Controllers
{
    [Route("api")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly DetailService _detailService;
        private readonly MachineTypeService _machineTypeService;
        private readonly MachineService _machineService;

        public ApiController(
            DetailService detailService,
            MachineTypeService machineTypeService,
            MachineService machineService)
        {
            _detailService = detailService;
            _machineTypeService = machineTypeService;
            _machineService = machineService;
        }

        [HttpGet("details")]
        public async Task<IActionResult> GetDetails()
        {
            try
            {
                var details = await _detailService.GetAllAsync();
                return Ok(details);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("machinetypes")]
        public async Task<IActionResult> GetMachineTypes()
        {
            try
            {
                var machineTypes = await _machineTypeService.GetAllAsync();
                return Ok(machineTypes);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("machines")]
        public async Task<IActionResult> GetMachines()
        {
            try
            {
                var machines = await _machineService.GetAllAsync();
                return Ok(machines);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}