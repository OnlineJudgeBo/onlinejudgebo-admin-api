using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Infrastructure.AwsS3.Implementations;

namespace OnlineJudgeAdmin.Infrastructure.AwsS3.DependencyInjection
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
