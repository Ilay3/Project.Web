using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Project.Application.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Project.Application.BackgroundServices
{
    /// <summary>
    /// Фоновая служба для автоматического управления производственным заданием.
    /// Реализует требования из дополнительного ТЗ по автоматическому запуску этапов
    /// </summary>
    public class ProductionSchedulerBackgroundService : BackgroundService
    {
        private readonly ILogger<ProductionSchedulerBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;

        // Интервал проверки (10 секунд)
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(10);

        public ProductionSchedulerBackgroundService(
            ILogger<ProductionSchedulerBackgroundService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Служба автоматического планирования производства запущена");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessSchedulingAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при выполнении автоматического планирования производства");
                }

                // Ожидание перед следующей проверкой
                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("Служба автоматического планирования производства остановлена");
        }

        private async Task ProcessSchedulingAsync()
        {
            // Создаем область для сервисов, чтобы не держать их на протяжении всего времени работы
            using var scope = _serviceProvider.CreateScope();

            // Получаем необходимые сервисы
            var schedulerService = scope.ServiceProvider.GetRequiredService<ProductionSchedulerService>();
            var batchRepo = scope.ServiceProvider.GetRequiredService<Domain.Repositories.IBatchRepository>();

            // 1. Проверяем этапы, ожидающие в очереди
            var stagesInQueue = await batchRepo.GetAllStagesInQueueAsync();
            foreach (var stage in stagesInQueue)
            {
                _logger.LogInformation("Планирование этапа в очереди: {StageId}", stage.Id);
                await schedulerService.ScheduleStageExecutionAsync(stage.Id);
            }

            // 2. Проверяем новые этапы, готовые к запуску
            var pendingStages = await batchRepo.GetPendingStagesAsync();
            foreach (var stage in pendingStages)
            {
                // Проверяем, все ли предыдущие этапы завершены
                bool canStart = await schedulerService.CanStartStageAsync(stage.Id);

                if (canStart)
                {
                    _logger.LogInformation("Автоматический запуск готового этапа: {StageId}", stage.Id);

                    // Если этап назначен на станок, запускаем его
                    if (stage.MachineId.HasValue)
                    {
                        await schedulerService.StartPendingStageAsync(stage.Id);
                    }
                    // Иначе сначала назначаем на подходящий станок
                    else
                    {
                        await schedulerService.ScheduleStageExecutionAsync(stage.Id);
                    }
                }
            }

            // 3. Обрабатываем завершенные этапы, запуская следующие в очереди
            var completedStages = await batchRepo.GetRecentlyCompletedStagesAsync();
            foreach (var stage in completedStages)
            {
                _logger.LogInformation("Обработка завершенного этапа: {StageId}", stage.Id);
                await schedulerService.HandleStageCompletionAsync(stage.Id);
            }
        }
    }
}