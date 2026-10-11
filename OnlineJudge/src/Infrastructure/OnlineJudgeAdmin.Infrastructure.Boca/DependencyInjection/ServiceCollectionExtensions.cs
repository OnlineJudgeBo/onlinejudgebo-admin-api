using Microsoft.Extensions.DependencyInjection;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

namespace OnlineJudgeAdmin.Infrastructure.Boca.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBocaInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IBocaPackageStaging, BocaPackageStaging>();
        return services;
    }
}
