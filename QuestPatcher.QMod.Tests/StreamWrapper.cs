using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace QuestPatcher.QMod.Tests
{
    /// <summary>
    /// Used to wrap streams for testing without writable/readable/seekable streams
    /// </summary>
    public class StreamWrapper : Stream
    {
        private readonly Stream _underlying;
        private bool _disposed;

        public StreamWrapper(Stream? underlying = null)
        {
            _underlying = underlying ?? new MemoryStream();
        }

        public override void Flush() => _underlying.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _underlying.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _underlying.Seek(offset, origin);
        public override void SetLength(long value) => _underlying.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _underlying.Write(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _underlying.ReadAsync(buffer, cancellationToken);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => _underlying.WriteAsync(buffer, cancellationToken);
        
        public override bool CanRead => OverrideCanRead ?? _underlying.CanRead;
        public bool? OverrideCanRead { get; set; }
        
        public override bool CanSeek => OverrideCanSeek ?? _underlying.CanSeek;
        public bool? OverrideCanSeek { get; set; }
        
        public override bool CanWrite => OverrideCanWrite ?? _underlying.CanWrite;
        public bool? OverrideCanWrite { get; set; }

        public override long Length => _underlying.Length;
        public override long Position
        {
            get => _underlying.Position;
            set => _underlying.Position = value;
        }
        
        ~StreamWrapper()
        {
            Dispose(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed) { return; }

            _underlying.Dispose();
            _disposed = true;
        }
        
        public override async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();
            
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            await _underlying.DisposeAsync();
        }
    }
}
