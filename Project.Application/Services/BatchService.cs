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
    public class BatchService
    {
        private readonly IBatchRepository _repo;
        private readonly IDetailRepository _detailRepo;

        public BatchService(IBatchRepository repo, IDetailRepository detailRepo)
        {
            _repo = repo;
            _detailRepo = detailRepo;
        }

        public async Task<List<BatchDto>> GetAllAsync()
        {
            var batches = await _repo.GetAllAsync();
            return batches.Select(b => new BatchDto
            {
                Id = b.Id,
                DetailId = b.DetailId,
                DetailName = b.Detail?.Name ?? "",
                Quantity = b.Quantity,
                CreatedUtc = b.CreatedUtc,
                SubBatches = b.SubBatches?.Select(sb => new SubBatchDto
                {
                    Id = sb.Id,
                    Quantity = sb.Quantity,
                    // StageExecutions — добавь если нужно
                }).ToList() ?? new List<SubBatchDto>()
            }).ToList();
        }

        public async Task AddAsync(BatchCreateDto dto)
        {
            var entity = new Batch
            {
                DetailId = dto.DetailId,
                Quantity = dto.Quantity,
                CreatedUtc = DateTime.UtcNow,
                SubBatches = dto.SubBatches?.Select(sb => new SubBatch
                {
                    Quantity = sb.Quantity
                }).ToList() ?? new List<SubBatch>()
            };
            await _repo.AddAsync(entity);
        }

        public async Task UpdateAsync(BatchEditDto dto)
        {
            var entity = await _repo.GetByIdAsync(dto.Id);
            if (entity == null) throw new Exception("Batch not found");
            entity.Quantity = dto.Quantity;
            entity.SubBatches.Clear();
            foreach (var sbDto in dto.SubBatches)
            {
                entity.SubBatches.Add(new SubBatch
                {
                    Quantity = sbDto.Quantity
                });
            }
            await _repo.UpdateAsync(entity);
        }

        public async Task DeleteAsync(int id) => await _repo.DeleteAsync(id);
    }


}
