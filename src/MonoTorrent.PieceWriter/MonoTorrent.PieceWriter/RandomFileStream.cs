using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Win32.SafeHandles;

using ReusableTasks;

namespace MonoTorrent.PieceWriter
{
    interface IFileReaderWriter : IDisposable
    {
        bool CanWrite { get; }

        ReusableTask FlushAsync ();
        ReusableTask<int> ReadAsync (Memory<byte> buffer, long offset);
        ReusableTask WriteAsync (ReadOnlyMemory<byte> buffer, long offset);
    }

#if NETSTANDARD2_0 || NETSTANDARD2_1 || NET5_0 || NETCOREAPP3_0 || NET472
    class RandomFileReaderWriter : IFileReaderWriter
    {
        public bool CanWrite { get; }
        public long Length { get; }
        FileStream Handle { get; }

        public RandomFileReaderWriter (string fullPath, long length, FileMode fileMode, FileAccess access, FileShare share)
        {
            Handle = new FileStream (fullPath, fileMode, access, share, 1, FileOptions.None);
            CanWrite = access.HasFlag (FileAccess.Write);
            Length = length;
        }

        public void Dispose ()
        {
            Handle.Dispose ();
        }

        public async ReusableTask FlushAsync ()
        {
            await new EnsureThreadPool ();
            Handle.Flush ();
        }

        public async ReusableTask<int> ReadAsync (Memory<byte> buffer, long offset)
        {
            if (offset + buffer.Length > Length)
                throw new ArgumentOutOfRangeException (nameof (offset));

            await new EnsureThreadPool ();
            if (Handle.Position != offset)
                Handle.Seek (offset, SeekOrigin.Begin);
            return Handle.Read (buffer);
        }

        public async ReusableTask WriteAsync (ReadOnlyMemory<byte> buffer, long offset)
        {
            if (offset + buffer.Length > Length)
                throw new ArgumentOutOfRangeException (nameof (offset));

            await new EnsureThreadPool ();
            if (Handle.Position != offset)
                Handle.Seek (offset, SeekOrigin.Begin);
            Handle.Write (buffer);
        }
    }
#else

    class RandomFileReaderWriter : IFileReaderWriter
    {
        public bool CanWrite { get; }
        public long Length { get; }
        SafeFileHandle Handle { get; }

        public RandomFileReaderWriter (string fullPath, long length, FileMode fileMode, FileAccess access, FileShare share)
        {
            Handle = File.OpenHandle (fullPath, fileMode, access, share, FileOptions.None);
            CanWrite = access.HasFlag (FileAccess.Write);
            Length = length;
        }

        public void Dispose ()
        {
            Handle.Dispose ();
        }

        public ReusableTask FlushAsync ()
            => ReusableTask.CompletedTask;

        public async ReusableTask<int> ReadAsync (Memory<byte> buffer, long offset)
        {
            if (offset + buffer.Length > Length)
                throw new ArgumentOutOfRangeException (nameof (offset));

            await new EnsureThreadPool ();
            return RandomAccess.Read (Handle, buffer.Span, offset);
        }

        public async ReusableTask WriteAsync (ReadOnlyMemory<byte> buffer, long offset)
        {
            if (offset + buffer.Length > Length)
                throw new ArgumentOutOfRangeException (nameof (offset));

            await new EnsureThreadPool ();
            RandomAccess.Write (Handle, buffer.Span, offset);
        }
    }
#endif
}
