# S3 Upload Stream

This code demonstrates how to perform uploads to S3 without holding the entire content in memory or storage beforehand.  This approach is more memory efficient than using `MemoryStream` and offers compatibility with libraries and packages that work with a `Stream` interface.  Examples for extracting GZIP, decrypting AES and PGP, and uploading dynamically-generated CSV are provided.  A [NuGet package](https://github.com/mlhpdx/s3-upload-stream/packages) is also available for the library, hosted here on GitHub.

This project is a follow-on to `SeekableS3Stream`, also on [Github](https://github.com/mlhpdx/seekable-s3-stream). Using both allows for very efficient and simple large file processing to and from S3 via AWS Lambda, as demonstrated in one of the [Examples](./Examples/Readme.md).

For a more detailed explaination, check out the article on [Medium](https://medium.com/circuitpeople/streaming-uploads-for-amazon-s3-in-c-...).
