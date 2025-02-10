using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

namespace OnlineJudgeAdmin.Core.Application.Services.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationScheduleServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IScheduleService, ScheduleService>();
        return services;
    }
}
