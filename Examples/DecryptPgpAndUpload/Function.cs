using System.Threading.Tasks;
using System.Text.Json;
using System.Diagnostics;
using Amazon.Lambda.Core;
using Amazon.S3;
using System.Security.Cryptography;
using System;
using PgpCore;
using Cppl.Utilities.AWS;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace DecryptPgpAndUpload
{
    public class Functions
    {
        readonly static string BUCKET = System.Environment.GetEnvironmentVariable("BUCKET_NAME");
        const string NAME = "csv_1GB";
        const string PGP_DATA = NAME + ".pgp";
        const string PGP_PRIVATE_KEY = "private_key.asc";
        const string PGP_PASSWORD = "this is not a great place for a password";
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
            using var data = new SeekableS3Stream(s3, BUCKET, PGP_DATA, 5 * 1024 * 1024, 10);
            var keys = new EncryptionKeys(new SeekableS3Stream(s3, BUCKET, PGP_PRIVATE_KEY, 32 * 1024), PGP_PASSWORD);

            using var pgp = new PGP(keys);
            using (var output = new S3UploadStream(s3, BUCKET, OUTPUT_KEY)) {
                await pgp.DecryptStreamAsync(data, output);
            }

            context.Logger.LogLine($"{timer.Elapsed}: Done copying.");
            timer.Stop();

            return new { 
                PgpFile = $"s3://{BUCKET}/{PGP_DATA}", 
                CsvFile = $"s3://{BUCKET}/{OUTPUT_KEY}", 
                Status = "ok"
            };
        }
    }
}
