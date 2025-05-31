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
    public class RouteService
    {
        private readonly IRouteRepository _repo;
        private readonly IDetailRepository _detailRepo;
        private readonly IMachineTypeRepository _machineTypeRepo;
        private readonly IBatchRepository _batchRepo;
        private readonly ILogger<RouteService> _logger;

        public RouteService(
            IRouteRepository repo,
            IDetailRepository detailRepo,
            IMachineTypeRepository machineTypeRepo,
            IBatchRepository batchRepo,
            ILogger<RouteService> logger)
        {
            _repo = repo;
            _detailRepo = detailRepo;
            _machineTypeRepo = machineTypeRepo;
            _batchRepo = batchRepo;
            _logger = logger;
        }

        /// <summary>
        /// Получение всех маршрутов согласно ТЗ
        /// </summary>
        public async Task<List<RouteDto>> GetAllAsync()
        {
            try
            {
                var routes = await _repo.GetAllAsync();
                var result = new List<RouteDto>();

                foreach (var route in routes)
                {
                    result.Add(await MapToRouteDtoAsync(route));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении всех маршрутов");
                throw;
            }
        }

        /// <summary>
        /// Получение маршрута по ID согласно ТЗ
        /// </summary>
        public async Task<RouteDto?> GetByIdAsync(int id)
        {
            try
            {
                var route = await _repo.GetByIdAsync(id);
                if (route == null) return null;

                return await MapToRouteDtoAsync(route);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении маршрута {RouteId}", id);
                throw;
            }
        }

        /// <summary>
        /// Получение маршрута по ID детали согласно ТЗ
        /// </summary>
        public async Task<RouteDto?> GetByDetailIdAsync(int detailId)
        {
            try
            {
                var route = await _repo.GetByDetailIdAsync(detailId);
                if (route == null) return null;

                return await MapToRouteDtoAsync(route);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении маршрута для детали {DetailId}", detailId);
                throw;
            }
        }

        /// <summary>
        /// Создание маршрута согласно ТЗ
        /// </summary>
        public async Task<int> AddAsync(RouteCreateDto dto)
        {
            try
            {
                // Валидация входных данных
                if (dto.Stages == null || !dto.Stages.Any())
                    throw new ArgumentException("Маршрут должен содержать хотя бы один этап");

                // Проверяем существование детали
                var detail = await _detailRepo.GetByIdAsync(dto.DetailId);
                if (detail == null)
                    throw new ArgumentException($"Деталь с ID {dto.DetailId} не найдена");

                // Проверяем, что маршрут для этой детали еще не существует
                var existingRoute = await _repo.GetByDetailIdAsync(dto.DetailId);
                if (existingRoute != null)
                    throw new ArgumentException($"Маршрут для детали '{detail.Name}' уже существует");

                // Валидация этапов
                await ValidateRouteStagesAsync(dto.Stages);

                // Создаем маршрут
                var entity = new Route
                {
                    DetailId = dto.DetailId,
                    Stages = new List<RouteStage>()
                };

                // Создаем этапы маршрута
                foreach (var stageDto in dto.Stages.OrderBy(s => s.Order))
                {
                    var stage = new RouteStage
                    {
                        Order = stageDto.Order,
                        Name = stageDto.Name.Trim(),
                        MachineTypeId = stageDto.MachineTypeId,
                        NormTime = stageDto.NormTime,
                        SetupTime = stageDto.SetupTime,
                        StageType = stageDto.StageType ?? "Operation"
                    };

                    entity.Stages.Add(stage);
                }

                await _repo.AddAsync(entity);

                _logger.LogInformation("Создан маршрут для детали '{DetailName}' с {StageCount} этапами",
                    detail.Name, entity.Stages.Count);

                return entity.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании маршрута для детали {DetailId}", dto.DetailId);
                throw;
            }
        }

        /// <summary>
        /// Обновление маршрута согласно ТЗ
        /// </summary>
        public async Task UpdateAsync(RouteEditDto dto)
        {
            try
            {
                var entity = await _repo.GetByIdAsync(dto.Id);
                if (entity == null)
                    throw new ArgumentException($"Маршрут с ID {dto.Id} не найден");

                // Проверяем, есть ли активные StageExecutions для этого маршрута
                var hasActiveStageExecutions = await HasActiveStageExecutionsAsync(dto.Id);
                if (hasActiveStageExecutions)
                {
                    throw new InvalidOperationException("Нельзя изменить маршрут, так как по нему выполняются или уже выполнены этапы производства. Создайте новый маршрут или дождитесь завершения всех партий.");
                }

                // Валидация входных данных
                if (dto.Stages == null || !dto.Stages.Any())
                    throw new ArgumentException("Маршрут должен содержать хотя бы один этап");

                // Проверяем, что деталь существует, если меняется
                if (entity.DetailId != dto.DetailId)
                {
                    var detail = await _detailRepo.GetByIdAsync(dto.DetailId);
                    if (detail == null)
                        throw new ArgumentException($"Деталь с ID {dto.DetailId} не найдена");

                    var existingRoute = await _repo.GetByDetailIdAsync(dto.DetailId);
                    if (existingRoute != null && existingRoute.Id != dto.Id)
                        throw new ArgumentException($"Маршрут для детали '{detail.Name}' уже существует");

                    entity.DetailId = dto.DetailId;
                }

                // Валидация этапов
                await ValidateRouteStagesForUpdateAsync(dto.Stages);

                // Безопасное обновление этапов
                entity.Stages.Clear();
                foreach (var stageDto in dto.Stages.OrderBy(s => s.Order))
                {
                    var machineType = await _machineTypeRepo.GetByIdAsync(stageDto.MachineTypeId);
                    if (machineType == null)
                        throw new ArgumentException($"Тип станка с ID {stageDto.MachineTypeId} не найден");

                    entity.Stages.Add(new RouteStage
                    {
                        Order = stageDto.Order,
                        Name = stageDto.Name.Trim(),
                        MachineTypeId = stageDto.MachineTypeId,
                        NormTime = stageDto.NormTime,
                        SetupTime = stageDto.SetupTime,
                        StageType = stageDto.StageType ?? "Operation"
                    });
                }

                await _repo.UpdateAsync(entity);

                _logger.LogInformation("Обновлен маршрут {RouteId} с {StageCount} этапами", dto.Id, entity.Stages.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении маршрута {RouteId}", dto.Id);
                throw;
            }
        }

        /// <summary>
        /// Удаление маршрута согласно ТЗ
        /// </summary>
        public async Task DeleteAsync(int id)
        {
            try
            {
                var route = await _repo.GetByIdAsync(id);
                if (route == null)
                    throw new ArgumentException($"Маршрут с ID {id} не найден");

                // Проверяем, есть ли активные StageExecutions для этого маршрута
                var hasActiveStageExecutions = await HasActiveStageExecutionsAsync(id);
                if (hasActiveStageExecutions)
                {
                    throw new InvalidOperationException("Нельзя удалить маршрут, так как по нему выполняются или уже выполнены этапы производства.");
                }

                await _repo.DeleteAsync(id);

                _logger.LogInformation("Удален маршрут {RouteId} для детали {DetailName}",
                    id, route.Detail?.Name ?? "неизвестно");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении маршрута {RouteId}", id);
                throw;
            }
        }

        /// <summary>
        /// Копирование маршрута на другую деталь
        /// </summary>
        public async Task<int> CopyRouteAsync(RouteCopyDto dto)
        {
            try
            {
                var sourceRoute = await _repo.GetByIdAsync(dto.SourceRouteId);
                if (sourceRoute == null)
                    throw new ArgumentException($"Исходный маршрут с ID {dto.SourceRouteId} не найден");

                var targetDetail = await _detailRepo.GetByIdAsync(dto.TargetDetailId);
                if (targetDetail == null)
                    throw new ArgumentException($"Целевая деталь с ID {dto.TargetDetailId} не найдена");

                // Проверяем, что маршрут для целевой детали еще не существует
                var existingRoute = await _repo.GetByDetailIdAsync(dto.TargetDetailId);
                if (existingRoute != null)
                    throw new ArgumentException($"Маршрут для детали '{targetDetail.Name}' уже существует");

                // Создаем новый маршрут на основе исходного
                var newRoute = new Route
                {
                    DetailId = dto.TargetDetailId,
                    Stages = new List<RouteStage>()
                };

                foreach (var sourceStage in sourceRoute.Stages.OrderBy(s => s.Order))
                {
                    var newStage = new RouteStage
                    {
                        Order = sourceStage.Order,
                        Name = sourceStage.Name,
                        MachineTypeId = sourceStage.MachineTypeId,
                        NormTime = sourceStage.NormTime,
                        SetupTime = sourceStage.SetupTime,
                        StageType = sourceStage.StageType
                    };

                    newRoute.Stages.Add(newStage);
                }

                await _repo.AddAsync(newRoute);

                _logger.LogInformation("Скопирован маршрут с детали '{SourceDetail}' на деталь '{TargetDetail}'",
                    sourceRoute.Detail?.Name, targetDetail.Name);

                return newRoute.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при копировании маршрута {SourceRouteId} на деталь {TargetDetailId}",
                    dto.SourceRouteId, dto.TargetDetailId);
                throw;
            }
        }

        #region Приватные методы

        /// <summary>
        /// Маппинг маршрута в DTO
        /// </summary>
        private async Task<RouteDto> MapToRouteDtoAsync(Route route)
        {
            try
            {
                var stages = route.Stages?
                    .OrderBy(s => s.Order)
                    .Select(s => new RouteStageDto
                    {
                        Id = s.Id,
                        Order = s.Order,
                        Name = s.Name,
                        MachineTypeId = s.MachineTypeId,
                        MachineTypeName = s.MachineType?.Name ?? "",
                        NormTime = s.NormTime,
                        SetupTime = s.SetupTime,
                        StageType = s.StageType
                    }).ToList() ?? new List<RouteStageDto>();

                var totalNormTime = stages.Sum(s => s.NormTime);
                var totalSetupTime = stages.Sum(s => s.SetupTime);

                return new RouteDto
                {
                    Id = route.Id,
                    DetailId = route.DetailId,
                    DetailName = route.Detail?.Name ?? "",
                    DetailNumber = route.Detail?.Number ?? "",
                    Stages = stages,
                    TotalNormTimeHours = totalNormTime,
                    TotalSetupTimeHours = totalSetupTime,
                    StageCount = stages.Count,
                    CanEdit = await CanEditRoute(route.Id),
                    CanDelete = await CanDeleteRoute(route.Id),
                    CreatedUtc = DateTime.UtcNow, // Если нет поля в Route
                    LastModifiedUtc = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при маппинге маршрута {RouteId}", route.Id);
                throw;
            }
        }

        /// <summary>
        /// Валидация этапов маршрута
        /// </summary>
        private async Task ValidateRouteStagesAsync(List<RouteStageCreateDto> stages)
        {
            // Проверяем порядковые номера
            var orders = stages.Select(s => s.Order).ToList();
            if (orders.Distinct().Count() != orders.Count)
                throw new ArgumentException("Порядковые номера этапов должны быть уникальными");

            if (orders.Any(o => o <= 0))
                throw new ArgumentException("Порядковые номера этапов должны быть больше 0");

            // Проверяем времена
            if (stages.Any(s => s.NormTime <= 0))
                throw new ArgumentException("Нормативное время этапов должно быть больше 0");

            if (stages.Any(s => s.SetupTime < 0))
                throw new ArgumentException("Время переналадки не может быть отрицательным");

            // Проверяем существование типов станков
            foreach (var stage in stages)
            {
                var machineType = await _machineTypeRepo.GetByIdAsync(stage.MachineTypeId);
                if (machineType == null)
                    throw new ArgumentException($"Тип станка с ID {stage.MachineTypeId} не найден для этапа '{stage.Name}'");
            }

            // Проверяем названия этапов
            if (stages.Any(s => string.IsNullOrWhiteSpace(s.Name)))
                throw new ArgumentException("Все этапы должны иметь название");
        }

        /// <summary>
        /// Валидация этапов маршрута для обновления
        /// </summary>
        private async Task ValidateRouteStagesForUpdateAsync(List<RouteStageEditDto> stages)
        {
            // Проверяем порядковые номера
            var orders = stages.Select(s => s.Order).ToList();
            if (orders.Distinct().Count() != orders.Count)
                throw new ArgumentException("Порядковые номера этапов должны быть уникальными");

            if (orders.Any(o => o <= 0))
                throw new ArgumentException("Порядковые номера этапов должны быть больше 0");

            // Проверяем времена
            if (stages.Any(s => s.NormTime <= 0))
                throw new ArgumentException("Нормативное время этапов должно быть больше 0");

            if (stages.Any(s => s.SetupTime < 0))
                throw new ArgumentException("Время переналадки не может быть отрицательным");

            // Проверяем существование типов станков
            foreach (var stage in stages)
            {
                var machineType = await _machineTypeRepo.GetByIdAsync(stage.MachineTypeId);
                if (machineType == null)
                    throw new ArgumentException($"Тип станка с ID {stage.MachineTypeId} не найден для этапа '{stage.Name}'");
            }

            // Проверяем названия этапов
            if (stages.Any(s => string.IsNullOrWhiteSpace(s.Name)))
                throw new ArgumentException("Все этапы должны иметь название");
        }

        /// <summary>
        /// Проверяет, есть ли активные или завершенные этапы по данному маршруту
        /// </summary>
        private async Task<bool> HasActiveStageExecutionsAsync(int routeId)
        {
            try
            {
                var allStageExecutions = await _batchRepo.GetAllStageExecutionsAsync();

                // Проверяем, есть ли этапы выполнения, которые ссылаются на этапы данного маршрута
                var route = await _repo.GetByIdAsync(routeId);
                if (route == null) return false;

                var routeStageIds = route.Stages.Select(s => s.Id).ToList();

                return allStageExecutions.Any(se =>
                    routeStageIds.Contains(se.RouteStageId) &&
                    (se.Status == StageExecutionStatus.InProgress ||
                     se.Status == StageExecutionStatus.Completed ||
                     se.Status == StageExecutionStatus.Paused ||
                     se.Status == StageExecutionStatus.Waiting));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке активных этапов для маршрута {RouteId}", routeId);
                // В случае ошибки лучше перестраховаться
                return true;
            }
        }

        /// <summary>
        /// Проверка возможности редактирования маршрута
        /// </summary>
        private async Task<bool> CanEditRoute(int routeId)
        {
            return !await HasActiveStageExecutionsAsync(routeId);
        }

        /// <summary>
        /// Проверка возможности удаления маршрута
        /// </summary>
        private async Task<bool> CanDeleteRoute(int routeId)
        {
            return !await HasActiveStageExecutionsAsync(routeId);
        }

        #endregion
    }
}