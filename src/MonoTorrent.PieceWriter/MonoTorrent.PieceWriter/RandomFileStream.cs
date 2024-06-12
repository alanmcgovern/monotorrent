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
            if (new FileInfo (fullPath).Length > length) {
                using (var fileStream = new FileStream (fullPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 1, FileOptions.None))
                    fileStream.SetLength (length);
            }
        }
    }

    class RandomFileReaderWriter : IFileReaderWriter
    {
#if NET6_0_OR_GREATER
        SafeFileHandle Handle { get; }
#else
        FileStream Handle { get; }
#endif
        public bool CanWrite { get; }
        public long Length { get; }

        public RandomFileReaderWriter (string fullPath, long length, FileMode fileMode, FileAccess access, FileShare share)
        {
            // The "preallocate the file before creating" check is racey as another thread could create this file,
            // so retry without setting the preallocation size if it fails.
            // This should avoid throwing exceptions most of the time.

#if NETSTANDARD2_0 || NETSTANDARD2_1 || NET5_0 || NETCOREAPP3_0
            try {
                if (!File.Exists (fullPath))
                    NtfsSparseFile.CreateSparse (fullPath, length);
            } catch {
                // who cares if we can't pre-allocate a sparse file.
            }
            Handle = new FileStream (fullPath, fileMode, access, share, 1, FileOptions.None);
#else
            try {
                if (!File.Exists (fullPath))
                    File.OpenHandle (fullPath, fileMode, access, share, FileOptions.None, length).Dispose ();
            } catch {
                // who cares if we can't pre-allocate a sparse file.
            }
            Handle = File.OpenHandle (fullPath, fileMode, access, share, FileOptions.None);
#endif
            CanWrite = access.HasFlag (FileAccess.Write);
            Length = length;
        }

        public void Dispose ()
        {
            Handle.Dispose ();
        }

#if NET6_0_OR_GREATER
        public ReusableTask FlushAsync ()
            => ReusableTask.CompletedTask;
#else
        public async ReusableTask FlushAsync ()
        {
            await new EnsureThreadPool ();
            Handle.Flush ();
        }
#endif

        public async ReusableTask<int> ReadAsync (Memory<byte> buffer, long offset)
        {
            if (offset + buffer.Length > Length)
                throw new ArgumentOutOfRangeException (nameof (offset));

            await new EnsureThreadPool ();
#if NET6_0_OR_GREATER
            return RandomAccess.Read (Handle, buffer.Span, offset);
#else
            if (Handle.Position != offset)
                Handle.Seek (offset, SeekOrigin.Begin);
            return Handle.Read (buffer);
#endif
        }

        public async ReusableTask WriteAsync (ReadOnlyMemory<byte> buffer, long offset)
        {
            if (offset + buffer.Length > Length)
                throw new ArgumentOutOfRangeException (nameof (offset));

            await new EnsureThreadPool ();
#if NET6_0_OR_GREATER
            RandomAccess.Write (Handle, buffer.Span, offset);
#else
            if (Handle.Position != offset)
                Handle.Seek (offset, SeekOrigin.Begin);
            Handle.Write (buffer);
#endif
        }
    }
}
