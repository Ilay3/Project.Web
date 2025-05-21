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
    public class DetailService
    {
        private readonly IDetailRepository _repo;

        public DetailService(IDetailRepository repo)
        {
            _repo = repo;
        }

        public async Task<List<DetailDto>> GetAllAsync()
        {
            var details = await _repo.GetAllAsync();
            return details.Select(d => new DetailDto
            {
                Id = d.Id,
                Name = d.Name,
                Number = d.Number
            }).ToList();
        }

        public async Task<DetailDto?> GetByIdAsync(int id)
        {
            var d = await _repo.GetByIdAsync(id);
            if (d == null) return null;
            return new DetailDto
            {
                Id = d.Id,
                Name = d.Name,
                Number = d.Number
            };
        }

        public async Task AddAsync(DetailCreateDto dto)
        {
            var entity = new Detail
            {
                Name = dto.Name,
                Number = dto.Number
            };
            await _repo.AddAsync(entity);
        }

        public async Task UpdateAsync(DetailEditDto dto)
        {
            var entity = await _repo.GetByIdAsync(dto.Id);
            if (entity == null) throw new Exception("Detail not found");
            entity.Name = dto.Name;
            entity.Number = dto.Number;
            await _repo.UpdateAsync(entity);
        }

        public async Task DeleteAsync(int id)
        {
            await _repo.DeleteAsync(id);
        }
    }

}
