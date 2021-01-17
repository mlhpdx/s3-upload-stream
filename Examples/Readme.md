# S3 Upload Stream Examples

The following example projects implement Lambda functions that use `S3UploadStream` to perform worloads that stream content from S3 and back to S3 without holding objects entirely in memory.

- [Object Decompression](./DecompressGzAndUpload)
- [AES Decryption](./DecryptAesAndUpload)
- [PGP Decryption](./DecryptPgpAndUpload)
- [Zip S3 Objects into a Zip Archive in S3](./ZipS3ListToS3)

## Deploying and Preparing to Run these Examples from the Command Line

This application may be deployed using the [Amazon.Lambda.Tools Global Tool](https://github.com/aws/aws-extensions-for-dotnet-cli#aws-lambda-amazonlambdatools) from the command line.  The command examples below should work on any Linux, but were composed on WSL 2 so YMMV.

Install Amazon.Lambda.Tools Global Tools if not already installed:
```
   dotnet tool install -g Amazon.Lambda.Tools
```

If already installed check if new version is available:
```
   dotnet tool update -g Amazon.Lambda.Tools
```

Create a bucket, and copy the files needed for the examples into it (a few GB of objects):
```
   export BUCKET=s3-upload-examples-$(cat /dev/urandom | tr -dc 'a-z0-9' | fold -w 8 | head -n 1)
   aws s3 mb s3://$BUCKET
   aws s3 cp s3://us-east-1.whitepuffies.com s3://$BUCKET --recursive
```

Once the bucket is created (and the BUCKET environment variable set) you can proceed to the examples above. Each example includes instructions for creating a stack, using it, and removing it (each stack only uses resources in the bucket you create here, both at deployment time and while running the examples). Once you're done with the example, the bucket can be removed.

To cleanup the bucket:

**NOTE:** Only run the following commands if you created a *dedicated bucket for these samples*. Running them will result in ALL OBJECTS in the bucket being removed, and the bucket deleted.

```
   aws s3 rb s3://$BUCKET --force
```