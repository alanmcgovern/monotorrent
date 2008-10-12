using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Microsoft.Win32.SafeHandles;
using System.Threading;
using System.Runtime.InteropServices;

namespace MonoTorrent.Client
{
    internal static class SparseFile
    {
        [StructLayout(LayoutKind.Sequential)]
        struct FILE_ZERO_DATA_INFORMATION
        {
            public FILE_ZERO_DATA_INFORMATION(long offset, long count)
            {
                FileOffset = offset;
                BeyondFinalZero = offset + count;
            }

            public long FileOffset;
            public long BeyondFinalZero;
        }

        private const int MAX_PATH = 260;
        private const uint FILE_SUPPORTS_SPARSE_FILES = 64;
        private const uint FSCTL_SET_SPARSE = ((uint)0x00000009 << 16) | ((uint)49 << 2);
        private const uint FSCTL_SET_ZERO_DATA = ((uint)0x00000009 << 16) | ((uint)50 << 2) | ((uint)2 << 14);

        private static bool SupportsSparse = true;

        public static void CreateSparse(string filename, long length)
        {
            if (!SupportsSparse)
                return;

            // Ensure we have the full path
            filename = Path.GetFullPath(filename);
            try
            {
                if (!CanCreateSparse(filename))
                    return;

                // Create a file with the sparse flag enabled

                uint bytesReturned = 0;
                uint access = (uint)0x40000000;         // GenericWrite
                uint sharing = 0;                       // none
                uint attributes = (uint)0x00000080;     // Normal
                uint creation = (uint)1;                // Only create if new

                using (SafeFileHandle handle = CreateFileW(filename, access, sharing, IntPtr.Zero, creation, attributes, IntPtr.Zero))
                {
                    // If we couldn't create the file, bail out
                    if (handle.IsInvalid)
                        return;

                    // If we can't set the sparse bit, bail out
                    if (!DeviceIoControl(handle, FSCTL_SET_SPARSE, IntPtr.Zero, 0, IntPtr.Zero, 0, ref bytesReturned, IntPtr.Zero))
                        return;

                    // Tell the filesystem to mark bytes 0 -> length as sparse zeros
                    FILE_ZERO_DATA_INFORMATION data = new FILE_ZERO_DATA_INFORMATION(0, length);
                    uint structSize = (uint)Marshal.SizeOf(data);
                    IntPtr ptr = Marshal.AllocHGlobal((int)structSize);

                    try
                    {
                        Marshal.StructureToPtr(data, ptr, false);
                        DeviceIoControl(handle, FSCTL_SET_ZERO_DATA, ptr,
                                        structSize, IntPtr.Zero, 0, ref bytesReturned, IntPtr.Zero);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }
            }
            catch (DllNotFoundException)
            {
                SupportsSparse = false;
            }
            catch (EntryPointNotFoundException)
            {
                SupportsSparse = false;
            }
            catch
            {
                // Ignore for now. Maybe if i keep hitting this i should abort future attemts
            }
        }
        private static bool CanCreateSparse(string volume)
        {
            // Ensure full path is supplied
            volume = Path.GetPathRoot(volume);

            StringBuilder volumeName = new StringBuilder(MAX_PATH);
            StringBuilder systemName = new StringBuilder(MAX_PATH);

            uint fsFlags, serialNumber, maxComponent;

            bool result = GetVolumeInformationW(volume, volumeName, MAX_PATH, out serialNumber, out maxComponent, out fsFlags, systemName, MAX_PATH);
            return result && (fsFlags & FILE_SUPPORTS_SPARSE_FILES) == FILE_SUPPORTS_SPARSE_FILES;
        }


        [DllImport("Kernel32.dll")]
        private static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            IntPtr InBuffer,
            uint nInBufferSize,
            IntPtr OutBuffer,
            uint nOutBufferSize,
            ref uint pBytesReturned,
            [In] IntPtr lpOverlapped
        );

        [DllImportAttribute("kernel32.dll")]
        private static extern SafeFileHandle CreateFileW(
                [In][MarshalAsAttribute(UnmanagedType.LPWStr)] string lpFileName,
                uint dwDesiredAccess,
                uint dwShareMode,
                [In] IntPtr lpSecurityAttributes,
                uint dwCreationDisposition,
                uint dwFlagsAndAttributes,
                [In] IntPtr hTemplateFile
        );

        [DllImportAttribute("kernel32.dll")]
        private static extern bool GetVolumeInformationW(
            [In] [MarshalAsAttribute(UnmanagedType.LPWStr)] string lpRootPathName,
            [Out] [MarshalAsAttribute(UnmanagedType.LPWStr)] StringBuilder lpVolumeNameBuffer,
            uint nVolumeNameSize,
            out uint lpVolumeSerialNumber,
            out uint lpMaximumComponentLength,
            out uint lpFileSystemFlags,
            [Out] [MarshalAsAttribute(UnmanagedType.LPWStr)] StringBuilder lpFileSystemNameBuffer,
            uint nFileSystemNameSize
        );
    }
}
