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
        private readonly IBatchRepository _batchRepo;
        private readonly IDetailRepository _detailRepo;
        private readonly IRouteRepository _routeRepo;
        private readonly StageExecutionService _stageService;

        public BatchService(
            IBatchRepository batchRepo,
            IDetailRepository detailRepo,
            IRouteRepository routeRepo,
            StageExecutionService stageService)
        {
            _batchRepo = batchRepo;
            _detailRepo = detailRepo;
            _routeRepo = routeRepo;
            _stageService = stageService;
        }

        public async Task<List<BatchDto>> GetAllAsync()
        {
            var batches = await _batchRepo.GetAllAsync();
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
                    StageExecutions = sb.StageExecutions?.Select(se => new StageExecutionDto
                    {
                        Id = se.Id,
                        RouteStageId = se.RouteStageId,
                        StageName = se.RouteStage?.Name ?? "",
                        MachineId = se.MachineId,
                        MachineName = se.Machine?.Name,
                        Status = se.Status.ToString(),
                        StartTimeUtc = se.StartTimeUtc,
                        EndTimeUtc = se.EndTimeUtc,
                        IsSetup = se.IsSetup
                    }).ToList() ?? new List<StageExecutionDto>()
                }).ToList() ?? new List<SubBatchDto>()
            }).ToList();
        }

        public async Task<BatchDto> GetByIdAsync(int id)
        {
            var batch = await _batchRepo.GetByIdAsync(id);
            if (batch == null)
                throw new Exception($"Batch with id {id} not found");

            return new BatchDto
            {
                Id = batch.Id,
                DetailId = batch.DetailId,
                DetailName = batch.Detail?.Name ?? "",
                Quantity = batch.Quantity,
                CreatedUtc = batch.CreatedUtc,
                SubBatches = batch.SubBatches?.Select(sb => new SubBatchDto
                {
                    Id = sb.Id,
                    Quantity = sb.Quantity,
                    StageExecutions = sb.StageExecutions?.Select(se => new StageExecutionDto
                    {
                        Id = se.Id,
                        RouteStageId = se.RouteStageId,
                        StageName = se.RouteStage?.Name ?? "",
                        MachineId = se.MachineId,
                        MachineName = se.Machine?.Name,
                        Status = se.Status.ToString(),
                        StartTimeUtc = se.StartTimeUtc,
                        EndTimeUtc = se.EndTimeUtc,
                        IsSetup = se.IsSetup
                    }).ToList() ?? new List<StageExecutionDto>()
                }).ToList() ?? new List<SubBatchDto>()
            };
        }

        public async Task<int> CreateAsync(BatchCreateDto dto)
        {
            // Создаем партию
            var batch = new Batch
            {
                DetailId = dto.DetailId,
                Quantity = dto.Quantity,
                CreatedUtc = DateTime.UtcNow,
                SubBatches = new List<SubBatch>()
            };

            // Если не указаны подпартии, создаем одну на весь объем
            if (dto.SubBatches == null || !dto.SubBatches.Any())
            {
                batch.SubBatches.Add(new SubBatch
                {
                    Quantity = dto.Quantity,
                    StageExecutions = new List<StageExecution>()
                });
            }
            else
            {
                // Иначе создаем указанные подпартии
                foreach (var sbDto in dto.SubBatches)
                {
                    batch.SubBatches.Add(new SubBatch
                    {
                        Quantity = sbDto.Quantity,
                        StageExecutions = new List<StageExecution>()
                    });
                }
            }

            await _batchRepo.AddAsync(batch);

            // Генерируем этапы маршрута для каждой подпартии
            foreach (var subBatch in batch.SubBatches)
            {
                await GenerateStageExecutionsForSubBatch(subBatch.Id);
            }

            return batch.Id;
        }

        public async Task UpdateAsync(BatchEditDto dto)
        {
            var entity = await _batchRepo.GetByIdAsync(dto.Id);
            if (entity == null) throw new Exception("Batch not found");

            entity.Quantity = dto.Quantity;

            // Только для подпартий, которые еще не начали выполняться
            // можно изменять количество
            foreach (var sbDto in dto.SubBatches)
            {
                var subBatch = entity.SubBatches.FirstOrDefault(sb => sb.Id == sbDto.Id);
                if (subBatch != null)
                {
                    // Проверяем, что ни один этап подпартии еще не начал выполняться
                    var canUpdate = !subBatch.StageExecutions.Any(se =>
                        se.Status == StageExecutionStatus.InProgress ||
                        se.Status == StageExecutionStatus.Completed);

                    if (canUpdate)
                    {
                        subBatch.Quantity = sbDto.Quantity;
                    }
                }
            }

            await _batchRepo.UpdateAsync(entity);
        }

        public async Task DeleteAsync(int id)
        {
            var batch = await _batchRepo.GetByIdAsync(id);
            if (batch == null) throw new Exception("Batch not found");

            // Проверяем, что ни один этап партии еще не начал выполняться
            var canDelete = !batch.SubBatches.Any(sb =>
                sb.StageExecutions.Any(se =>
                    se.Status == StageExecutionStatus.InProgress ||
                    se.Status == StageExecutionStatus.Completed));

            if (!canDelete)
                throw new Exception("Cannot delete batch that has started execution");

            await _batchRepo.DeleteAsync(id);
        }

        // Генерация этапов выполнения для подпартии на основе маршрута детали
        private async Task GenerateStageExecutionsForSubBatch(int subBatchId)
        {
            var subBatch = await _batchRepo.GetSubBatchByIdAsync(subBatchId);
            if (subBatch == null) throw new Exception("SubBatch not found");

            var batch = subBatch.Batch;
            var detail = batch.Detail;

            // Получаем маршрут для детали
            var route = await _routeRepo.GetByDetailIdAsync(detail.Id);
            if (route == null) throw new Exception($"Route not found for Detail {detail.Name}");

            // Создаем этапы выполнения в той же последовательности, что и в маршруте
            foreach (var stage in route.Stages.OrderBy(s => s.Order))
            {
                var stageExecution = new StageExecution
                {
                    SubBatchId = subBatchId,
                    RouteStageId = stage.Id,
                    Status = StageExecutionStatus.Pending, // ожидание
                    IsSetup = false // это основной этап, не переналадка
                };

                subBatch.StageExecutions.Add(stageExecution);
            }

            await _batchRepo.UpdateSubBatchAsync(subBatch);
        }

        // Получение всех этапов выполнения для партии
        public async Task<List<StageExecutionDto>> GetStageExecutionsForBatchAsync(int batchId)
        {
            var batch = await _batchRepo.GetByIdAsync(batchId);
            if (batch == null)
                throw new Exception($"Batch with id {batchId} not found");

            var result = new List<StageExecutionDto>();

            foreach (var subBatch in batch.SubBatches)
            {
                foreach (var se in subBatch.StageExecutions)
                {
                    result.Add(new StageExecutionDto
                    {
                        Id = se.Id,
                        RouteStageId = se.RouteStageId,
                        StageName = se.RouteStage?.Name ?? "",
                        MachineId = se.MachineId,
                        MachineName = se.Machine?.Name,
                        Status = se.Status.ToString(),
                        StartTimeUtc = se.StartTimeUtc,
                        EndTimeUtc = se.EndTimeUtc,
                        IsSetup = se.IsSetup
                    });
                }
            }

            return result;
        }
    }
}