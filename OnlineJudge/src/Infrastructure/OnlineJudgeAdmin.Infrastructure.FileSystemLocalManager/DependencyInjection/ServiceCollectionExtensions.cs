using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScheduleManager.Core.Domain.Abstractions.Infrastructure;

namespace ScheduleManager.Infrastructure.FileSystemLocalManager.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddFileSystemLocalManagerInfrastructureManager(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IFileSystemLocalManagerManager, FileSystemLocalManagerManager>();
            return services;
        }
    }
}
