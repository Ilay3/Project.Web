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
        private readonly ILogger<SetupTimeService> _logger;

        public SetupTimeService(
            ISetupTimeRepository repo,
            IMachineRepository machineRepo,
            IDetailRepository detailRepo,
            ILogger<SetupTimeService> logger)
        {
            _repo = repo;
            _machineRepo = machineRepo;
            _detailRepo = detailRepo;
            _logger = logger;
        }

        // Получение всех записей о переналадке
        public async Task<List<SetupTimeDto>> GetAllAsync()
        {
            var entities = await _repo.GetAllAsync();
            return entities.Select(e => new SetupTimeDto
            {
                Id = e.Id,
                MachineId = e.MachineId,
                FromDetailId = e.FromDetailId,
                ToDetailId = e.ToDetailId,
                Time = e.Time
            }).ToList();
        }

        /// <summary>
        /// ИСПРАВЛЕННОЕ получение времени переналадки с логированием
        /// </summary>
        public async Task<double> GetSetupTimeAsync(int machineId, int fromDetailId, int toDetailId)
        {
            try
            {
                // Если это одна и та же деталь, переналадка не нужна
                if (fromDetailId == toDetailId)
                    return 0;

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
                    throw new Exception($"Machine with id {machineId} not found");

                // Стандартное время переналадки - 0.5 часа (30 минут)
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
        /// ИСПРАВЛЕННОЕ добавление записи о времени переналадки с валидацией
        /// </summary>
        public async Task AddAsync(SetupTimeDto dto)
        {
            try
            {
                // Валидация входных данных
                if (dto.MachineId <= 0)
                    throw new Exception("Не указан станок");

                if (dto.FromDetailId <= 0 || dto.ToDetailId <= 0)
                    throw new Exception("Не указаны детали");

                if (dto.FromDetailId == dto.ToDetailId)
                    throw new Exception("Деталь 'откуда' и деталь 'куда' не могут быть одинаковыми");

                if (dto.Time < 0)
                    throw new Exception("Время переналадки не может быть отрицательным");

                // Проверяем существование станка
                var machine = await _machineRepo.GetByIdAsync(dto.MachineId);
                if (machine == null)
                    throw new Exception($"Станок с ID {dto.MachineId} не найден");

                // Проверяем существование деталей
                var fromDetail = await _detailRepo.GetByIdAsync(dto.FromDetailId);
                if (fromDetail == null)
                    throw new Exception($"Деталь 'откуда' с ID {dto.FromDetailId} не найдена");

                var toDetail = await _detailRepo.GetByIdAsync(dto.ToDetailId);
                if (toDetail == null)
                    throw new Exception($"Деталь 'куда' с ID {dto.ToDetailId} не найдена");

                // Проверяем, что запись для такой комбинации еще не существует
                var existing = await _repo.GetSetupTimeAsync(dto.MachineId, dto.FromDetailId, dto.ToDetailId);
                if (existing != null)
                    throw new Exception($"Время переналадки для станка '{machine.Name}' с детали '{fromDetail.Name}' на деталь '{toDetail.Name}' уже существует");

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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении времени переналадки: {@dto}", dto);
                throw;
            }
        }

        // Обновление записи о времени переналадки
        public async Task UpdateAsync(SetupTimeDto dto)
        {
            var entity = await _repo.GetByIdAsync(dto.Id);
            if (entity == null)
                throw new Exception($"Setup time with id {dto.Id} not found");

            // Валидация как при добавлении
            if (dto.Time < 0)
                throw new Exception("Время переналадки не может быть отрицательным");

            if (dto.FromDetailId == dto.ToDetailId)
                throw new Exception("Деталь 'откуда' и деталь 'куда' не могут быть одинаковыми");

            entity.MachineId = dto.MachineId;
            entity.FromDetailId = dto.FromDetailId;
            entity.ToDetailId = dto.ToDetailId;
            entity.Time = dto.Time;

            await _repo.UpdateAsync(entity);

            _logger.LogInformation("Обновлено время переналадки ID {Id}: время = {Time}ч", dto.Id, dto.Time);
        }

        // Удаление записи о времени переналадки
        public async Task DeleteAsync(int id)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null)
                throw new Exception($"Setup time with id {id} not found");

            await _repo.DeleteAsync(id);
            _logger.LogInformation("Удалено время переналадки ID {Id}", id);
        }

        /// <summary>
        /// ИСПРАВЛЕННАЯ проверка необходимости переналадки и получение времени
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
                        ToDetailId = detailId,
                        SetupTime = 0
                    };
                }

                // Получаем время переналадки
                double setupTime = await GetSetupTimeAsync(machineId, lastDetailOnMachine.Id, detailId);

                return new SetupInfoDto
                {
                    SetupNeeded = true,
                    FromDetailId = lastDetailOnMachine.Id,
                    FromDetailName = lastDetailOnMachine.Name,
                    ToDetailId = detailId,
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
                    ToDetailId = detailId,
                    SetupTime = 0.5 // Стандартное время
                };
            }
        }

        /// <summary>
        /// Массовое добавление времен переналадки
        /// </summary>
        public async Task<BulkSetupTimeResult> BulkAddSetupTimesAsync(List<SetupTimeDto> setupTimes)
        {
            var result = new BulkSetupTimeResult();

            foreach (var setupTime in setupTimes)
            {
                try
                {
                    await AddAsync(setupTime);
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.FailureCount++;
                    result.Errors.Add($"Станок {setupTime.MachineId}, {setupTime.FromDetailId}->{setupTime.ToDetailId}: {ex.Message}");
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Результат массового добавления времен переналадки
    /// </summary>
    public class BulkSetupTimeResult
    {
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }
}