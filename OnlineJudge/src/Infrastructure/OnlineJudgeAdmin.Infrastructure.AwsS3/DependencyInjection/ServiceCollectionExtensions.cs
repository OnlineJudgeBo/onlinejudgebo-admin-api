using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScheduleManager.Core.Domain.Abstractions.Infrastructure;
using ScheduleManager.Infrastructure.AwsS3.Implementations;

namespace ScheduleManager.Infrastructure.AwsS3.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAwsS3FileManager(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IAwsS3FileManager, AwsS3Manager>();
            return services;
        }
    }
}
