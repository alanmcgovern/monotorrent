using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Win32.SafeHandles;

using MonoTorrent.Client;

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

    static class FileReaderWriterHelper
    {
        public static void MaybeTruncate (string fullPath, long length)
        {
            var fi = new FileInfo (fullPath);
            if (fi.Exists && fi.Length > length) {
                using var fileStream = new FileStream (fullPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 1, FileOptions.None);
                fileStream.SetLength (length);
            }
        }
    }

#if NET6_0_OR_GREATER

    class RandomFileReaderWriter : IFileReaderWriter
    {
        SafeFileHandle Handle { get; }

        public bool CanWrite { get; }
        public long Length { get; }

        public RandomFileReaderWriter (SafeFileHandle handle, long length, FileAccess access)
        {
            Handle = handle;
            CanWrite = access.HasFlag (FileAccess.Write);
            Length = length;
        }

        public void Dispose ()
        {
            // Do nothing!
        }

        public ReusableTask FlushAsync ()
            => ReusableTask.CompletedTask;

        public async ReusableTask<int> ReadAsync (Memory<byte> buffer, long offset)
        {
            if (offset + buffer.Length > Length)
                throw new ArgumentOutOfRangeException (nameof (offset));

            await new ThreadSwitcher ();
            return RandomAccess.Read (Handle, buffer.Span, offset);
        }

        public async ReusableTask WriteAsync (ReadOnlyMemory<byte> buffer, long offset)
        {
            if (offset + buffer.Length > Length)
                throw new ArgumentOutOfRangeException (nameof (offset));

            await new ThreadSwitcher ();
            RandomAccess.Write (Handle, buffer.Span, offset);
        }
    }

#else

    class RandomFileReaderWriter : IFileReaderWriter
    {
        FileStream Handle { get; }

        public bool CanWrite { get; }
        public long Length { get; }

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
            await new ThreadSwitcher ();
            Handle.Flush ();
        }

        public async ReusableTask<int> ReadAsync (Memory<byte> buffer, long offset)
        {
            if (offset + buffer.Length > Length)
                throw new ArgumentOutOfRangeException (nameof (offset));

            await new ThreadSwitcher ();
            if (Handle.Position != offset)
                Handle.Seek (offset, SeekOrigin.Begin);
            return Handle.Read (buffer);
        }

        public async ReusableTask WriteAsync (ReadOnlyMemory<byte> buffer, long offset)
        {
            if (offset + buffer.Length > Length)
                throw new ArgumentOutOfRangeException (nameof (offset));

            await new ThreadSwitcher ();
            if (Handle.Position != offset)
                Handle.Seek (offset, SeekOrigin.Begin);
            Handle.Write (buffer);
        }
    }
#endif
}
