using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Project.Domain.Repositories;
using Project.Infrastructure.Data;
using Project.Infrastructure.Repositories;

namespace Project.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // Настройка DbContext
            services.AddDbContext<ManufacturingDbContext>(options =>
                options.UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    b => b.MigrationsAssembly(typeof(ManufacturingDbContext).Assembly.FullName)));

            // Регистрация репозиториев
            services.AddScoped<IDetailRepository, DetailRepository>();
            services.AddScoped<IMachineTypeRepository, MachineTypeRepository>();
            services.AddScoped<IMachineRepository, MachineRepository>();
            services.AddScoped<IRouteRepository, RouteRepository>();
            services.AddScoped<IBatchRepository, BatchRepository>();
            services.AddScoped<ISetupTimeRepository, SetupTimeRepository>();
            services.AddScoped<IEventRepository, EventRepository>();

            return services;
        }
    }
}