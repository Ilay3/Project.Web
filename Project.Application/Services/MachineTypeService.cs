using Project.Contracts.ModelDTO;
using Project.Domain.Entities;
using Project.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class MachineTypeService
    {
        private readonly IMachineTypeRepository _repo;
        private readonly IMachineRepository _machineRepo;
        private readonly IBatchRepository _batchRepo;
        private readonly ILogger<MachineTypeService> _logger;

        public MachineTypeService(
            IMachineTypeRepository repo,
            IMachineRepository machineRepo,
            IBatchRepository batchRepo,
            ILogger<MachineTypeService> logger)
        {
            _repo = repo;
            _machineRepo = machineRepo;
            _batchRepo = batchRepo;
            _logger = logger;
        }

        /// <summary>
        /// Получение всех типов станков согласно ТЗ
        /// </summary>
        public async Task<List<MachineTypeDto>> GetAllAsync()
        {
            try
            {
                var machineTypes = await _repo.GetAllAsync();
                var result = new List<MachineTypeDto>();

                foreach (var machineType in machineTypes)
                {
                    result.Add(await MapToMachineTypeDtoAsync(machineType));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка типов станков");
                throw;
            }
        }

        /// <summary>
        /// Получение типа станка по ID согласно ТЗ
        /// </summary>
        public async Task<MachineTypeDto?> GetByIdAsync(int id)
        {
            try
            {
                var entity = await _repo.GetByIdAsync(id);
                if (entity == null) return null;

                return await MapToMachineTypeDtoAsync(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении типа станка {MachineTypeId}", id);
                throw;
            }
        }

        /// <summary>
        /// Создание нового типа станка согласно ТЗ
        /// </summary>
        public async Task<int> AddAsync(MachineTypeCreateDto dto)
        {
            try
            {
                // Валидация
                if (string.IsNullOrWhiteSpace(dto.Name))
                    throw new ArgumentException("Название типа станка обязательно");

                // Проверяем уникальность названия
                var existingMachineTypes = await _repo.GetAllAsync();
                if (existingMachineTypes.Any(mt => mt.Name.Equals(dto.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException($"Тип станка с названием '{dto.Name}' уже существует");

                var entity = new MachineType
                {
                    Name = dto.Name.Trim()
                };

                await _repo.AddAsync(entity);

                _logger.LogInformation("Создан тип станка: {MachineTypeName}", entity.Name);

                return entity.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании типа станка {@MachineTypeDto}", dto);
                throw;
            }
        }

        /// <summary>
        /// Обновление типа станка согласно ТЗ
        /// </summary>
        public async Task UpdateAsync(MachineTypeEditDto dto)
        {
            try
            {
                var entity = await _repo.GetByIdAsync(dto.Id);
                if (entity == null)
                    throw new ArgumentException($"Тип станка с ID {dto.Id} не найден");

                // Валидация
                if (string.IsNullOrWhiteSpace(dto.Name))
                    throw new ArgumentException("Название типа станка обязательно");

                // Проверяем уникальность названия (исключая текущий тип)
                var existingMachineTypes = await _repo.GetAllAsync();
                if (existingMachineTypes.Any(mt => mt.Id != dto.Id &&
                    mt.Name.Equals(dto.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException($"Тип станка с названием '{dto.Name}' уже существует");

                var oldName = entity.Name;
                entity.Name = dto.Name.Trim();

                await _repo.UpdateAsync(entity);

                _logger.LogInformation("Обновлен тип станка {MachineTypeId}: '{OldName}' -> '{NewName}'",
                    dto.Id, oldName, entity.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении типа станка {@MachineTypeDto}", dto);
                throw;
            }
        }

        /// <summary>
        /// Удаление типа станка согласно ТЗ
        /// </summary>
        public async Task DeleteAsync(int id)
        {
            try
            {
                var entity = await _repo.GetByIdAsync(id);
                if (entity == null)
                    throw new ArgumentException($"Тип станка с ID {id} не найден");

                // Проверяем, есть ли станки этого типа
                var machines = await _machineRepo.GetMachinesByTypeAsync(id);
                if (machines.Any())
                    throw new InvalidOperationException($"Нельзя удалить тип станка '{entity.Name}' - существуют станки этого типа");

                // Проверяем, используется ли тип в маршрутах
                var hasActiveUsage = await HasActiveUsageAsync(id);
                if (hasActiveUsage)
                    throw new InvalidOperationException($"Нельзя удалить тип станка '{entity.Name}' - он используется в маршрутах производства");

                await _repo.DeleteAsync(id);

                _logger.LogInformation("Удален тип станка {MachineTypeId}: {MachineTypeName}", id, entity.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении типа станка {MachineTypeId}", id);
                throw;
            }
        }

        /// <summary>
        /// Получение статистики использования типа станка
        /// </summary>
        public async Task<MachineTypeUsageStatisticsDto> GetUsageStatisticsAsync(int machineTypeId, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var machineType = await _repo.GetByIdAsync(machineTypeId);
                if (machineType == null)
                    throw new ArgumentException($"Тип станка с ID {machineTypeId} не найден");

                startDate ??= DateTime.Today.AddDays(-30);
                endDate ??= DateTime.Today.AddDays(1);

                // Получаем станки этого типа
                var machines = await _machineRepo.GetMachinesByTypeAsync(machineTypeId);
                var machineIds = machines.Select(m => m.Id).ToList();

                // Получаем статистику выполнения этапов
                var stageHistory = await _batchRepo.GetStageExecutionHistoryAsync(startDate, endDate);
                var relevantStages = stageHistory.Where(s => s.MachineId.HasValue && machineIds.Contains(s.MachineId.Value)).ToList();

                var completedOperations = relevantStages.Count(s => s.Status == StageExecutionStatus.Completed && !s.IsSetup);
                var totalWorkingHours = relevantStages
                    .Where(s => !s.IsSetup && s.ActualWorkingTime.HasValue)
                    .Sum(s => s.ActualWorkingTime!.Value.TotalHours);

                var setupStages = relevantStages.Where(s => s.IsSetup && s.ActualWorkingTime.HasValue).ToList();
                var averageSetupTime = setupStages.Any() ?
                    setupStages.Average(s => s.ActualWorkingTime!.Value.TotalHours) : 0;

                // Получаем этапы в очереди
                var allStages = await _batchRepo.GetAllStageExecutionsAsync();
                var queuedParts = allStages.Count(s => s.Status == StageExecutionStatus.Waiting &&
                                                     s.RouteStage.MachineTypeId == machineTypeId);

                // Расчет средней загруженности
                var totalAvailableHours = machines.Count * (endDate.Value - startDate.Value).TotalHours;
                var averageUtilization = totalAvailableHours > 0 ?
                    (decimal)((totalWorkingHours / totalAvailableHours) * 100) : 0;

                return new MachineTypeUsageStatisticsDto
                {
                    TotalWorkingHours = totalWorkingHours,
                    AverageUtilization = Math.Round(averageUtilization, 2),
                    CompletedOperations = completedOperations,
                    AverageSetupTime = Math.Round(averageSetupTime, 2),
                    QueuedParts = queuedParts
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении статистики использования типа станка {MachineTypeId}", machineTypeId);
                throw;
            }
        }

        #region Приватные методы

        /// <summary>
        /// Маппинг типа станка в DTO
        /// </summary>
        private async Task<MachineTypeDto> MapToMachineTypeDtoAsync(MachineType machineType)
        {
            try
            {
                // Получаем станки этого типа
                var machines = await _machineRepo.GetMachinesByTypeAsync(machineType.Id);

                // Получаем информацию о работоспособности каждого станка асинхронно
                var machineStatuses = new List<bool>();
                foreach (var machine in machines)
                {
                    var isBroken = await IsMachineBroken(machine.Id);
                    machineStatuses.Add(!isBroken);
                }

                var activeMachinesCount = machineStatuses.Count(isActive => isActive);

                // Получаем операции, которые можно выполнять на станках данного типа
                var supportedOperations = await GetSupportedOperationsAsync(machineType.Id);

                // Получаем статистику использования
                var usageStatistics = await GetUsageStatisticsAsync(machineType.Id);

                return new MachineTypeDto
                {
                    Id = machineType.Id,
                    Name = machineType.Name,
                    MachineCount = machines.Count(),
                    ActiveMachineCount = activeMachinesCount,
                    AveragePriority = machines.Any() ? (decimal)machines.Average(m => m.Priority) : 0,
                    SupportedOperations = supportedOperations,
                    UsageStatistics = usageStatistics,
                    CanDelete = await CanDeleteMachineType(machineType.Id),
                    CreatedUtc = DateTime.UtcNow // Если нет поля CreatedUtc в MachineType
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка при маппинге типа станка {MachineTypeId}, используем базовые данные", machineType.Id);

                return new MachineTypeDto
                {
                    Id = machineType.Id,
                    Name = machineType.Name,
                    MachineCount = 0,
                    ActiveMachineCount = 0,
                    AveragePriority = 0,
                    SupportedOperations = new List<string>(),
                    UsageStatistics = new MachineTypeUsageStatisticsDto(),
                    CanDelete = false,
                    CreatedUtc = DateTime.UtcNow
                };
            }
        }


        /// <summary>
        /// Проверка, сломан ли станок (упрощенная версия)
        /// </summary>
        private async Task<bool> IsMachineBroken(int machineId)
        {
            // Пока что считаем все станки рабочими
            // В будущем здесь можно добавить логику проверки состояния станка
            return false;
        }

        /// <summary>
        /// Получение операций, которые можно выполнять на станках данного типа
        /// </summary>
        private async Task<List<string>> GetSupportedOperationsAsync(int machineTypeId)
        {
            try
            {
                // Получаем все этапы выполнения на станках этого типа
                var allStages = await _batchRepo.GetAllStageExecutionsAsync();
                var operations = allStages
                    .Where(s => s.RouteStage.MachineTypeId == machineTypeId && !s.IsSetup)
                    .Select(s => s.RouteStage.Name)
                    .Distinct()
                    .OrderBy(name => name)
                    .ToList();

                return operations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении поддерживаемых операций для типа станка {MachineTypeId}", machineTypeId);
                return new List<string>();
            }
        }

        /// <summary>
        /// Проверка активного использования типа станка
        /// </summary>
        private async Task<bool> HasActiveUsageAsync(int machineTypeId)
        {
            try
            {
                // Проверяем, есть ли активные этапы выполнения на станках этого типа
                var allStages = await _batchRepo.GetAllStageExecutionsAsync();
                return allStages.Any(s => s.RouteStage.MachineTypeId == machineTypeId &&
                                         (s.Status == StageExecutionStatus.InProgress ||
                                          s.Status == StageExecutionStatus.Waiting ||
                                          s.Status == StageExecutionStatus.Pending));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке активного использования типа станка {MachineTypeId}", machineTypeId);
                return true; // В случае ошибки лучше перестраховаться
            }
        }

        /// <summary>
        /// Проверка возможности удаления типа станка
        /// </summary>
        private async Task<bool> CanDeleteMachineType(int machineTypeId)
        {
            try
            {
                // Проверяем, есть ли станки этого типа
                var machines = await _machineRepo.GetMachinesByTypeAsync(machineTypeId);
                if (machines.Any()) return false;

                // Проверяем активное использование
                return !await HasActiveUsageAsync(machineTypeId);
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}