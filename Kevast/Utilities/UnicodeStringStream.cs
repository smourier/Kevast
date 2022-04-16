using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Kevast.Utilities
{
    // from https://stackoverflow.com/a/67424222/403671
    public sealed class UnicodeStringStream : Stream
    {
        private const int _bytesPerChar = 2;

        private ReadOnlyMemory<char> _memory;
        private long _position;

        public UnicodeStringStream(string @string)
            : this((@string ?? throw new ArgumentNullException(nameof(@string))).AsMemory())
        {
        }

        public UnicodeStringStream(ReadOnlyMemory<char> memory) => _memory = memory;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override int Read(Span<byte> buffer)
        {
            EnsureOpen();
            var charPosition = _position / _bytesPerChar;

            // MemoryMarshal.AsBytes will throw on strings longer than int.MaxValue / 2, so only slice what we need. 
            var slice = MemoryMarshal.AsBytes(_memory.Slice((int)charPosition, (int)Math.Min(_memory.Length - charPosition, 1 + buffer.Length / _bytesPerChar)).Span);
            var slicePosition = _position % _bytesPerChar;
            var read = (int)Math.Min(buffer.Length, slice.Length - slicePosition);
            slice.Slice((int)slicePosition, read).CopyTo(buffer);
            _position += read;
            return read;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArgs(buffer, offset, count);
            return Read(buffer.AsSpan(offset, count));
        }

        public override int ReadByte()
        {
            // could be optimized
            Span<byte> span = stackalloc byte[1];
            return Read(span) == 0 ? -1 : span[0];
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            EnsureOpen();
            if (cancellationToken.IsCancellationRequested)
                return ValueTask.FromCanceled<int>(cancellationToken);

            try
            {
                return new ValueTask<int>(Read(buffer.Span));
            }
            catch (Exception exception)
            {
                return ValueTask.FromException<int>(exception);
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArgs(buffer, offset, count);
            return await ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _memory = default;
                _position = -1;
            }
        }

        private void EnsureOpen()
        {
            if (_position < 0)
                throw new ObjectDisposedException(GetType().Name);
        }

        private static void ValidateBufferArgs(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (count < 0 || count > buffer.Length - offset)
                throw new ArgumentOutOfRangeException(nameof(count));
        }

        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => cancellationToken.IsCancellationRequested ? Task.FromCanceled(cancellationToken) : Task.CompletedTask;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
