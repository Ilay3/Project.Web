using Project.Contracts.ModelDTO;
using Project.Contracts.Enums;
using Project.Domain.Entities;
using Project.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class MachineService
    {
        private readonly IMachineRepository _machineRepo;
        private readonly IMachineTypeRepository _machineTypeRepo;
        private readonly IBatchRepository _batchRepo;
        private readonly ILogger<MachineService> _logger;

        public MachineService(
            IMachineRepository machineRepo,
            IMachineTypeRepository machineTypeRepo,
            IBatchRepository batchRepo,
            ILogger<MachineService> logger)
        {
            _machineRepo = machineRepo;
            _machineTypeRepo = machineTypeRepo;
            _batchRepo = batchRepo;
            _logger = logger;
        }

        /// <summary>
        /// Получение всех станков с расширенной информацией
        /// </summary>
        public async Task<List<MachineDto>> GetAllAsync()
        {
            try
            {
                var machines = await _machineRepo.GetAllAsync();
                var result = new List<MachineDto>();

                foreach (var machine in machines)
                {
                    var machineDto = await MapToMachineDto(machine);
                    result.Add(machineDto);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка станков");
                throw;
            }
        }

        /// <summary>
        /// Получение станка по ID с полной информацией
        /// </summary>
        public async Task<MachineDto?> GetByIdAsync(int id)
        {
            try
            {
                var machine = await _machineRepo.GetByIdAsync(id);
                if (machine == null) return null;

                return await MapToMachineDto(machine);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении станка {MachineId}", id);
                throw;
            }
        }

        /// <summary>
        /// Получение станков по типу
        /// </summary>
        public async Task<List<MachineDto>> GetMachinesByTypeAsync(int machineTypeId)
        {
            try
            {
                var machines = await _machineRepo.GetMachinesByTypeAsync(machineTypeId);
                var result = new List<MachineDto>();

                foreach (var machine in machines)
                {
                    var machineDto = await MapToMachineDto(machine);
                    result.Add(machineDto);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении станков типа {MachineTypeId}", machineTypeId);
                throw;
            }
        }

        /// <summary>
        /// Получение доступных станков для типа
        /// </summary>
        public async Task<List<MachineDto>> GetAvailableMachinesAsync(int machineTypeId)
        {
            try
            {
                var machines = await _machineRepo.GetAvailableMachinesAsync(machineTypeId);
                var result = new List<MachineDto>();

                foreach (var machine in machines)
                {
                    var machineDto = await MapToMachineDto(machine);
                    result.Add(machineDto);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении доступных станков типа {MachineTypeId}", machineTypeId);
                throw;
            }
        }

        /// <summary>
        /// Создание нового станка
        /// </summary>
        public async Task<int> CreateAsync(MachineCreateDto dto)
        {
            try
            {
                // Валидация
                if (string.IsNullOrWhiteSpace(dto.Name))
                    throw new ArgumentException("Название станка обязательно");

                if (string.IsNullOrWhiteSpace(dto.InventoryNumber))
                    throw new ArgumentException("Инвентарный номер обязателен");

                // Проверяем существование типа станка
                var machineType = await _machineTypeRepo.GetByIdAsync(dto.MachineTypeId);
                if (machineType == null)
                    throw new ArgumentException($"Тип станка с ID {dto.MachineTypeId} не найден");

                var entity = new Machine
                {
                    Name = dto.Name.Trim(),
                    InventoryNumber = dto.InventoryNumber.Trim(),
                    MachineTypeId = dto.MachineTypeId,
                    Priority = dto.Priority
                };

                await _machineRepo.AddAsync(entity);

                _logger.LogInformation("Создан станок: {MachineName} ({InventoryNumber}), тип: {MachineType}",
                    entity.Name, entity.InventoryNumber, machineType.Name);

                return entity.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании станка {@MachineDto}", dto);
                throw;
            }
        }

        /// <summary>
        /// Обновление станка
        /// </summary>
        public async Task UpdateAsync(MachineEditDto dto)
        {
            try
            {
                var entity = await _machineRepo.GetByIdAsync(dto.Id);
                if (entity == null)
                    throw new ArgumentException($"Станок с ID {dto.Id} не найден");

                // Валидация
                if (string.IsNullOrWhiteSpace(dto.Name))
                    throw new ArgumentException("Название станка обязательно");

                if (string.IsNullOrWhiteSpace(dto.InventoryNumber))
                    throw new ArgumentException("Инвентарный номер обязателен");

                // Проверяем существование типа станка
                var machineType = await _machineTypeRepo.GetByIdAsync(dto.MachineTypeId);
                if (machineType == null)
                    throw new ArgumentException($"Тип станка с ID {dto.MachineTypeId} не найден");

                entity.Name = dto.Name.Trim();
                entity.InventoryNumber = dto.InventoryNumber.Trim();
                entity.MachineTypeId = dto.MachineTypeId;
                entity.Priority = dto.Priority;

                await _machineRepo.UpdateAsync(entity);

                _logger.LogInformation("Обновлен станок {MachineId}: {MachineName}", dto.Id, dto.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении станка {@MachineDto}", dto);
                throw;
            }
        }

        /// <summary>
        /// Удаление станка
        /// </summary>
        public async Task DeleteAsync(int id)
        {
            try
            {
                var entity = await _machineRepo.GetByIdAsync(id);
                if (entity == null)
                    throw new ArgumentException($"Станок с ID {id} не найден");

                // Проверяем, есть ли активные этапы на этом станке
                var activeStages = await _batchRepo.GetAllStageExecutionsAsync();
                var hasActiveStages = activeStages.Any(se =>
                    se.MachineId == id &&
                    (se.Status == StageExecutionStatus.InProgress ||
                     se.Status == StageExecutionStatus.Pending ||
                     se.Status == StageExecutionStatus.Waiting));

                if (hasActiveStages)
                    throw new InvalidOperationException($"Нельзя удалить станок {entity.Name} - на нем есть активные этапы");

                await _machineRepo.DeleteAsync(id);

                _logger.LogInformation("Удален станок {MachineId}: {MachineName}", id, entity.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении станка {MachineId}", id);
                throw;
            }
        }

        /// <summary>
        /// Получение статистики загрузки станка
        /// </summary>
        public async Task<MachineUtilizationDto> GetMachineUtilizationAsync(int machineId, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var machine = await _machineRepo.GetByIdAsync(machineId);
                if (machine == null)
                    throw new ArgumentException($"Станок с ID {machineId} не найден");

                startDate ??= DateTime.Today.AddDays(-30);
                endDate ??= DateTime.Today.AddDays(1);

                var stageHistory = await _batchRepo.GetStageExecutionHistoryAsync(startDate, endDate, machineId);

                var completedStages = stageHistory.Where(s => s.Status == StageExecutionStatus.Completed).ToList();
                var workingHours = completedStages
                    .Where(s => !s.IsSetup && s.ActualWorkingTime.HasValue)
                    .Sum(s => s.ActualWorkingTime!.Value.TotalHours);

                var setupHours = completedStages
                    .Where(s => s.IsSetup && s.ActualWorkingTime.HasValue)
                    .Sum(s => s.ActualWorkingTime!.Value.TotalHours);

                var totalAvailableHours = (endDate.Value - startDate.Value).TotalHours;
                var utilizationPercentage = totalAvailableHours > 0 ?
                    (decimal)((workingHours + setupHours) / totalAvailableHours * 100) : 0;

                return new MachineUtilizationDto
                {
                    MachineId = machineId,
                    MachineName = machine.Name,
                    MachineTypeName = machine.MachineType?.Name ?? "",
                    TotalAvailableHours = totalAvailableHours,
                    WorkingHours = workingHours,
                    SetupHours = setupHours,
                    IdleHours = Math.Max(0, totalAvailableHours - workingHours - setupHours),
                    UtilizationPercentage = Math.Round(utilizationPercentage, 2),
                    CompletedOperations = completedStages.Count(s => !s.IsSetup),
                    SetupOperations = completedStages.Count(s => s.IsSetup)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении статистики загрузки станка {MachineId}", machineId);
                throw;
            }
        }

        /// <summary>
        /// Получение календарного отчета по станку
        /// </summary>
        public async Task<MachineCalendarReportDto> GetMachineCalendarReportAsync(int machineId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var machine = await _machineRepo.GetByIdAsync(machineId);
                if (machine == null)
                    throw new ArgumentException($"Станок с ID {machineId} не найден");

                var stageHistory = await _batchRepo.GetStageExecutionHistoryAsync(startDate, endDate, machineId);

                var report = new MachineCalendarReportDto
                {
                    MachineId = machineId,
                    MachineName = machine.Name,
                    MachineTypeName = machine.MachineType?.Name ?? "",
                    StartDate = startDate,
                    EndDate = endDate
                };

                // Группируем по дням
                var stagesByDay = stageHistory
                    .Where(s => s.StartTimeUtc.HasValue)
                    .GroupBy(s => s.StartTimeUtc!.Value.Date)
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var day in stagesByDay)
                {
                    var dayDate = day.Key;
                    var dayStages = day.Value;

                    // Рабочее время
                    var workingHours = dayStages
                        .Where(s => !s.IsSetup && s.ActualWorkingTime.HasValue)
                        .Sum(s => s.ActualWorkingTime!.Value.TotalHours);

                    // Время переналадок
                    var setupHours = dayStages
                        .Where(s => s.IsSetup && s.ActualWorkingTime.HasValue)
                        .Sum(s => s.ActualWorkingTime!.Value.TotalHours);

                    // Изготовленные детали
                    var manufacturedParts = dayStages
                        .Where(s => !s.IsSetup && s.Status == StageExecutionStatus.Completed)
                        .Select(s => new ManufacturedPartDto
                        {
                            DetailName = s.SubBatch.Batch.Detail.Name,
                            DetailNumber = s.SubBatch.Batch.Detail.Number,
                            Quantity = s.SubBatch.Quantity,
                            ManufacturingTimeHours = s.ActualWorkingTime?.TotalHours ?? 0,
                            CompletedUtc = s.EndTimeUtc ?? DateTime.UtcNow,
                            BatchId = s.SubBatch.BatchId
                        }).ToList();

                    report.DailyWorkingHours[dayDate] = Math.Round(workingHours, 2);
                    report.DailySetupHours[dayDate] = Math.Round(setupHours, 2);
                    report.DailyIdleHours[dayDate] = Math.Max(0, 24 - workingHours - setupHours); // Упрощенный расчет
                    report.DailyManufacturedParts[dayDate] = manufacturedParts;
                    report.DailyUtilization[dayDate] = workingHours + setupHours > 0 ?
                        (decimal)Math.Round((workingHours / (workingHours + setupHours)) * 100, 1) : 0;
                }

                // Общая статистика
                report.TotalStatistics = await GetMachineStatisticsAsync(machineId, startDate, endDate);

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении календарного отчета для станка {MachineId}", machineId);
                throw;
            }
        }

        /// <summary>
        /// Маппинг Entity в DTO
        /// </summary>
        private async Task<MachineDto> MapToMachineDto(Machine machine)
        {
            try
            {
                // Определяем текущий статус станка
                var currentStage = await _batchRepo.GetCurrentStageOnMachineAsync(machine.Id);
                var status = currentStage != null ? MachineStatus.Busy : MachineStatus.Available;

                // Получаем очередь этапов
                var queuedStages = await _batchRepo.GetQueuedStagesForMachineAsync(machine.Id);

                // Рассчитываем загруженность за последние 7 дней
                var utilizationData = await GetMachineUtilizationAsync(machine.Id,
                    DateTime.Today.AddDays(-7), DateTime.Today.AddDays(1));

                return new MachineDto
                {
                    Id = machine.Id,
                    Name = machine.Name,
                    InventoryNumber = machine.InventoryNumber,
                    MachineTypeId = machine.MachineTypeId,
                    MachineTypeName = machine.MachineType?.Name ?? "",
                    Priority = machine.Priority,
                    Status = status,
                    CurrentStageId = currentStage?.Id,
                    CurrentStageName = currentStage?.RouteStage?.Name,
                    CurrentDetailName = currentStage?.SubBatch?.Batch?.Detail?.Name,
                    QueueLength = queuedStages.Count,
                    EstimatedAvailableTime = await CalculateEstimatedAvailableTime(machine.Id),
                    UtilizationPercentage = utilizationData.UtilizationPercentage,
                    LastMaintenanceDate = null, // Пока не реализовано в entity
                    NextMaintenanceDate = null, // Пока не реализовано в entity
                    CanDelete = await CanDeleteMachine(machine.Id)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка при маппинге станка {MachineId}, используем базовые данные", machine.Id);

                return new MachineDto
                {
                    Id = machine.Id,
                    Name = machine.Name,
                    InventoryNumber = machine.InventoryNumber,
                    MachineTypeId = machine.MachineTypeId,
                    MachineTypeName = machine.MachineType?.Name ?? "",
                    Priority = machine.Priority,
                    Status = MachineStatus.Unknown,
                    CanDelete = false
                };
            }
        }

        /// <summary>
        /// Расчет ожидаемого времени освобождения станка
        /// </summary>
        private async Task<DateTime?> CalculateEstimatedAvailableTime(int machineId)
        {
            try
            {
                var currentStage = await _batchRepo.GetCurrentStageOnMachineAsync(machineId);
                if (currentStage == null) return DateTime.UtcNow; // Станок свободен

                var estimatedEndTime = DateTime.UtcNow;

                // Время завершения текущего этапа
                if (currentStage.StartTimeUtc.HasValue)
                {
                    var elapsed = DateTime.UtcNow - currentStage.StartTimeUtc.Value;
                    var remaining = currentStage.PlannedDuration - elapsed;
                    estimatedEndTime = DateTime.UtcNow.Add(remaining > TimeSpan.Zero ? remaining : TimeSpan.FromMinutes(5));
                }

                // Добавляем время выполнения этапов в очереди
                var queuedStages = await _batchRepo.GetQueuedStagesForMachineAsync(machineId);
                foreach (var stage in queuedStages)
                {
                    estimatedEndTime = estimatedEndTime.Add(stage.PlannedDuration);
                }

                return estimatedEndTime;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка при расчете времени освобождения станка {MachineId}", machineId);
                return null;
            }
        }

        /// <summary>
        /// Проверка возможности удаления станка
        /// </summary>
        private async Task<bool> CanDeleteMachine(int machineId)
        {
            try
            {
                var allStages = await _batchRepo.GetAllStageExecutionsAsync();
                return !allStages.Any(se => se.MachineId == machineId &&
                                          (se.Status == StageExecutionStatus.InProgress ||
                                           se.Status == StageExecutionStatus.Pending ||
                                           se.Status == StageExecutionStatus.Waiting));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Получение общей статистики станка
        /// </summary>
        private async Task<MachineStatisticsDto> GetMachineStatisticsAsync(int machineId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var utilizationData = await GetMachineUtilizationAsync(machineId, startDate, endDate);

                return new MachineStatisticsDto
                {
                    MachineId = machineId,
                    TotalWorkingHours = utilizationData.WorkingHours,
                    TotalSetupHours = utilizationData.SetupHours,
                    TotalIdleHours = utilizationData.IdleHours,
                    UtilizationPercentage = utilizationData.UtilizationPercentage,
                    CompletedOperations = utilizationData.CompletedOperations,
                    SetupOperations = utilizationData.SetupOperations,
                    AverageOperationTime = utilizationData.CompletedOperations > 0 ?
                        utilizationData.WorkingHours / utilizationData.CompletedOperations : 0,
                    AverageSetupTime = utilizationData.SetupOperations > 0 ?
                        utilizationData.SetupHours / utilizationData.SetupOperations : 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении статистики станка {MachineId}", machineId);
                throw;
            }
        }
    }
}