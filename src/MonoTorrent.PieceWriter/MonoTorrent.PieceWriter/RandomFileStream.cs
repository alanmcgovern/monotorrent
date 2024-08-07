﻿using System;
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
#if NETSTANDARD2_0 || NETSTANDARD2_1 || NET5_0 || NETCOREAPP3_0 || NET472
        FileStream Handle { get; }
#else
        SafeFileHandle Handle { get; }
#endif
        public bool CanWrite { get; }
        public long Length { get; }

        public RandomFileReaderWriter (string fullPath, long length, FileMode fileMode, FileAccess access, FileShare share)
        {
            // The "preallocate the file before creating" check is racey as another thread could create this file,
            // so retry without setting the preallocation size if it fails.
            // This should avoid throwing exceptions most of the time.

#if NETSTANDARD2_0 || NETSTANDARD2_1 || NET5_0 || NETCOREAPP3_0 || NET472
            try {
                if (!File.Exists (fullPath))
                    NtfsSparseFile.CreateSparse (fullPath, length);
            } catch {
                // who cares if we can't pre-allocate a sparse file.
            }
            Handle = new FileStream (fullPath, fileMode, access, share, 1, FileOptions.None);
#else
            try {
                if (!File.Exists (fullPath) && (fileMode == FileMode.Create || fileMode == FileMode.CreateNew || fileMode == FileMode.OpenOrCreate))
                    File.OpenHandle (fullPath, FileMode.CreateNew, access, share, FileOptions.None, length).Dispose ();
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

#if NETSTANDARD2_0 || NETSTANDARD2_1 || NET5_0 || NETCOREAPP3_0 || NET472
        public async ReusableTask FlushAsync ()
        {
            await new EnsureThreadPool ();
            Handle.Flush ();
        }
#else
        public ReusableTask FlushAsync ()
            => ReusableTask.CompletedTask;
#endif

        public async ReusableTask<int> ReadAsync (Memory<byte> buffer, long offset)
        {
            if (offset + buffer.Length > Length)
                throw new ArgumentOutOfRangeException (nameof (offset));

            await new EnsureThreadPool ();
#if NETSTANDARD2_0 || NETSTANDARD2_1 || NET5_0 || NETCOREAPP3_0 || NET472
            if (Handle.Position != offset)
                Handle.Seek (offset, SeekOrigin.Begin);
            return Handle.Read (buffer);
#else
            return RandomAccess.Read (Handle, buffer.Span, offset);
#endif
        }

        public async ReusableTask WriteAsync (ReadOnlyMemory<byte> buffer, long offset)
        {
            if (offset + buffer.Length > Length)
                throw new ArgumentOutOfRangeException (nameof (offset));

            await new EnsureThreadPool ();
#if NETSTANDARD2_0 || NETSTANDARD2_1 || NET5_0 || NETCOREAPP3_0 || NET472
            if (Handle.Position != offset)
                Handle.Seek (offset, SeekOrigin.Begin);
            Handle.Write (buffer);
#else
            RandomAccess.Write (Handle, buffer.Span, offset);
#endif
        }
    }
}
