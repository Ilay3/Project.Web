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

        public RouteService(
            IRouteRepository repo,
            IDetailRepository detailRepo,
            IMachineTypeRepository machineTypeRepo)
        {
            _repo = repo;
            _detailRepo = detailRepo;
            _machineTypeRepo = machineTypeRepo;
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

        public async Task AddAsync(RouteCreateDto dto)
        {
            var entity = new Route
            {
                DetailId = dto.DetailId,
                Stages = dto.Stages.Select(s => new RouteStage
                {
                    Order = s.Order,
                    Name = s.Name,
                    MachineTypeId = s.MachineTypeId,
                    NormTime = s.NormTime,
                    SetupTime = s.SetupTime,
                    StageType = s.StageType
                }).ToList()
            };
            await _repo.AddAsync(entity);
        }

        public async Task UpdateAsync(RouteEditDto dto)
        {
            var entity = await _repo.GetByIdAsync(dto.Id);
            if (entity == null) throw new Exception("Route not found");

            entity.DetailId = dto.DetailId;
            // Полная перезапись этапов:
            entity.Stages.Clear();
            foreach (var s in dto.Stages)
            {
                entity.Stages.Add(new RouteStage
                {
                    Order = s.Order,
                    Name = s.Name,
                    MachineTypeId = s.MachineTypeId,
                    NormTime = s.NormTime,
                    SetupTime = s.SetupTime,
                    StageType = s.StageType
                });
            }
            await _repo.UpdateAsync(entity);
        }

        public async Task DeleteAsync(int id) => await _repo.DeleteAsync(id);
    }
}

