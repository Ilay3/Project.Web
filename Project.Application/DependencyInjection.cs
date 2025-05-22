using Microsoft.Extensions.DependencyInjection;
using Project.Application.BackgroundServices;
using Project.Application.Services;
using Project.Domain.Repositories;

namespace Project.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            // Регистрация сервисов приложения
            services.AddScoped<DetailService>();
            services.AddScoped<MachineTypeService>();
            services.AddScoped<MachineService>();
            services.AddScoped<RouteService>();
            services.AddScoped<BatchService>();
            services.AddScoped<StageExecutionService>();
            services.AddScoped<HistoryService>();
            services.AddScoped<SetupTimeService>();
            services.AddScoped<PlanningService>();
            services.AddScoped<ProductionSchedulerService>();
            services.AddScoped<EventLogService>();

            // Регистрация фоновых служб
            services.AddHostedService<ProductionSchedulerBackgroundService>();

            return services;
        }
    }
}