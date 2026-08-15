using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Infrastructure.IdeIntegration.Implementations;

namespace OnlineJudgeAdmin.Infrastructure.IdeIntegration.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdeIntegrationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IIdeLaunchTokenValidator, IdeLaunchTokenValidator>();
        services.AddScoped<IIdeLaunchTokenIssuer, IdeLaunchTokenIssuer>();
        return services;
    }
}
