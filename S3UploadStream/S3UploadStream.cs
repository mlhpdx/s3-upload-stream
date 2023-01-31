using Amazon.S3;
using Amazon.S3.Model;
using System.Collections.Concurrent;
using Microsoft.IO;

namespace Infrastructure.Common.IO;

public class S3UploadStream : Stream
{
    /* Note the that maximum size (as of now) of a file in S3 is 5TB so it isn't
     * safe to assume all uploads will work here.  MAX_PART_SIZE times MAX_PART_COUNT
     * is ~50TB, which is too big for S3. */
    private const long MinPartLength = 5 * 1024 * 1024; // 5MBs
    private const long MaxPartLength = 10 * 1024 * 1024; // 10MB max per PUT
    private const long MaxPartCount = 10000; // no more than 10,000 parts total
    private const long DefaultPartLength = MinPartLength;
    private const int MaxTaskRunningInParallel = 6;

    private class S3UploadContext
    {
        public string BucketName { get; init; } = string.Empty;
        public string Key { get; init; } = string.Empty;
        public long PartLength { get; set; } = DefaultPartLength;
        public int PartCount { get; set; }
        public string UploadId { get; set; } = string.Empty;
        public MemoryStream? CurrentStream { get; set; }
        public long Position { get; set; } // based on bytes written
        public long Length { get; set; } // based on bytes written or SetLength, whichever is larger (no truncation)
        public readonly List<Task> Tasks = new();
        public readonly ConcurrentDictionary<int, string> PartETags = new();
    }

    private S3UploadContext _context;
    private readonly IAmazonS3 _s3;
    private readonly RecyclableMemoryStreamManager _memoryStreamManager = new();

    public S3UploadStream(IAmazonS3 s3, string s3Uri, long partLength = DefaultPartLength)
        : this(s3, new Uri(s3Uri), partLength)
    {
    }

    private S3UploadStream(IAmazonS3 s3, Uri s3Uri, long partLength = DefaultPartLength)
        : this(s3, s3Uri.Host, s3Uri.LocalPath[1..], partLength)
    {
    }

    public S3UploadStream(IAmazonS3 s3, string bucket, string key, long partLength = DefaultPartLength)
    {
        _s3 = s3;
        _context = new S3UploadContext
        {
            BucketName = bucket,
            Key = key,
            PartLength = partLength
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Flush(true);
            CompleteUpload();
        }
        _context = new S3UploadContext();
        base.Dispose(disposing);
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;

    public override long Length => _context.Length = Math.Max(_context.Length, _context.Position);

    public override long Position
    {
        get => _context.Position;
        set => throw new NotImplementedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();

    public override void SetLength(long value)
    {
        _context.Length = Math.Max(_context.Length, value);
        _context.PartLength = Math.Max(MinPartLength, Math.Min(MaxPartLength, _context.Length / MaxPartCount));
    }

    private void StartNewPart()
    {
        if (_context.CurrentStream != null)
        {
            Flush(false);
        }

        _context.CurrentStream = _memoryStreamManager.GetStream("S3_UPLOAD_STREAM");
        _context.PartLength = Math.Min(MaxPartLength,
            Math.Max(_context.PartLength, (_context.PartCount / 2 + 1) * MinPartLength));
    }

    public override void Flush()
    {
        Flush(false);
    }

    private void Flush(bool disposing)
    {
        if ((_context.CurrentStream == null || _context.CurrentStream.Length < MinPartLength) &&
            !disposing)
            return;

        if (string.IsNullOrEmpty(_context.UploadId))
        {
            _context.UploadId = _s3.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
            {
                BucketName = _context.BucketName,
                Key = _context.Key
            }).GetAwaiter().GetResult().UploadId;
        }

        if (_context.CurrentStream == null)
            return;

        var i = ++_context.PartCount;

        _context.CurrentStream.Seek(0, SeekOrigin.Begin);
        var request = new UploadPartRequest
        {
            BucketName = _context.BucketName,
            Key = _context.Key,
            UploadId = _context.UploadId,
            PartNumber = i,
            IsLastPart = disposing,
            InputStream = _context.CurrentStream
        };

        _context.CurrentStream = null;

        if (_context.Tasks.Count > MaxTaskRunningInParallel)
        {
            Task.WaitAll(_context.Tasks.ToArray());
            _context.Tasks.Clear();
        }

        var upload = Task.Run(async () =>
        {
            var response = await _s3.UploadPartAsync(request);
            _context.PartETags.AddOrUpdate(i, response.ETag,
                (_, _) => response.ETag);
            await request.InputStream.DisposeAsync();
        });
        _context.Tasks.Add(upload);
    }

    private void CompleteUpload()
    {
        Task.WaitAll(_context.Tasks.ToArray());

        if (Length > 0)
        {
            _s3.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
            {
                BucketName = _context.BucketName,
                Key = _context.Key,
                PartETags = _context.PartETags.Select(e => new PartETag(e.Key, e.Value)).ToList(),
                UploadId = _context.UploadId
            }).GetAwaiter().GetResult();
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
            if (_context.CurrentStream == null || _context.CurrentStream.Length >= _context.PartLength)
                StartNewPart();

            var remaining = _context.PartLength - _context.CurrentStream!.Length;
            var w = Math.Min(c, (int)remaining);

            _context.CurrentStream.Write(buffer, o, w);
            _context.Position += w;
            c -= w;
            o += w;
        } while (c > 0);
    }
}