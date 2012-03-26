namespace Nouzuru
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// A collection of various miscellaneous functions that do not directly have to do with memory auditing.
    /// </summary>
    public static class Auxiliary
    {
        /// <summary>
        /// Gets the bytes that make up the supplied object.
        /// </summary>
        /// <typeparam name="T">The type of object being supplied.</typeparam>
        /// <param name="o">The object to be converted to a byte array.</param>
        /// <returns>Returns a byte array equivalent to the bytes representing the object.</returns>
        public static byte[] GetBytes<T>(T o)
        {
            int size = Marshal.SizeOf(o);
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);

            Marshal.StructureToPtr(o, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);

            return arr;
        }

        /// <summary>
        /// Gets the name of the file referenced by the supplied file handle.
        /// </summary>
        /// <param name="fileHandle">The handle to the file.</param>
        /// <returns>Returns the full path to the file.</returns>
        /// <remarks>Thanks to Rudi for providing this code. http://stackoverflow.com/a/3314313</remarks>
        public static string GetFileNameFromHandle(IntPtr fileHandle)
        {
            string fileName = string.Empty;
            IntPtr fileMap = IntPtr.Zero, fileSizeHi = IntPtr.Zero;
            uint fileSizeLo = 0;

            fileSizeLo = WinApi.GetFileSize(fileHandle, fileSizeHi);

            if (fileSizeLo == 0)
            {
                // Cannot map a 0 byte file.
                return "Empty file.";
            }

            fileMap =
                WinApi.CreateFileMapping(fileHandle, IntPtr.Zero, WinApi.FileMapProtection.PageReadonly, 0, 1, null);

            if (fileMap != IntPtr.Zero)
            {
                IntPtr memPtr = WinApi.MapViewOfFile(fileMap, WinApi.FileMapAccess.FileMapRead, 0, 0, 1);
                if (memPtr != IntPtr.Zero)
                {
                    StringBuilder fn = new StringBuilder(250);
                    WinApi.GetMappedFileName(System.Diagnostics.Process.GetCurrentProcess().Handle, memPtr, fn, 250);
                    if (fn.Length > 0)
                    {
                        WinApi.UnmapViewOfFile(memPtr);
                        WinApi.CloseHandle(fileHandle);
                        return fn.ToString();
                    }
                    else
                    {
                        WinApi.UnmapViewOfFile(memPtr);
                        WinApi.CloseHandle(fileHandle);
                        return "Empty filename.";
                    }
                }
            }

            return "Empty filemap handle.";
        }
    }
}
