using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;

namespace Cppl.Utilities.AWS
{
    public class S3UploadStream : Stream
    {
        /* Note the that maximum size (as of now) of a file in S3 is 5TB so it isn't
         * safe to assume all uploads will work here.  MAX_PART_SIZE times MAX_PART_COUNT
         * is ~50TB, which is too big for S3. */
        const long MIN_PART_LENGTH = 5L * 1024 * 1024; // all parts but the last this size or greater
        const long MAX_PART_LENGTH = 5L * 1024 * 1024 * 1024; // 5GB max per PUT
        const long MAX_PART_COUNT = 10000; // no more than 10,000 parts total
        const long DEFAULT_PART_LENGTH = MIN_PART_LENGTH;

        internal class Metadata
        {
            public long PartLength = DEFAULT_PART_LENGTH;

            public int PartCount = 0;
            public string UploadId;
            public MemoryStream CurrentStream;
            public CancellationToken CancellationToken;

            public long Position = 0; // based on bytes written
            public long Length = 0; // based on bytes written or SetLength, whichever is larger (no truncation)

            public List<Task> Tasks = new List<Task>();
            public ConcurrentDictionary<int, PartETag> PartETags = new ConcurrentDictionary<int, PartETag>();

            public InitiateMultipartUploadRequest InitiateMultipartUploadRequest;
        }

        private readonly IAmazonS3 _s3 = null;
        private Metadata _metadata = new Metadata();

        public event Action<InitiateMultipartUploadResponse> Initiated;
        public event Action<UploadPartResponse> UploadedPart;
        public event Action<StreamTransferProgressArgs> StreamTransfer;
        public event Action<CompleteMultipartUploadResponse> Completed;


        public S3UploadStream(
            IAmazonS3 s3,
            string s3uri,
            long partLength = DEFAULT_PART_LENGTH,
            CancellationToken token = default)
            : this(s3, new Uri(s3uri), partLength, token)
        {
        }

        public S3UploadStream(
            IAmazonS3 s3,
            Uri s3uri,
            long partLength = DEFAULT_PART_LENGTH,
            CancellationToken token = default)
            : this(s3, s3uri.Host, s3uri.LocalPath.Substring(1), partLength, token)
        {
        }

        public S3UploadStream(
            IAmazonS3 s3,
            string bucket,
            string key,
            long partLength = DEFAULT_PART_LENGTH,
            CancellationToken token = default)
            : this(
                s3,
                new InitiateMultipartUploadRequest
                {
                    BucketName = bucket,
                    Key = key
                },
                partLength,
                token)
        {
        }

        public S3UploadStream(
            IAmazonS3 s3,
            InitiateMultipartUploadRequest initiateMultipartUploadRequest,
            long partLength = DEFAULT_PART_LENGTH,
            CancellationToken token = default)
        {
            _s3 = s3;
            _metadata.PartLength = partLength;
            _metadata.InitiateMultipartUploadRequest = initiateMultipartUploadRequest;
            _metadata.CancellationToken = token;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_metadata != null)
                {
                    Flush(true);
                    CompleteUpload();
                }
            }

            _metadata = null;
            base.Dispose(disposing);
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _metadata.Length = Math.Max(_metadata.Length, _metadata.Position);

