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
    public class MachineTypeService
    {
        private readonly IMachineTypeRepository _repo;
        public MachineTypeService(IMachineTypeRepository repo) => _repo = repo;

        public async Task<List<MachineTypeDto>> GetAllAsync() =>
            (await _repo.GetAllAsync()).Select(x => new MachineTypeDto { Id = x.Id, Name = x.Name }).ToList();

        public async Task<MachineTypeDto?> GetByIdAsync(int id)
        {
            var entity = await _repo.GetByIdAsync(id);
            return entity == null ? null : new MachineTypeDto { Id = entity.Id, Name = entity.Name };
        }

        public async Task AddAsync(MachineTypeCreateDto dto)
        {
            await _repo.AddAsync(new MachineType { Name = dto.Name });
        }
        public async Task UpdateAsync(MachineTypeEditDto dto)
        {
            var entity = await _repo.GetByIdAsync(dto.Id);
            if (entity == null) throw new Exception("Not found");
            entity.Name = dto.Name;
            await _repo.UpdateAsync(entity);
        }
        public async Task DeleteAsync(int id) => await _repo.DeleteAsync(id);
    }


}
