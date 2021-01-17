# Streaming Zip Compression from and to S3

This example demonstrates a Lambda function that compresses objects in S3 that are in combination much larger than the available RAM, and uploads the compression archive back to S3 using a `S3UploadStream`.  In this case the Lambda is intentionally under-provisioned with ony 1024 MB of RAM while the files to be processed total around 2GB.  Producing such a Zip with a naiive solution where each included file is read into RAM and the Zip produced in RAM (likely using `MemoryStream`s) would obviously not work here. Likewise, saving each file to an attached drive (EFS) and producing the zip there would be slow at best.  This examples Zip's all the object found at a given prefix (`zip_me/`).

**NOTE**: Running this Lambda processes a very large amount of data, and will cost you money.  Keep that in mind, please.  Also note that this example reads and WRITES to the bucket you specify, hense following the instructions to create a new bucket for these examples is probably a good idea -- see the parent folder's [Readme.md](../Readme.md) for more information.

## Deploying and Running from the Command Line

Deploy application (run from the project directory, see NOTE about about the bucket)
```
   export STACK=s3-upload-stream-example-zip
   dotnet lambda deploy-serverless --stack-name $STACK --s3-bucket $BUCKET --template-parameters Bucket=$BUCKET
```

Invoking the example function
```
   export LAMBDA=$(aws cloudformation describe-stack-resources --stack-name $STACK --query "StackResources[? LogicalResourceId == 'Process'].PhysicalResourceId" --output text)
   aws lambda invoke --function-name $LAMBDA --log-type Tail --cli-read-timeout 900 --query LogResult --output text payload.json > log.txt
```

Viewing the resulting payload, and logs
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
START RequestId: 7839b691-2c46-43a0-b38f-f5aaedf05b66 Version: $LATEST
00:00:00.0000016: Getting started.
00:00:00.0688976: Starting on zip_me/a.bin.
00:01:52.5103068: Done with zip_me/a.bin.
00:01:52.5105763: Starting on zip_me/b.csv.
00:03:55.3356461: Done with zip_me/b.csv.
00:03:56.9912055: Done.
END RequestId: 7839b691-2c46-43a0-b38f-f5aaedf05b66
REPORT RequestId: 7839b691-2c46-43a0-b38f-f5aaedf05b66  Duration: 236993.76 ms  Billed Duration: 236994 ms      Memory Size: 1024 MB    Max Memory Used: 858 MB
```

For completeness, here is the function output payload:

```json
{
  "Prefix": "zip_me/",
  "ZipFile": "s3://s3-upload-examples-5i2iwewz/zip_me.zip",
  "Status": "ok"
}
```
