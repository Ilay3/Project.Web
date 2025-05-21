using Project.Contracts.ModelDTO;
using Project.Domain.Entities;
using Project.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class HistoryService
    {
        private readonly IBatchRepository _batchRepo;
        private readonly IMachineRepository _machineRepo;

        public HistoryService(
            IBatchRepository batchRepo,
            IMachineRepository machineRepo)
        {
            _batchRepo = batchRepo;
            _machineRepo = machineRepo;
        }

        // Получение истории выполнения этапов
        public async Task<List<StageHistoryDto>> GetStageExecutionHistoryAsync(StageHistoryFilterDto filter)
        {
            var stageExecutions = await _batchRepo.GetStageExecutionHistoryAsync(
                filter.StartDate,
                filter.EndDate,
                filter.MachineId,
                filter.DetailId);

            // Применяем дополнительные фильтры
            var filtered = stageExecutions.AsEnumerable();

            // Фильтр по этапам переналадки
            if (!filter.IncludeSetups)
            {
                filtered = filtered.Where(se => !se.IsSetup);
            }

            // Фильтр по статусу
            if (filter.StatusFilter != "All")
            {
                filtered = filtered.Where(se => se.Status.ToString() == filter.StatusFilter);
            }

            return filtered.Select(se => new StageHistoryDto
            {
                Id = se.Id,
                SubBatchId = se.SubBatchId,
                BatchId = se.SubBatch.BatchId,
                DetailName = se.SubBatch.Batch.Detail.Name,
                StageName = se.IsSetup ? $"Переналадка: {se.RouteStage.Name}" : se.RouteStage.Name,
                MachineId = se.MachineId,
                MachineName = se.Machine?.Name,
                Status = se.Status.ToString(),
                StartTimeUtc = se.StartTimeUtc,
                EndTimeUtc = se.EndTimeUtc,
                PauseTimeUtc = se.PauseTimeUtc,
                ResumeTimeUtc = se.ResumeTimeUtc,
                IsSetup = se.IsSetup,
                OperatorId = se.OperatorId,
                ReasonNote = se.ReasonNote,
                Duration = CalculateDuration(se)
            }).ToList();
        }

        // Статистика за период
        public async Task<StageStatisticsDto> GetStatisticsAsync(
            DateTime startDate,
            DateTime endDate,
            int? machineId = null)
        {
            var stageExecutions = await _batchRepo.GetStageExecutionHistoryAsync(
                startDate,
                endDate,
                machineId,
                null);

            // Вычисляем общую статистику
            double totalWorkHours = 0;
            double totalSetupHours = 0;
            double totalIdleHours = 0;

            var completedStages = stageExecutions.Count(se => se.Status == StageExecutionStatus.Completed);
            var setupStages = stageExecutions.Count(se => se.IsSetup);

            // Расчет рабочего времени и времени переналадки
            foreach (var stage in stageExecutions.Where(se => se.Status == StageExecutionStatus.Completed))
            {
                var duration = CalculateDuration(stage) ?? 0;

                if (stage.IsSetup)
                    totalSetupHours += duration;
                else
                    totalWorkHours += duration;
            }

            // Расчет времени простоя (только если указан конкретный станок)
            if (machineId.HasValue)
            {
                // Получаем все этапы выполнения на указанном станке
                var machineStages = stageExecutions
                    .Where(se => se.MachineId == machineId.Value)
                    .OrderBy(se => se.StartTimeUtc)
                    .ToList();

                DateTime? lastEndTime = null;

                foreach (var stage in machineStages)
                {
                    if (stage.StartTimeUtc.HasValue && lastEndTime.HasValue)
                    {
                        // Если есть разрыв между окончанием предыдущего этапа и началом текущего - это простой
                        if (stage.StartTimeUtc.Value > lastEndTime.Value)
                        {
                            var idleHours = (stage.StartTimeUtc.Value - lastEndTime.Value).TotalHours;
                            totalIdleHours += idleHours;
                        }
                    }

                    if (stage.EndTimeUtc.HasValue)
                    {
                        lastEndTime = stage.EndTimeUtc.Value;
                    }
                }
            }

            // Расчет эффективности
            double totalHours = totalWorkHours + totalSetupHours + totalIdleHours;
            double efficiencyPercentage = totalHours > 0
                ? (totalWorkHours / totalHours) * 100
                : 0;

            return new StageStatisticsDto
            {
                TotalStages = stageExecutions.Count,
                CompletedStages = completedStages,
                SetupStages = setupStages,
                TotalWorkHours = totalWorkHours,
                TotalSetupHours = totalSetupHours,
                TotalIdleHours = totalIdleHours,
                EfficiencyPercentage = Math.Round(efficiencyPercentage, 2)
            };
        }

        // Расчет фактической длительности этапа
        private double? CalculateDuration(StageExecution stage)
        {
            if (!stage.StartTimeUtc.HasValue)
                return null;

            if (stage.EndTimeUtc.HasValue)
            {
                return (stage.EndTimeUtc.Value - stage.StartTimeUtc.Value).TotalHours;
            }

            if (stage.Status == StageExecutionStatus.InProgress)
            {
                // Для незавершенных этапов используем текущее время
                return (DateTime.UtcNow - stage.StartTimeUtc.Value).TotalHours;
            }

            return null;
        }
    }
}