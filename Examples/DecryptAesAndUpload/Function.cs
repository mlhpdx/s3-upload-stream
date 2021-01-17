using System.Threading.Tasks;
using System.Text.Json;
using System.Diagnostics;
using Amazon.Lambda.Core;
using Amazon.S3;
using System.Security.Cryptography;
using System;
using Cppl.Utilities.AWS;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace DecryptAesAndUpload
{
    public class Functions
    {
        readonly static string BUCKET = System.Environment.GetEnvironmentVariable("BUCKET_NAME");
        const string NAME = "csv_1GB";
        const string AES_DATA = NAME + ".aes";
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
            using var stream = (await s3.GetObjectAsync(BUCKET, AES_DATA)).ResponseStream;

            // setup a decryptor
            using var aes = AesManaged.Create();
            aes.IV = Convert.FromBase64String("EqYoED0ag4vlPnFkWZMCog==");
            aes.Key = Convert.FromBase64String("Sgf9NocncDHSBqMXrMthXbToAQmthMpC6eJ6Hw51Ghg=");
            
            using var idecrypt = aes.CreateDecryptor();
            using var cstream = new CryptoStream(stream, idecrypt, CryptoStreamMode.Read);
            using (var output = new S3UploadStream(s3, BUCKET, OUTPUT_KEY)) {
                await cstream.CopyToAsync(output);
            }
            context.Logger.LogLine($"{timer.Elapsed}: Done copying.");
            timer.Stop();

            return new { 
                AesFile = $"s3://{BUCKET}/{AES_DATA}", 
                CsvFile = $"s3://{BUCKET}/{OUTPUT_KEY}",
                Status = "ok"
            };
        }
    }
}
