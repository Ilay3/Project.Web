using Project.Contracts.ModelDTO;
using Project.Domain.Entities;
using Project.Domain.Repositories;
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

        public SetupTimeService(
            ISetupTimeRepository repo,
            IMachineRepository machineRepo,
            IDetailRepository detailRepo)
        {
            _repo = repo;
            _machineRepo = machineRepo;
            _detailRepo = detailRepo;
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

        // Получение времени переналадки для конкретной пары деталей на станке
        public async Task<double> GetSetupTimeAsync(int machineId, int fromDetailId, int toDetailId)
        {
            // Если это одна и та же деталь, переналадка не нужна
            if (fromDetailId == toDetailId)
                return 0;

            var setupTime = await _repo.GetSetupTimeAsync(machineId, fromDetailId, toDetailId);

            if (setupTime != null)
                return setupTime.Time;

            // Если нет конкретной записи, пробуем найти стандартное время переналадки
            // для типа станка и детали
            var machine = await _machineRepo.GetByIdAsync(machineId);
            if (machine == null)
                throw new Exception($"Machine with id {machineId} not found");

            // Тут можно реализовать логику поиска стандартного времени
            // для типа станка и типа деталей, но пока просто возвращаем
            // стандартное значение
            return 0.5; // 30 минут по умолчанию
        }

        // Добавление записи о времени переналадки
        public async Task AddAsync(SetupTimeDto dto)
        {
            // Проверяем, что запись для такой комбинации еще не существует
            var existing = await _repo.GetSetupTimeAsync(dto.MachineId, dto.FromDetailId, dto.ToDetailId);
            if (existing != null)
                throw new Exception("Setup time record for this combination already exists");

            var entity = new SetupTime
            {
                MachineId = dto.MachineId,
                FromDetailId = dto.FromDetailId,
                ToDetailId = dto.ToDetailId,
                Time = dto.Time
            };

            await _repo.AddAsync(entity);
        }

        // Обновление записи о времени переналадки
        public async Task UpdateAsync(SetupTimeDto dto)
        {
            var entity = await _repo.GetByIdAsync(dto.Id);
            if (entity == null)
                throw new Exception($"Setup time with id {dto.Id} not found");

            entity.MachineId = dto.MachineId;
            entity.FromDetailId = dto.FromDetailId;
            entity.ToDetailId = dto.ToDetailId;
            entity.Time = dto.Time;

            await _repo.UpdateAsync(entity);
        }

        // Удаление записи о времени переналадки
        public async Task DeleteAsync(int id)
        {
            await _repo.DeleteAsync(id);
        }

        // Проверка необходимости переналадки и получение времени
        public async Task<SetupInfoDto> CheckSetupNeededAsync(int machineId, int detailId)
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
    }

   
}