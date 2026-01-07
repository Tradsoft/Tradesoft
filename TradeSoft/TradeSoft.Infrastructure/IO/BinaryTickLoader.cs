using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using TradeSoft.Core.Domain;

namespace TradeSoft.Infrastructure.IO
{
    public unsafe class BinaryTickLoader : IDisposable
    {
        private MemoryMappedFile? _mmf;
        private MemoryMappedViewAccessor? _accessor;
        private byte* _pointer;
        private bool _disposed;
        private long _count;

        public long Count => _count;

        public BinaryTickLoader(string filePath)
        {
            var fileInfo = new FileInfo(filePath);

            if (fileInfo.Length == 0)
            {
                _count = 0;
                return;
            }

            _count = fileInfo.Length / sizeof(TickData);

            _mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            byte* ptr = null;
            _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            _pointer = ptr;
        }

        public ReadOnlySpan<TickData> GetTicksAsSpan(long count)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BinaryTickLoader));

            if (count > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(count), "Span ne peut pas contenir plus de 2 milliards d'éléments.");

            return new ReadOnlySpan<TickData>(_pointer, (int)count);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_accessor != null)
                {
                    _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                    _accessor.Dispose();
                }

                _mmf?.Dispose();
                _disposed = true;
            }
        }
    }
}