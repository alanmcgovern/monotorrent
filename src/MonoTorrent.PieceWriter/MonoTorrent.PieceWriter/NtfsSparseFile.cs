//
// NtfsSpareFile.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Win32.SafeHandles;

namespace MonoTorrent.PieceWriter
{
    static class NtfsSparseFile
    {
        [StructLayout (LayoutKind.Sequential)]
        struct FILE_ZERO_DATA_INFORMATION
        {
            public FILE_ZERO_DATA_INFORMATION (long offset, long count)
            {
                FileOffset = offset;
                BeyondFinalZero = offset + count;
            }

            public readonly long FileOffset;
            public readonly long BeyondFinalZero;
        }

        const int MAX_PATH = 260;
        const uint FILE_SUPPORTS_SPARSE_FILES = 64;
        const uint FSCTL_SET_SPARSE = ((uint) 0x00000009 << 16) | ((uint) 49 << 2);
        const uint FSCTL_SET_ZERO_DATA = ((uint) 0x00000009 << 16) | ((uint) 50 << 2) | ((uint) 2 << 14);

        static bool SupportsSparse = true;

        public static void CreateSparse (string filename, long length)
        {
            if (!SupportsSparse)
                return;

            // Ensure we have the full path
            filename = Path.GetFullPath (filename);
            try {
                if (!CanCreateSparse (filename))
                    return;

                // Create a file with the sparse flag enabled

                uint bytesReturned = 0;
                uint access = 0x40000000;         // GenericWrite
                uint sharing = 0;                       // none
                uint attributes = 0x00000080;     // Normal
                uint creation = 1;                // Only create if new

                using SafeFileHandle handle = CreateFileW (filename, access, sharing, IntPtr.Zero, creation, attributes, IntPtr.Zero);
                // If we couldn't create the file, bail out
                if (handle.IsInvalid)
                    return;

                // If we can't set the sparse bit, bail out
                if (!DeviceIoControl (handle, FSCTL_SET_SPARSE, IntPtr.Zero, 0, IntPtr.Zero, 0, ref bytesReturned, IntPtr.Zero))
                    return;

                // Tell the filesystem to mark bytes 0 -> length as sparse zeros
                var data = new FILE_ZERO_DATA_INFORMATION (0, length);
                uint structSize = (uint) Marshal.SizeOf (data);
                IntPtr ptr = Marshal.AllocHGlobal ((int) structSize);

                try {
                    Marshal.StructureToPtr (data, ptr, false);
                    DeviceIoControl (handle, FSCTL_SET_ZERO_DATA, ptr,
                        structSize, IntPtr.Zero, 0, ref bytesReturned, IntPtr.Zero);
                } finally {
                    Marshal.FreeHGlobal (ptr);
                }
            } catch (DllNotFoundException) {
                SupportsSparse = false;
            } catch (EntryPointNotFoundException) {
                SupportsSparse = false;
            } catch {
                // Ignore for now. Maybe if i keep hitting this i should abort future attemts
            }
        }
        static bool CanCreateSparse (string volume)
        {
            // Ensure full path is supplied
            volume = Path.GetPathRoot (volume)!;

            var volumeName = new StringBuilder (MAX_PATH);
            var systemName = new StringBuilder (MAX_PATH);

            bool result = GetVolumeInformationW (volume, volumeName, MAX_PATH, out _, out _, out uint fsFlags, systemName, MAX_PATH);
            return result && (fsFlags & FILE_SUPPORTS_SPARSE_FILES) == FILE_SUPPORTS_SPARSE_FILES;
        }


        [DllImport ("Kernel32.dll")]
        static extern bool DeviceIoControl (
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            IntPtr InBuffer,
            uint nInBufferSize,
            IntPtr OutBuffer,
            uint nOutBufferSize,
            ref uint pBytesReturned,
            [In] IntPtr lpOverlapped
        );

        [DllImportAttribute ("kernel32.dll")]
        static extern SafeFileHandle CreateFileW (
                [In][MarshalAsAttribute (UnmanagedType.LPWStr)] string lpFileName,
                uint dwDesiredAccess,
                uint dwShareMode,
                [In] IntPtr lpSecurityAttributes,
                uint dwCreationDisposition,
                uint dwFlagsAndAttributes,
                [In] IntPtr hTemplateFile
        );

        [DllImportAttribute ("kernel32.dll")]
        static extern bool GetVolumeInformationW (
            [In][MarshalAsAttribute (UnmanagedType.LPWStr)] string lpRootPathName,
            [Out][MarshalAsAttribute (UnmanagedType.LPWStr)] StringBuilder lpVolumeNameBuffer,
            uint nVolumeNameSize,
            out uint lpVolumeSerialNumber,
            out uint lpMaximumComponentLength,
            out uint lpFileSystemFlags,
            [Out][MarshalAsAttribute (UnmanagedType.LPWStr)] StringBuilder lpFileSystemNameBuffer,
            uint nFileSystemNameSize
        );
    }
}
