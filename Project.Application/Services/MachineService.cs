// Добавить эти методы в Project.Application/Services/MachineService.cs

using Project.Contracts.ModelDTO;
using Project.Domain.Entities;
using Project.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class MachineService
    {
        private readonly IMachineRepository _repo;

        public MachineService(IMachineRepository repo)
        {
            _repo = repo;
        }

        public async Task<List<MachineDto>> GetAllAsync()
        {
            var machines = await _repo.GetAllAsync();
            return machines.Select(m => new MachineDto
            {
                Id = m.Id,
                Name = m.Name,
                InventoryNumber = m.InventoryNumber,
                MachineTypeId = m.MachineTypeId,
                MachineTypeName = m.MachineType?.Name ?? "",
                Priority = m.Priority
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
                MachineTypeName = m.MachineType?.Name ?? "",
                Priority = m.Priority
            };
        }

        // Добавляем недостающий метод
        public async Task<List<MachineDto>> GetMachinesByTypeAsync(int machineTypeId)
        {
            var machines = await _repo.GetMachinesByTypeAsync(machineTypeId);
            return machines.Select(m => new MachineDto
            {
                Id = m.Id,
                Name = m.Name,
                InventoryNumber = m.InventoryNumber,
                MachineTypeId = m.MachineTypeId,
                MachineTypeName = m.MachineType?.Name ?? "",
                Priority = m.Priority
            }).ToList();
        }

        // Добавляем метод для получения доступных станков
        public async Task<List<MachineDto>> GetAvailableMachinesAsync(int machineTypeId)
        {
            var machines = await _repo.GetAvailableMachinesAsync(machineTypeId);
            return machines.Select(m => new MachineDto
            {
                Id = m.Id,
                Name = m.Name,
                InventoryNumber = m.InventoryNumber,
                MachineTypeId = m.MachineTypeId,
                MachineTypeName = m.MachineType?.Name ?? "",
                Priority = m.Priority
            }).ToList();
        }

        public async Task AddAsync(MachineCreateDto dto)
        {
            var entity = new Machine
            {
                Name = dto.Name,
                InventoryNumber = dto.InventoryNumber,
                MachineTypeId = dto.MachineTypeId,
                Priority = dto.Priority
            };
            await _repo.AddAsync(entity);
        }

        public async Task UpdateAsync(MachineEditDto dto)
        {
            var entity = await _repo.GetByIdAsync(dto.Id);
            if (entity == null) throw new Exception("Machine not found");

            entity.Name = dto.Name;
            entity.InventoryNumber = dto.InventoryNumber;
            entity.MachineTypeId = dto.MachineTypeId;
            entity.Priority = dto.Priority;

            await _repo.UpdateAsync(entity);
        }

        public async Task DeleteAsync(int id)
        {
            await _repo.DeleteAsync(id);
        }
    }
}