# Streaming AES 256 Decryption from and to S3

This example demonstrates a Lambda function that  decrypt a file in S3 that is much larger than the available RAM, and uploads the content back to S3 using a `S3UploadStream`.  In this case the Lambda is intentionally under-provisioned with ony 1024 MB of RAM while the file to be processed is around 1GB.  Processing such a file with a naiive solution where it is read and decompressed into RAM (likely using a `MemoryStream`) would obviously not work here, and saving to an attached drive (EFS) would be slow at best.  The example file is randomly-generated CSV content encrypted with AES 256.

**NOTE**: Running this Lambda processes a very large amount of data, and will cost you money.  Keep that in mind, please.  Also note that this example reads and WRITES to the bucket you specify, hense following the instructions to create a new bucket for these examples is probably a good idea -- see the parent folder's [Readme.md](../Readme.md) for more information.

## Deploying and Running from the Command Line

Deploy application (run from the project directory, see NOTE about about the bucket)
```
   export STACK=s3-upload-stream-example-eas
   dotnet lambda deploy-serverless --stack-name $STACK --s3-bucket $BUCKET --template-parameters Bucket=$BUCKET
```

Invoking the example function
```
   export LAMBDA=$(aws cloudformation describe-stack-resources --stack-name $STACK --query "StackResources[? LogicalResourceId == 'Process'].PhysicalResourceId" --output text)
   aws lambda invoke --function-name $LAMBDA --log-type Tail --cli-read-timeout 900 --query LogResult --output text payload.json > log.txt
```

View the resulting payload, and logs
```
   more payload.json
   cat log.txt | base64 -d
```

Cleanup the stack
```
   aws cloudformation delete-stack --stack-name $STACK
```

## Results

Here are the log messages and output of a (second, not cold-start) run of the function: 

```
START RequestId: a60cf25a-f575-4d33-a573-ab7716befcaa Version: $LATEST
00:00:00.0000047: Getting started.
00:01:18.2201559: Done copying.
END RequestId: a60cf25a-f575-4d33-a573-ab7716befcaa
REPORT RequestId: a60cf25a-f575-4d33-a573-ab7716befcaa  Duration: 78222.65 ms   Billed Duration: 78223 ms       Memory Size: 1024 MB    Max Memory Used: 788 MB
```

For completeness, here is the function output payload:

```json
{
  "AesFile": "s3://s3-upload-examples-5i2iwewz/csv_1GB.aes",
  "CsvFile": "s3://s3-upload-examples-5i2iwewz/csv_1GB.csv",
  "Status": "ok"
}
```
