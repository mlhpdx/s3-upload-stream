# S3 Upload Stream

This library performs uploads to S3 without holding the entire object content in memory (or storage) beforehand.  This approach is more efficient than using `MemoryStream` and offers compatibility with libraries and packages that work with a `Stream` interface.  Examples for extracting GZIP, decrypting AES and PGP, and uploading dynamically-generated CSV are provided.

This project is a follow-on to `SeekableS3Stream`, also on [Github](https://github.com/mlhpdx/seekable-s3-stream). Using both libraries allows for very efficient, and simple, large-file processing to and from S3 via AWS Lambda, as demonstrated in one of the [Examples](https://github.com/mlhpdx/s3-upload-stream/tree/main/Examples).

For a more detailed explaination, check out the article on [Medium](https://medium.com/circuitpeople/stream-to-stream-s3-uploads-with-aws-lambda-578fe710ac1e).
