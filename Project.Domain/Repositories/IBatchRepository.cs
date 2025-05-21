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

        // Получение всех этапов в статусе "Pending" (готовы к запуску)
        Task<List<StageExecution>> GetPendingStagesAsync();

        // Получение недавно завершенных этапов, которые еще не обработаны системой планирования
        Task<List<StageExecution>> GetRecentlyCompletedStagesAsync();

        // Получение следующих доступных этапов для указанной подпартии
        Task<List<StageExecution>> GetNextAvailableStagesForSubBatchAsync(int subBatchId);

        // Получение списка свободных станков, подходящих для указанного этапа
        Task<List<Machine>> GetAvailableMachinesForStageAsync(int stageExecutionId);

        // Обновление статуса обработки для завершенного этапа
        Task MarkStageAsProcessedAsync(int stageExecutionId);

        // Получение информации о загрузке станков на указанный период
        Task<Dictionary<int, List<StageExecution>>> GetMachineScheduleAsync(DateTime startDate, DateTime endDate);

        // Получение прогнозируемого времени завершения для указанной партии
        Task<DateTime?> GetEstimatedCompletionTimeForBatchAsync(int batchId);

        // Проверка наличия возможных конфликтов в расписании
        Task<List<ScheduleConflict>> GetScheduleConflictsAsync();

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

    public class ScheduleConflict
    {
        public int MachineId { get; set; }
        public string MachineName { get; set; }
        public List<StageExecution> ConflictingStages { get; set; }
        public DateTime ConflictStartTime { get; set; }
        public DateTime ConflictEndTime { get; set; }
        public string ConflictType { get; set; } // Например: "Overlap", "DoubleBooking", "ResourceUnavailable"
    }

}