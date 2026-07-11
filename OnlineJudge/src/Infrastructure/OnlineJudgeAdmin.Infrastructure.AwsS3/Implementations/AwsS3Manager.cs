using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

namespace OnlineJudgeAdmin.Infrastructure.AwsS3.Implementations;

public class AwsS3Manager : IAwsS3FileManager
{
    private const string DefaultRegion = "us-east-1";

    private readonly AmazonS3Client _s3Client;

    public AwsS3Manager(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var regionName = configuration["AWS:Region"] ?? DefaultRegion;
        var region = RegionEndpoint.GetBySystemName(regionName);

        _s3Client = new AmazonS3Client(region);
    }

    public async Task<string> S3UploadFileAsync(string bucketName, string keyName, string filePath)
    {
        try
        {
            var fileTransferUtility = new TransferUtility(_s3Client);

            var uploadRequest = new TransferUtilityUploadRequest
            {
                BucketName = bucketName,
                Key = keyName,
                FilePath = filePath,
                CannedACL = S3CannedACL.PublicRead
            };

            await fileTransferUtility.UploadAsync(uploadRequest);
            return $"https://{bucketName}.s3.{_s3Client.Config.RegionEndpoint.SystemName}.amazonaws.com/{keyName}";
        }
        catch (AmazonS3Exception e)
        {
            Console.WriteLine($"Error encountered on server. Message:'{e.Message}' when writing an object");
            throw;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Unknown encountered on server. Message:'{e.Message}'");
            throw;
        }
    }
}
