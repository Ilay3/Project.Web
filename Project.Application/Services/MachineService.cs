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
    public class MachineService
    {
        private readonly IMachineRepository _repo;
        private readonly IMachineTypeRepository _typeRepo;

        public MachineService(IMachineRepository repo, IMachineTypeRepository typeRepo)
        {
            _repo = repo;
            _typeRepo = typeRepo;
        }

        public async Task<List<MachineDto>> GetAllAsync()
        {
            var machines = await _repo.GetAllAsync();
            return machines.Select(x => new MachineDto
            {
                Id = x.Id,
                Name = x.Name,
                InventoryNumber = x.InventoryNumber,
                MachineTypeId = x.MachineTypeId,
                MachineTypeName = x.MachineType?.Name,
                Priority = x.Priority
            }).ToList();
        }

        public async Task<MachineDto?> GetByIdAsync(int id)
        {
            var m = await _repo.GetByIdAsync(id);
            if (m == null) return null;
            return new MachineDto
            {
                Id = m.Id,
                Name = m.Name,
                InventoryNumber = m.InventoryNumber,
                MachineTypeId = m.MachineTypeId,
                MachineTypeName = m.MachineType?.Name,
                Priority = m.Priority
            };
        }

        public async Task AddAsync(MachineCreateDto dto)
        {
            await _repo.AddAsync(new Machine
            {
                Name = dto.Name,
                InventoryNumber = dto.InventoryNumber,
                MachineTypeId = dto.MachineTypeId,
                Priority = dto.Priority
            });
        }

        public async Task UpdateAsync(MachineEditDto dto)
        {
            var entity = await _repo.GetByIdAsync(dto.Id);
            if (entity == null) throw new Exception("Not found");
            entity.Name = dto.Name;
            entity.InventoryNumber = dto.InventoryNumber;
            entity.MachineTypeId = dto.MachineTypeId;
            entity.Priority = dto.Priority;
            await _repo.UpdateAsync(entity);
        }

        public async Task DeleteAsync(int id) => await _repo.DeleteAsync(id);
    }

}
