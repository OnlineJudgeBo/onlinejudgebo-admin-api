using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScheduleManager.Core.Application.Services.Implementations;
using ScheduleManager.Core.Domain.Abstractions.Services;

namespace ScheduleManager.Core.Application.Services.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationScheduleServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IScheduleService, ScheduleService>();
        return services;
    }
}
