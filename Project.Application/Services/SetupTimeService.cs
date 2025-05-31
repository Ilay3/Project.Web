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
    public class SetupTimeService
    {
        private readonly ISetupTimeRepository _repo;
        private readonly IMachineRepository _machineRepo;
        private readonly IDetailRepository _detailRepo;
        private readonly IBatchRepository _batchRepo;
        private readonly ILogger<SetupTimeService> _logger;

        public SetupTimeService(
            ISetupTimeRepository repo,
            IMachineRepository machineRepo,
            IDetailRepository detailRepo,
            IBatchRepository batchRepo,
            ILogger<SetupTimeService> logger)
        {
            _repo = repo;
            _machineRepo = machineRepo;
            _detailRepo = detailRepo;
            _batchRepo = batchRepo;
            _logger = logger;
        }

        /// <summary>
        /// Получение всех записей о переналадке согласно ТЗ
        /// </summary>
        public async Task<List<SetupTimeDto>> GetAllAsync()
        {
            try
            {
                var entities = await _repo.GetAllAsync();
                var result = new List<SetupTimeDto>();

                foreach (var entity in entities)
                {
                    result.Add(await MapToSetupTimeDtoAsync(entity));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении всех записей времени переналадки");
                throw;
            }
        }

        /// <summary>
        /// Получение записи времени переналадки по ID
        /// </summary>
        public async Task<SetupTimeDto?> GetByIdAsync(int id)
        {
            try
            {
                var entity = await _repo.GetByIdAsync(id);
                if (entity == null) return null;

                return await MapToSetupTimeDtoAsync(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении времени переналадки {SetupTimeId}", id);
                throw;
            }
        }

        /// <summary>
        /// Получение времени переналадки с логированием согласно ТЗ
        /// </summary>
        public async Task<double> GetSetupTimeAsync(int machineId, int fromDetailId, int toDetailId)
        {
            try
            {
                // Если это одна и та же деталь, переналадка не нужна
                if (fromDetailId == toDetailId)
                {
                    _logger.LogDebug("Переналадка не требуется: одна и та же деталь {DetailId} на станке {MachineId}",
                        toDetailId, machineId);
                    return 0;
                }

                var setupTime = await _repo.GetSetupTimeAsync(machineId, fromDetailId, toDetailId);

                if (setupTime != null)
                {
                    _logger.LogDebug("Найдено время переналадки: станок {MachineId}, {FromDetailId} -> {ToDetailId} = {Time}ч",
                        machineId, fromDetailId, toDetailId, setupTime.Time);
                    return setupTime.Time;
                }

                // Если нет конкретной записи, возвращаем стандартное время
                var machine = await _machineRepo.GetByIdAsync(machineId);
                if (machine == null)
                    throw new Exception($"Станок с ID {machineId} не найден");

                // Стандартное время переналадки - 0.5 часа (30 минут) согласно ТЗ
                const double defaultSetupTime = 0.5;

                _logger.LogDebug("Время переналадки не найдено, используется стандартное: станок {MachineId}, {FromDetailId} -> {ToDetailId} = {Time}ч",
                    machineId, fromDetailId, toDetailId, defaultSetupTime);

                return defaultSetupTime;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении времени переналадки: станок {MachineId}, {FromDetailId} -> {ToDetailId}",
                    machineId, fromDetailId, toDetailId);
                return 0.5; // Возвращаем стандартное время при ошибке
            }
        }

        /// <summary>
        /// Добавление записи о времени переналадки с валидацией согласно ТЗ
        /// </summary>
        public async Task<int> AddAsync(SetupTimeCreateDto dto)
        {
            try
            {
                // Валидация входных данных
                if (dto.MachineId <= 0)
                    throw new ArgumentException("Не указан станок");

                if (dto.FromDetailId <= 0 || dto.ToDetailId <= 0)
                    throw new ArgumentException("Не указаны детали");

                if (dto.FromDetailId == dto.ToDetailId)
                    throw new ArgumentException("Деталь 'откуда' и деталь 'куда' не могут быть одинаковыми");

                if (dto.Time < 0)
                    throw new ArgumentException("Время переналадки не может быть отрицательным");

                // Проверяем существование станка
                var machine = await _machineRepo.GetByIdAsync(dto.MachineId);
                if (machine == null)
                    throw new ArgumentException($"Станок с ID {dto.MachineId} не найден");

                // Проверяем существование деталей
                var fromDetail = await _detailRepo.GetByIdAsync(dto.FromDetailId);
                if (fromDetail == null)
                    throw new ArgumentException($"Деталь 'откуда' с ID {dto.FromDetailId} не найдена");

                var toDetail = await _detailRepo.GetByIdAsync(dto.ToDetailId);
                if (toDetail == null)
                    throw new ArgumentException($"Деталь 'куда' с ID {dto.ToDetailId} не найдена");

                // Проверяем, что запись для такой комбинации еще не существует
                var existing = await _repo.GetSetupTimeAsync(dto.MachineId, dto.FromDetailId, dto.ToDetailId);
                if (existing != null)
                    throw new ArgumentException($"Время переналадки для станка '{machine.Name}' с детали '{fromDetail.Name}' на деталь '{toDetail.Name}' уже существует");

                var entity = new SetupTime
                {
                    MachineId = dto.MachineId,
                    FromDetailId = dto.FromDetailId,
                    ToDetailId = dto.ToDetailId,
                    Time = dto.Time
                };

                await _repo.AddAsync(entity);

                _logger.LogInformation("Добавлено время переналадки: станок '{MachineName}' ({MachineId}), '{FromDetailName}' -> '{ToDetailName}', время: {Time}ч",
                    machine.Name, dto.MachineId, fromDetail.Name, toDetail.Name, dto.Time);

                return entity.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении времени переналадки: {@dto}", dto);
                throw;
            }
        }

        /// <summary>
        /// Обновление записи о времени переналадки согласно ТЗ
        /// </summary>
        public async Task UpdateAsync(SetupTimeEditDto dto)
        {
            try
            {
                var entity = await _repo.GetByIdAsync(dto.Id);
                if (entity == null)
                    throw new ArgumentException($"Запись времени переналадки с ID {dto.Id} не найдена");

                // Валидация как при добавлении
                if (dto.Time < 0)
                    throw new ArgumentException("Время переналадки не может быть отрицательным");

                if (dto.FromDetailId == dto.ToDetailId)
                    throw new ArgumentException("Деталь 'откуда' и деталь 'куда' не могут быть одинаковыми");

                // Проверяем существование станка
                var machine = await _machineRepo.GetByIdAsync(dto.MachineId);
                if (machine == null)
                    throw new ArgumentException($"Станок с ID {dto.MachineId} не найден");

                // Проверяем существование деталей
                var fromDetail = await _detailRepo.GetByIdAsync(dto.FromDetailId);
                if (fromDetail == null)
                    throw new ArgumentException($"Деталь 'откуда' с ID {dto.FromDetailId} не найдена");

                var toDetail = await _detailRepo.GetByIdAsync(dto.ToDetailId);
                if (toDetail == null)
                    throw new ArgumentException($"Деталь 'куда' с ID {dto.ToDetailId} не найдена");

                // Проверяем, что запись для такой комбинации не существует (исключая текущую)
                var existing = await _repo.GetSetupTimeAsync(dto.MachineId, dto.FromDetailId, dto.ToDetailId);
                if (existing != null && existing.Id != dto.Id)
                    throw new ArgumentException($"Время переналадки для станка '{machine.Name}' с детали '{fromDetail.Name}' на деталь '{toDetail.Name}' уже существует");

                entity.MachineId = dto.MachineId;
                entity.FromDetailId = dto.FromDetailId;
                entity.ToDetailId = dto.ToDetailId;
                entity.Time = dto.Time;

                await _repo.UpdateAsync(entity);

                _logger.LogInformation("Обновлено время переналадки ID {Id}: станок '{MachineName}', '{FromDetailName}' -> '{ToDetailName}', время = {Time}ч",
                    dto.Id, machine.Name, fromDetail.Name, toDetail.Name, dto.Time);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении времени переналадки: {@dto}", dto);
                throw;
            }
        }

        /// <summary>
        /// Удаление записи о времени переналадки согласно ТЗ
        /// </summary>
        public async Task DeleteAsync(int id)
        {
            try
            {
                var entity = await _repo.GetByIdAsync(id);
                if (entity == null)
                    throw new ArgumentException($"Запись времени переналадки с ID {id} не найдена");

                await _repo.DeleteAsync(id);

                _logger.LogInformation("Удалено время переналадки ID {Id}: станок {MachineId}, {FromDetailId} -> {ToDetailId}",
                    id, entity.MachineId, entity.FromDetailId, entity.ToDetailId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении времени переналадки {SetupTimeId}", id);
                throw;
            }
        }

        /// <summary>
        /// Проверка необходимости переналадки и получение времени согласно ТЗ
        /// </summary>
        public async Task<SetupInfoDto> CheckSetupNeededAsync(int machineId, int detailId)
        {
            try
            {
                // Получаем последнюю обработанную деталь на станке
                var lastDetailOnMachine = await _repo.GetLastDetailOnMachineAsync(machineId);

                // Если это первая деталь или та же самая, переналадка не нужна
                if (lastDetailOnMachine == null || lastDetailOnMachine.Id == detailId)
                {
                    return new SetupInfoDto
                    {
                        SetupNeeded = false,
                        FromDetailId = null,
                        FromDetailName = null,
                        FromDetailNumber = null,
                        ToDetailId = detailId,
                        SetupTime = 0
                    };
                }

                // Получаем время переналадки
                double setupTime = await GetSetupTimeAsync(machineId, lastDetailOnMachine.Id, detailId);

                // Получаем информацию о целевой детали
                var toDetail = await _detailRepo.GetByIdAsync(detailId);

                return new SetupInfoDto
                {
                    SetupNeeded = true,
                    FromDetailId = lastDetailOnMachine.Id,
                    FromDetailName = lastDetailOnMachine.Name,
                    FromDetailNumber = lastDetailOnMachine.Number,
                    ToDetailId = detailId,
                    ToDetailName = toDetail?.Name ?? "",
                    ToDetailNumber = toDetail?.Number ?? "",
                    SetupTime = setupTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке необходимости переналадки для станка {MachineId}, деталь {DetailId}",
                    machineId, detailId);

                // В случае ошибки считаем, что переналадка нужна
                return new SetupInfoDto
                {
                    SetupNeeded = true,
                    FromDetailId = null,
                    FromDetailName = "Неизвестно",
                    FromDetailNumber = "Неизвестно",
                    ToDetailId = detailId,
                    ToDetailName = "Неизвестно",
                    ToDetailNumber = "Неизвестно",
                    SetupTime = 0.5 // Стандартное время
                };
            }
        }

        /// <summary>
        /// Массовое добавление времен переналадки согласно ТЗ
        /// </summary>
        public async Task<BulkSetupTimeResultDto> BulkAddSetupTimesAsync(BulkSetupTimeImportDto dto)
        {
            var result = new BulkSetupTimeResultDto();

            foreach (var setupTime in dto.SetupTimes)
            {
                try
                {
                    // Если разрешена перезапись существующих записей
                    if (dto.OverwriteExisting)
                    {
                        var existing = await _repo.GetSetupTimeAsync(setupTime.MachineId, setupTime.FromDetailId, setupTime.ToDetailId);
                        if (existing != null)
                        {
                            var editDto = new SetupTimeEditDto
                            {
                                Id = existing.Id,
                                MachineId = setupTime.MachineId,
                                FromDetailId = setupTime.FromDetailId,
                                ToDetailId = setupTime.ToDetailId,
                                Time = setupTime.Time,
                                SetupDescription = setupTime.SetupDescription,
                                RequiredSkills = setupTime.RequiredSkills,
                                RequiredTools = setupTime.RequiredTools
                            };

                            await UpdateAsync(editDto);
                            result.SuccessCount++;
                            continue;
                        }
                    }

                    await AddAsync(setupTime);
                    result.SuccessCount++;
                }
                catch (ArgumentException ex) when (ex.Message.Contains("уже существует"))
                {
                    result.SkippedCount++;
                    result.Warnings.Add($"Станок {setupTime.MachineId}, {setupTime.FromDetailId}->{setupTime.ToDetailId}: запись уже существует");
                }
                catch (Exception ex)
                {
                    result.FailureCount++;
                    result.Errors.Add($"Станок {setupTime.MachineId}, {setupTime.FromDetailId}->{setupTime.ToDetailId}: {ex.Message}");
                }
            }

            _logger.LogInformation("Массовое добавление времен переналадки завершено. Успешно: {Success}, Ошибки: {Failures}, Пропущено: {Skipped}",
                result.SuccessCount, result.FailureCount, result.SkippedCount);

            return result;
        }

        /// <summary>
        /// Получение времен переналадки для станка
        /// </summary>
        public async Task<List<SetupTimeDto>> GetSetupTimesForMachineAsync(int machineId)
        {
            try
            {
                var setupTimes = await _repo.GetAllAsync();
                var machineSetupTimes = setupTimes.Where(st => st.MachineId == machineId).ToList();

                var result = new List<SetupTimeDto>();
                foreach (var setupTime in machineSetupTimes)
                {
                    result.Add(await MapToSetupTimeDtoAsync(setupTime));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении времен переналадки для станка {MachineId}", machineId);
                throw;
            }
        }

        /// <summary>
        /// Получение времен переналадки для детали
        /// </summary>
        public async Task<List<SetupTimeDto>> GetSetupTimesForDetailAsync(int detailId)
        {
            try
            {
                var setupTimes = await _repo.GetAllAsync();
                var detailSetupTimes = setupTimes.Where(st => st.FromDetailId == detailId || st.ToDetailId == detailId).ToList();

                var result = new List<SetupTimeDto>();
                foreach (var setupTime in detailSetupTimes)
                {
                    result.Add(await MapToSetupTimeDtoAsync(setupTime));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении времен переналадки для детали {DetailId}", detailId);
                throw;
            }
        }

        #region Приватные методы

        /// <summary>
        /// Маппинг записи времени переналадки в DTO
        /// </summary>
        private async Task<SetupTimeDto> MapToSetupTimeDtoAsync(SetupTime setupTime)
        {
            try
            {
                // Получаем статистику использования данной переналадки
                var usageCount = await GetSetupUsageCountAsync(setupTime.MachineId, setupTime.FromDetailId, setupTime.ToDetailId);
                var lastUsedDate = await GetLastSetupUsageAsync(setupTime.MachineId, setupTime.FromDetailId, setupTime.ToDetailId);
                var averageActualTime = await GetAverageActualSetupTimeAsync(setupTime.MachineId, setupTime.FromDetailId, setupTime.ToDetailId);

                return new SetupTimeDto
                {
                    Id = setupTime.Id,
                    MachineId = setupTime.MachineId,
                    MachineName = setupTime.Machine?.Name ?? "",
                    FromDetailId = setupTime.FromDetailId,
                    FromDetailName = setupTime.FromDetail?.Name ?? "",
                    FromDetailNumber = setupTime.FromDetail?.Number ?? "",
                    ToDetailId = setupTime.ToDetailId,
                    ToDetailName = setupTime.ToDetail?.Name ?? "",
                    ToDetailNumber = setupTime.ToDetail?.Number ?? "",
                    Time = setupTime.Time,
                    CreatedUtc = DateTime.UtcNow, // Если нет поля CreatedUtc в SetupTime
                    LastUsedUtc = lastUsedDate,
                    UsageCount = usageCount,
                    AverageActualTime = averageActualTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка при маппинге времени переналадки {SetupTimeId}, используем базовые данные", setupTime.Id);

                return new SetupTimeDto
                {
                    Id = setupTime.Id,
                    MachineId = setupTime.MachineId,
                    MachineName = setupTime.Machine?.Name ?? "",
                    FromDetailId = setupTime.FromDetailId,
                    FromDetailName = setupTime.FromDetail?.Name ?? "",
                    FromDetailNumber = setupTime.FromDetail?.Number ?? "",
                    ToDetailId = setupTime.ToDetailId,
                    ToDetailName = setupTime.ToDetail?.Name ?? "",
                    ToDetailNumber = setupTime.ToDetail?.Number ?? "",
                    Time = setupTime.Time,
                    CreatedUtc = DateTime.UtcNow,
                    UsageCount = 0
                };
            }
        }

        /// <summary>
        /// Получение количества использований переналадки
        /// </summary>
        private async Task<int> GetSetupUsageCountAsync(int machineId, int fromDetailId, int toDetailId)
        {
            try
            {
                var allStages = await _batchRepo.GetAllStageExecutionsAsync();

                // Подсчитываем количество выполненных переналадок с данной комбинации
                return allStages.Count(s => s.IsSetup && s.MachineId == machineId &&
                                           s.Status == StageExecutionStatus.Completed);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Получение даты последнего использования переналадки
        /// </summary>
        private async Task<DateTime?> GetLastSetupUsageAsync(int machineId, int fromDetailId, int toDetailId)
        {
            try
            {
                var allStages = await _batchRepo.GetAllStageExecutionsAsync();

                var lastSetupStage = allStages
                    .Where(s => s.IsSetup && s.MachineId == machineId &&
                               s.Status == StageExecutionStatus.Completed)
                    .OrderByDescending(s => s.EndTimeUtc)
                    .FirstOrDefault();

                return lastSetupStage?.EndTimeUtc;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Получение среднего фактического времени переналадки
        /// </summary>
        private async Task<double?> GetAverageActualSetupTimeAsync(int machineId, int fromDetailId, int toDetailId)
        {
            try
            {
                var allStages = await _batchRepo.GetAllStageExecutionsAsync();

                var setupStages = allStages
                    .Where(s => s.IsSetup && s.MachineId == machineId &&
                               s.Status == StageExecutionStatus.Completed &&
                               s.ActualWorkingTime.HasValue)
                    .ToList();

                if (!setupStages.Any()) return null;

                return setupStages.Average(s => s.ActualWorkingTime!.Value.TotalHours);
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}