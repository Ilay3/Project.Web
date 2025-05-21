using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Repositories
{
    public interface IBatchRepository
    {
        // Базовые CRUD операции для партий
        Task<List<Batch>> GetAllAsync();
        Task<Batch?> GetByIdAsync(int id);
        Task AddAsync(Batch entity);
        Task UpdateAsync(Batch entity);
        Task DeleteAsync(int id);

        // Операции с подпартиями
        Task<SubBatch?> GetSubBatchByIdAsync(int id);
        Task UpdateSubBatchAsync(SubBatch entity);

        // Операции с этапами выполнения
        Task<StageExecution?> GetStageExecutionByIdAsync(int id);
        Task UpdateStageExecutionAsync(StageExecution entity);
        Task<List<StageExecution>> GetAllStageExecutionsAsync();

        // Специализированные запросы для планирования
        Task<StageExecution?> GetLastCompletedStageOnMachineAsync(int machineId);
        Task<StageExecution?> GetCurrentStageOnMachineAsync(int machineId);
        Task<StageExecution?> GetSetupStageForMainStageAsync(int mainStageId);
        Task<StageExecution?> GetMainStageForSetupAsync(int setupStageId);
        Task<StageExecution?> GetNextStageInQueueForMachineAsync(int machineId);
        Task<List<StageExecution>> GetAllStagesInQueueAsync();
        Task<List<StageExecution>> GetQueuedStagesForMachineAsync(int machineId);

        // История и отчетность
        Task<List<StageExecution>> GetStageExecutionHistoryAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? machineId = null,
            int? detailId = null);
    }
}