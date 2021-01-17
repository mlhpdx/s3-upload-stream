using System.Threading.Tasks;
using System.Text.Json;
using System.Diagnostics;
using Amazon.Lambda.Core;
using Amazon.S3;
using System.Security.Cryptography;
using System;
using Cppl.Utilities.AWS;
using System.IO.Compression;
using Amazon.S3.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ZipS3ListToS3
{
    public class Functions
    {
        readonly static string BUCKET = System.Environment.GetEnvironmentVariable("BUCKET_NAME");
        const string NAME = "zip_me";
        const string PREFIX = NAME + "/";
        const string OUTPUT_KEY = NAME + ".zip";

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
            using (var output = new S3UploadStream(s3, BUCKET, OUTPUT_KEY)) {
                using var zip = new ZipArchive(output, ZipArchiveMode.Create);
                await foreach (var o in s3.Paginators.ListObjects(new ListObjectsRequest() { BucketName = BUCKET, Prefix = PREFIX }).S3Objects) {
                    context.Logger.LogLine($"{timer.Elapsed}: Starting on {o.Key}.");
                    using var stream = (await s3.GetObjectAsync(BUCKET, o.Key)).ResponseStream;
                    using var entry = zip.CreateEntry(o.Key.Substring(PREFIX.Length)).Open();
                    await stream.CopyToAsync(entry);
                    context.Logger.LogLine($"{timer.Elapsed}: Done with {o.Key}.");
                }
            }
            context.Logger.LogLine($"{timer.Elapsed}: Done.");
            timer.Stop();

            return new { 
                Prefix = PREFIX, 
                ZipFile = $"s3://{BUCKET}/{OUTPUT_KEY}",
                Status = "ok"
            };
        }
    }
}
