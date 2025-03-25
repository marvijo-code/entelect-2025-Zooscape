using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;

namespace S3Logger.Utilities;

public static class S3
{
    public static async Task UploadLogs()
    {
        Console.WriteLine("Game Complete. Saving logs...");

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PUSH_LOGS_TO_S3")))
        {
            return;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("S3_BUCKET_NAME")))
                throw new Exception("No S3_BUCKET_NAME environment variable defined.");

            string[] parts = Environment.GetEnvironmentVariable("S3_BUCKET_NAME").Split('/');
            string bucketName = parts[0];
            string prefix = "/" + string.Join("/", parts.Skip(1));
            RegionEndpoint? bucketRegion = RegionEndpoint.GetBySystemName(
                Environment.GetEnvironmentVariable("AWS_REGION")
            );

            Console.WriteLine("Beginning S3 Upload");
            AmazonS3Client s3Client = new(bucketRegion);
            TransferUtility transferUtility = new(s3Client);
            TransferUtilityUploadDirectoryRequest uploadRequest = new()
            {
                BucketName = bucketName,
                Directory = Environment.GetEnvironmentVariable("LOG_DIR"),
                KeyPrefix = prefix,
            };
            await transferUtility.UploadDirectoryAsync(uploadRequest);
            Console.WriteLine("Completed S3 Upload");
        }
        catch (Exception exp)
        {
            Console.WriteLine($"Failed to upload to S3 - {exp.Message}");
            throw;
        }
    }
}
