using Project.Contracts.ModelDTO;
using Project.Domain.Entities;
using Project.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class RouteService
    {
        private readonly IRouteRepository _repo;
        private readonly IDetailRepository _detailRepo;
        private readonly IMachineTypeRepository _machineTypeRepo;
        private readonly IBatchRepository _batchRepo; // Добавляем для проверки связей

        public RouteService(
            IRouteRepository repo,
            IDetailRepository detailRepo,
            IMachineTypeRepository machineTypeRepo,
            IBatchRepository batchRepo)
        {
            _repo = repo;
            _detailRepo = detailRepo;
            _machineTypeRepo = machineTypeRepo;
            _batchRepo = batchRepo;
        }

        public async Task<List<RouteDto>> GetAllAsync()
        {
            var routes = await _repo.GetAllAsync();
            return routes.Select(r => new RouteDto
            {
                Id = r.Id,
                DetailId = r.DetailId,
                DetailName = r.Detail?.Name ?? "",
                Stages = r.Stages.OrderBy(s => s.Order).Select(s => new RouteStageDto
                {
                    Id = s.Id,
                    Order = s.Order,
                    Name = s.Name,
                    MachineTypeId = s.MachineTypeId,
                    MachineTypeName = s.MachineType?.Name ?? "",
                    NormTime = s.NormTime,
                    SetupTime = s.SetupTime,
                    StageType = s.StageType
                }).ToList()
            }).ToList();
        }

        public async Task<RouteDto?> GetByIdAsync(int id)
        {
            var r = await _repo.GetByIdAsync(id);
            if (r == null) return null;
            return new RouteDto
            {
                Id = r.Id,
                DetailId = r.DetailId,
                DetailName = r.Detail?.Name ?? "",
                Stages = r.Stages.OrderBy(s => s.Order).Select(s => new RouteStageDto
                {
                    Id = s.Id,
                    Order = s.Order,
                    Name = s.Name,
                    MachineTypeId = s.MachineTypeId,
                    MachineTypeName = s.MachineType?.Name ?? "",
                    NormTime = s.NormTime,
                    SetupTime = s.SetupTime,
                    StageType = s.StageType
                }).ToList()
            };
        }

        public async Task<RouteDto?> GetByDetailIdAsync(int detailId)
        {
            var r = await _repo.GetByDetailIdAsync(detailId);
            if (r == null) return null;
            return new RouteDto
            {
                Id = r.Id,
                DetailId = r.DetailId,
                DetailName = r.Detail?.Name ?? "",
                Stages = r.Stages.OrderBy(s => s.Order).Select(s => new RouteStageDto
                {
                    Id = s.Id,
                    Order = s.Order,
                    Name = s.Name,
                    MachineTypeId = s.MachineTypeId,
                    MachineTypeName = s.MachineType?.Name ?? "",
                    NormTime = s.NormTime,
                    SetupTime = s.SetupTime,
                    StageType = s.StageType
                }).ToList()
            };
        }

        public async Task AddAsync(RouteCreateDto dto)
        {
            var detail = await _detailRepo.GetByIdAsync(dto.DetailId);
            if (detail == null)
                throw new Exception($"Detail with ID {dto.DetailId} not found");

            var existingRoute = await _repo.GetByDetailIdAsync(dto.DetailId);
            if (existingRoute != null)
                throw new Exception($"Route for detail {detail.Name} already exists");

            var entity = new Route
            {
                DetailId = dto.DetailId,
                Stages = dto.Stages.Select(s => new RouteStage
                {
                    Order = s.Order,
                    Name = s.Name,
                    MachineTypeId = s.MachineTypeId,
                    NormTime = s.NormTime,
                    SetupTime = 0, // Убираем время переналадки из создания этапов
                    StageType = s.StageType
                }).ToList()
            };

            await _repo.AddAsync(entity);
        }

        public async Task UpdateAsync(RouteEditDto dto)
        {
            var entity = await _repo.GetByIdAsync(dto.Id);
            if (entity == null) throw new Exception("Route not found");

            // Проверяем, есть ли активные StageExecutions для этого маршрута
            var hasActiveStageExecutions = await HasActiveStageExecutionsAsync(dto.Id);
            if (hasActiveStageExecutions)
            {
                throw new Exception("Нельзя изменить маршрут, так как по нему выполняются или уже выполнены этапы производства. Создайте новый маршрут или дождитесь завершения всех партий.");
            }

            // Проверяем, что деталь существует, если меняется
            if (entity.DetailId != dto.DetailId)
            {
                var detail = await _detailRepo.GetByIdAsync(dto.DetailId);
                if (detail == null)
                    throw new Exception($"Detail with ID {dto.DetailId} not found");

                var existingRoute = await _repo.GetByDetailIdAsync(dto.DetailId);
                if (existingRoute != null && existingRoute.Id != dto.Id)
                    throw new Exception($"Route for detail {detail.Name} already exists");

                entity.DetailId = dto.DetailId;
            }

            // Безопасное обновление этапов - только если нет активных исполнений
            entity.Stages.Clear();
            foreach (var s in dto.Stages)
            {
                var machineType = await _machineTypeRepo.GetByIdAsync(s.MachineTypeId);
                if (machineType == null)
                    throw new Exception($"Machine type with ID {s.MachineTypeId} not found");

                entity.Stages.Add(new RouteStage
                {
                    Order = s.Order,
                    Name = s.Name,
                    MachineTypeId = s.MachineTypeId,
                    NormTime = s.NormTime,
                    SetupTime = 0, // Убираем время переналадки
                    StageType = s.StageType
                });
            }
            await _repo.UpdateAsync(entity);
        }

        public async Task DeleteAsync(int id)
        {
            var route = await _repo.GetByIdAsync(id);
            if (route == null) throw new Exception("Route not found");

            // Проверяем, есть ли активные StageExecutions для этого маршрута
            var hasActiveStageExecutions = await HasActiveStageExecutionsAsync(id);
            if (hasActiveStageExecutions)
            {
                throw new Exception("Нельзя удалить маршрут, так как по нему выполняются или уже выполнены этапы производства.");
            }

            await _repo.DeleteAsync(id);
        }

        /// <summary>
        /// Проверяет, есть ли активные или завершенные этапы по данному маршруту
        /// </summary>
        private async Task<bool> HasActiveStageExecutionsAsync(int routeId)
        {
            try
            {
                var allStageExecutions = await _batchRepo.GetAllStageExecutionsAsync();

                // Проверяем, есть ли этапы выполнения, которые ссылаются на этапы данного маршрута
                var route = await _repo.GetByIdAsync(routeId);
                if (route == null) return false;

                var routeStageIds = route.Stages.Select(s => s.Id).ToList();

                return allStageExecutions.Any(se =>
                    routeStageIds.Contains(se.RouteStageId) &&
                    (se.Status == StageExecutionStatus.InProgress ||
                     se.Status == StageExecutionStatus.Completed ||
                     se.Status == StageExecutionStatus.Paused ||
                     se.Status == StageExecutionStatus.Waiting));
            }
            catch
            {
                // В случае ошибки лучше перестраховаться
                return true;
            }
        }
    }
}