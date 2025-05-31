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
        /// Получение всех станков с расширенной информацией согласно ТЗ
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
        /// Получение доступных станков для типа согласно ТЗ
        /// </summary>
        public async Task<List<MachineDto>> GetAvailableMachinesAsync(int machineTypeId)
        {
            try
            {
                var machines = await _machineRepo.GetMachinesByTypeAsync(machineTypeId);
                var result = new List<MachineDto>();

                // Получаем информацию о занятых станках
                var occupiedMachineIds = await GetOccupiedMachineIdsAsync();

                foreach (var machine in machines)
                {
                    // Станок доступен, если на нем ничего не выполняется
                    if (!occupiedMachineIds.Contains(machine.Id))
                    {
                        var machineDto = await MapToMachineDto(machine);
                        result.Add(machineDto);
                    }
                }

                return result.OrderByDescending(m => m.Priority).ToList();
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

                // Проверяем уникальность инвентарного номера
                var allMachines = await _machineRepo.GetAllAsync();
                if (allMachines.Any(m => m.InventoryNumber.Equals(dto.InventoryNumber.Trim(), StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException($"Станок с инвентарным номером '{dto.InventoryNumber}' уже существует");

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

                // Проверяем уникальность инвентарного номера (исключая текущий станок)
                var allMachines = await _machineRepo.GetAllAsync();
                if (allMachines.Any(m => m.Id != dto.Id &&
                    m.InventoryNumber.Equals(dto.InventoryNumber.Trim(), StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException($"Станок с инвентарным номером '{dto.InventoryNumber}' уже существует");

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
        /// Получение статистики загрузки станка согласно ТЗ
        /// </summary>
        public async Task<MachineUtilizationDto> GetMachineUtilizationAsync(int machineId, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var machine = await _machineRepo.GetByIdAsync(machineId);
                if (machine == null)
                    throw new ArgumentException($"Станок с ID {machineId} не найден");

                startDate ??= DateTime.Today.AddDays(-7);
                endDate ??= DateTime.Today.AddDays(1);

                var stageHistory = await _batchRepo.GetStageExecutionHistoryAsync(startDate, endDate, machineId);

                var completedStages = stageHistory.Where(s => s.Status == StageExecutionStatus.Completed).ToList();

                // Рабочее время (основные операции)
                var workingHours = completedStages
                    .Where(s => !s.IsSetup && s.ActualWorkingTime.HasValue)
                    .Sum(s => s.ActualWorkingTime!.Value.TotalHours);

                // Время переналадок
                var setupHours = completedStages
                    .Where(s => s.IsSetup && s.ActualWorkingTime.HasValue)
                    .Sum(s => s.ActualWorkingTime!.Value.TotalHours);

                // Рассчитываем общее доступное время (с учетом рабочего времени согласно ТЗ)
                var totalAvailableHours = CalculateAvailableWorkingHours(startDate.Value, endDate.Value);

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
        /// Получение календарного отчета по станку согласно ТЗ
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
                    EndDate = endDate,
                    DailyWorkingHours = new Dictionary<DateTime, double>(),
                    DailySetupHours = new Dictionary<DateTime, double>(),
                    DailyIdleHours = new Dictionary<DateTime, double>(),
                    DailyManufacturedParts = new Dictionary<DateTime, List<ManufacturedPartDto>>(),
                    DailyUtilization = new Dictionary<DateTime, decimal>()
                };

                // Группируем этапы по дням
                var stagesByDay = stageHistory
                    .Where(s => s.StartTimeUtc.HasValue)
                    .GroupBy(s => s.StartTimeUtc!.Value.Date)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Заполняем все дни в периоде
                for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
                {
                    var dayStages = stagesByDay.ContainsKey(date) ? stagesByDay[date] : new List<StageExecution>();

                    // Рабочее время (основные операции)
                    var workingHours = dayStages
                        .Where(s => !s.IsSetup && s.Status == StageExecutionStatus.Completed && s.ActualWorkingTime.HasValue)
                        .Sum(s => s.ActualWorkingTime!.Value.TotalHours);

                    // Время переналадок
                    var setupHours = dayStages
                        .Where(s => s.IsSetup && s.Status == StageExecutionStatus.Completed && s.ActualWorkingTime.HasValue)
                        .Sum(s => s.ActualWorkingTime!.Value.TotalHours);

                    // Доступное время в день с учетом рабочего времени согласно ТЗ
                    var dailyAvailableHours = CalculateAvailableWorkingHours(date, date.AddDays(1));
                    var idleHours = Math.Max(0, dailyAvailableHours - workingHours - setupHours);

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

                    // Коэффициент использования согласно ТЗ
                    var totalActiveHours = workingHours + setupHours;
                    var utilization = dailyAvailableHours > 0 ?
                        (decimal)Math.Round((totalActiveHours / dailyAvailableHours) * 100, 1) : 0;

                    report.DailyWorkingHours[date] = Math.Round(workingHours, 2);
                    report.DailySetupHours[date] = Math.Round(setupHours, 2);
                    report.DailyIdleHours[date] = Math.Round(idleHours, 2);
                    report.DailyManufacturedParts[date] = manufacturedParts;
                    report.DailyUtilization[date] = utilization;
                }

                // Общая статистика
                report.TotalStatistics = new MachineStatisticsDto
                {
                    MachineId = machineId,
                    MachineName = machine.Name,
                    MachineTypeName = machine.MachineType?.Name ?? "",
                    WorkingHours = report.DailyWorkingHours.Values.Sum(),
                    SetupHours = report.DailySetupHours.Values.Sum(),
                    IdleHours = report.DailyIdleHours.Values.Sum(),
                    UtilizationPercentage = report.DailyWorkingHours.Values.Sum() + report.DailySetupHours.Values.Sum() > 0 ?
                        (decimal)Math.Round(((report.DailyWorkingHours.Values.Sum() + report.DailySetupHours.Values.Sum()) /
                                          (report.DailyWorkingHours.Values.Sum() + report.DailySetupHours.Values.Sum() + report.DailyIdleHours.Values.Sum())) * 100, 2) : 0,
                    PartsMade = stageHistory.Count(s => !s.IsSetup && s.Status == StageExecutionStatus.Completed),
                    SetupCount = stageHistory.Count(s => s.IsSetup && s.Status == StageExecutionStatus.Completed),
                    AverageSetupTime = stageHistory.Where(s => s.IsSetup && s.ActualWorkingTime.HasValue).Any() ?
                        stageHistory.Where(s => s.IsSetup && s.ActualWorkingTime.HasValue).Average(s => s.ActualWorkingTime!.Value.TotalHours) : 0
                };

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении календарного отчета для станка {MachineId}", machineId);
                throw;
            }
        }

        #region Приватные методы

        /// <summary>
        /// Маппинг Entity в DTO
        /// </summary>
        private async Task<MachineDto> MapToMachineDto(Machine machine)
        {
            try
            {
                // Определяем текущий статус станка согласно ТЗ
                var status = await DetermineMachineStatus(machine.Id);

                // Получаем текущий этап
                var currentStage = await _batchRepo.GetCurrentStageOnMachineAsync(machine.Id);

                // Получаем очередь этапов
                var queuedStages = await _batchRepo.GetQueuedStagesForMachineAsync(machine.Id);

                // Получаем последнюю деталь для переналадки
                var lastStage = await _batchRepo.GetLastCompletedStageOnMachineAsync(machine.Id);

                // Рассчитываем загруженность за сегодня
                var todayUtilization = await CalculateTodayUtilization(machine.Id);

                // Рассчитываем время до освобождения
                var timeToFree = await CalculateTimeToFree(machine.Id);

                return new MachineDto
                {
                    Id = machine.Id,
                    Name = machine.Name,
                    InventoryNumber = machine.InventoryNumber,
                    MachineTypeId = machine.MachineTypeId,
                    MachineTypeName = machine.MachineType?.Name ?? "",
                    Priority = machine.Priority,
                    Status = status,
                    CurrentStageExecutionId = currentStage?.Id,
                    CurrentStageDescription = GetStageDescription(currentStage),
                    LastDetailId = lastStage?.SubBatch?.Batch?.DetailId,
                    LastDetailName = lastStage?.SubBatch?.Batch?.Detail?.Name,
                    LastStatusUpdateUtc = currentStage?.StatusChangedTimeUtc ?? DateTime.UtcNow,
                    TodayUtilizationPercent = todayUtilization,
                    TimeToFree = timeToFree,
                    QueueLength = queuedStages.Count
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
                    LastStatusUpdateUtc = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Определение статуса станка согласно ТЗ
        /// </summary>
        private async Task<MachineStatus> DetermineMachineStatus(int machineId)
        {
            try
            {
                var currentStage = await _batchRepo.GetCurrentStageOnMachineAsync(machineId);

                if (currentStage == null)
                    return MachineStatus.Free; // Свободен

                if (currentStage.IsSetup)
                    return MachineStatus.Setup; // Переналадка

                return MachineStatus.Busy; // Занят
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при определении статуса станка {MachineId}", machineId);
                return MachineStatus.Unknown;
            }
        }

        /// <summary>
        /// Получение ID занятых станков
        /// </summary>
        private async Task<List<int>> GetOccupiedMachineIdsAsync()
        {
            try
            {
                var inProgressStages = await _batchRepo.GetAllStageExecutionsAsync();
                return inProgressStages
                    .Where(se => se.Status == StageExecutionStatus.InProgress && se.MachineId.HasValue)
                    .Select(se => se.MachineId!.Value)
                    .Distinct()
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении ID занятых станков");
                return new List<int>();
            }
        }

        /// <summary>
        /// Расчет доступных рабочих часов согласно ТЗ
        /// </summary>
        private double CalculateAvailableWorkingHours(DateTime startDate, DateTime endDate)
        {
            double totalHours = 0;

            for (var date = startDate.Date; date < endDate.Date; date = date.AddDays(1))
            {
                // Пропускаем выходные дни согласно ТЗ (суббота, воскресенье)
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                    continue;

                // Рабочие часы согласно ТЗ: 08:00-01:30 следующего дня = 17.5 часов
                // Минус обеденный перерыв 12:00-13:00 = 1 час
                // Минус перерыв на ужин 21:00-21:30 = 0.5 часа
                // Итого: 16 рабочих часов в день
                totalHours += 16;
            }

            return totalHours;
        }

        /// <summary>
        /// Расчет загруженности за сегодня
        /// </summary>
        private async Task<decimal?> CalculateTodayUtilization(int machineId)
        {
            try
            {
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                var utilization = await GetMachineUtilizationAsync(machineId, today, tomorrow);
                return utilization.UtilizationPercentage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при расчете загруженности станка {MachineId} за сегодня", machineId);
                return null;
            }
        }

        /// <summary>
        /// Расчет времени до освобождения станка
        /// </summary>
        private async Task<TimeSpan?> CalculateTimeToFree(int machineId)
        {
            try
            {
                var currentStage = await _batchRepo.GetCurrentStageOnMachineAsync(machineId);
                if (currentStage == null)
                    return null; // Станок уже свободен

                if (!currentStage.StartTimeUtc.HasValue)
                    return null;

                // Рассчитываем оставшееся время
                var elapsed = DateTime.UtcNow - currentStage.StartTimeUtc.Value;
                var remaining = currentStage.PlannedDuration - elapsed;

                return remaining > TimeSpan.Zero ? remaining : TimeSpan.FromMinutes(5);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при расчете времени до освобождения станка {MachineId}", machineId);
                return null;
            }
        }

        /// <summary>
        /// Получение описания этапа
        /// </summary>
        private string? GetStageDescription(StageExecution? stage)
        {
            if (stage == null) return null;

            var description = stage.IsSetup ? "Переналадка: " : "";
            description += stage.RouteStage?.Name ?? "Неизвестная операция";

            if (stage.SubBatch?.Batch?.Detail != null)
            {
                description += $" ({stage.SubBatch.Batch.Detail.Name})";
            }

            return description;
        }

        #endregion
    }
}