        public override long Position
        {
            get => _metadata.Position;
            set => throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();

        public override void SetLength(long value)
        {
            _metadata.Length = Math.Max(_metadata.Length, value);
            _metadata.PartLength =
                Math.Max(MIN_PART_LENGTH, Math.Min(MAX_PART_LENGTH, _metadata.Length / MAX_PART_COUNT));
        }

        private void StartNewPart()
        {
            if (_metadata.CurrentStream != null)
            {
                Flush(false);
            }

            _metadata.CurrentStream = new MemoryStream();
            _metadata.PartLength = Math.Min(MAX_PART_LENGTH,
                Math.Max(_metadata.PartLength, (_metadata.PartCount / 2 + 1) * MIN_PART_LENGTH));
        }

        public override void Flush()
        {
            Flush(false);
        }

        private void Flush(bool disposing)
        {
            if ((_metadata.CurrentStream == null || _metadata.CurrentStream.Length < MIN_PART_LENGTH) &&
                !disposing)
            {
                return;
            }

            if (_metadata.UploadId == null)
            {
                var response = _s3
                    .InitiateMultipartUploadAsync(_metadata.InitiateMultipartUploadRequest, _metadata.CancellationToken)
                    .GetAwaiter().GetResult();
                _metadata.CancellationToken.ThrowIfCancellationRequested();
                Initiated?.Invoke(response);
                _metadata.UploadId = response.UploadId;
            }

            if (_metadata.CurrentStream != null)
            {
                var i = ++_metadata.PartCount;

                _metadata.CurrentStream.Seek(0, SeekOrigin.Begin);
                var request = new UploadPartRequest()
                {
                    BucketName = _metadata.InitiateMultipartUploadRequest.BucketName,
                    Key = _metadata.InitiateMultipartUploadRequest.Key,
                    UploadId = _metadata.UploadId,
                    PartNumber = i,
                    IsLastPart = disposing,
                    InputStream = _metadata.CurrentStream,
                    ChecksumAlgorithm = _metadata.InitiateMultipartUploadRequest.ChecksumAlgorithm,
                };
                _metadata.CurrentStream = null;
                request.StreamTransferProgress += (_, progressArgs) => { StreamTransfer?.Invoke(progressArgs); };

                var upload = Task.Run(
                    async () =>
                    {
                        var response = await _s3.UploadPartAsync(request, _metadata.CancellationToken);

                        _metadata.CancellationToken.ThrowIfCancellationRequested();

                        UploadedPart?.Invoke(response);

                        var partETag = new PartETag
                        {
                            PartNumber = response.PartNumber,
                            ETag = response.ETag,
                            ChecksumSHA1 = response.ChecksumSHA1,
                            ChecksumSHA256 = response.ChecksumSHA256,
                            ChecksumCRC32 = response.ChecksumCRC32,
                            ChecksumCRC32C = response.ChecksumCRC32C,
                        };

                        _metadata.PartETags.AddOrUpdate(i, partETag, (n, s) => partETag);
                        request.InputStream.Dispose();
                    });
                _metadata.Tasks.Add(upload);
            }
        }

        private void CompleteUpload()
        {
            Task.WaitAll(_metadata.Tasks.ToArray());

            if (Length > 0)
            {
                _metadata.CancellationToken.ThrowIfCancellationRequested();

                var response = _s3.CompleteMultipartUploadAsync(
                    new CompleteMultipartUploadRequest()
                    {
                        BucketName = _metadata.InitiateMultipartUploadRequest.BucketName,
                        Key = _metadata.InitiateMultipartUploadRequest.Key,
                        PartETags = _metadata.PartETags.Values.ToList(),
                        UploadId = _metadata.UploadId,
                    }, _metadata.CancellationToken).GetAwaiter().GetResult();
                Completed?.Invoke(response);
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count == 0) return;

            // write as much of the buffer as will fit to the current part, and if needed
            // allocate a new part and continue writing to it (and so on).
            var o = offset;
            var c = Math.Min(count, buffer.Length - offset); // don't over-read the buffer, even if asked to
            do
            {
                if (_metadata.CancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (_metadata.CurrentStream == null || _metadata.CurrentStream.Length >= _metadata.PartLength)
                {
                    StartNewPart();
                }

                if (_metadata.CurrentStream == null)
                {
                    throw new ArgumentNullException(nameof(Metadata.CurrentStream));
                }

                var remaining = _metadata.PartLength - _metadata.CurrentStream.Length;
                var w = Math.Min(c, (int)remaining);
                _metadata.CurrentStream.Write(buffer, o, w);

                _metadata.Position += w;
                c -= w;
                o += w;
            } while (c > 0);
        }
    }
}