using System.Threading.Tasks;
using System.Text.Json;
using System.Diagnostics;
using Amazon.Lambda.Core;
using Amazon.S3;
using System.Security.Cryptography;
using System;
using Cppl.Utilities.AWS;
using System.IO.Compression;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace DecompressGzAndUpload
{
    public class Functions
    {
        readonly static string BUCKET = System.Environment.GetEnvironmentVariable("BUCKET_NAME");
        const string NAME = "csv_1GB";
        const string GZ_DATA = NAME + ".gz";
        const string OUTPUT_KEY = NAME + ".csv";

        public Functions()
        {
        }

        public async Task<object> Process(JsonDocument request, ILambdaContext context)
        {
            var s3 = new AmazonS3Client();

            // easier than doing math on the timestamps in logs
            var timer = new Stopwatch();
            timer.Start();

            context.Logger.LogLine($"{timer.Elapsed}: Getting started.");
            using var stream = (await s3.GetObjectAsync(BUCKET, GZ_DATA)).ResponseStream;

            using var decompress = new GZipStream(stream, CompressionMode.Decompress);
            using (var output = new S3UploadStream(s3, BUCKET, OUTPUT_KEY)) {
                await decompress.CopyToAsync(output);
            }
            context.Logger.LogLine($"{timer.Elapsed}: Done copying.");
            timer.Stop();

            return new { 
                AesFile = $"s3://{BUCKET}/{GZ_DATA}", 
                CsvFile = $"s3://{BUCKET}/{OUTPUT_KEY}",
                Status = "ok"
            };
        }
    }
}
