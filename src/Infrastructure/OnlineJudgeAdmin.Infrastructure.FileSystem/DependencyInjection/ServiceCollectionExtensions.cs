using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnlineJudgeAdmin.Core.Domain.Abstractions.FileSystemManager;

namespace OnlineJudgeAdmin.Infrastructure.FileSystem.DependencyInjection
{
    public static class FileSystemManagerCollectionExtensions
    {
        public static IServiceCollection AddFileSystemInfrastructureManager(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IFileSystemManager, FileSystemManager>();
            return services;
        }
    }
}
