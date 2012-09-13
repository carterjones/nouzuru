namespace Nouzuru
{
    using System;
    using System.Runtime.InteropServices;
    using System.Diagnostics;

    /// <summary>
    /// A collection of extension methods that add extra functionality to existing .NET classes.
    /// </summary>
    internal static class Extensions
    {
        /// <summary>
        /// Gets the bytes that make up the supplied string, bypassing any encoding adjustments.
        /// </summary>
        /// <param name="s">The string to be converted to a byte array.</param>
        /// <returns>Returns a byte array equivalent to the bytes representing the string.</returns>
        internal static byte[] GetBytes(this string s)
        {
            return ((object)s).GetBytes();
        }

        /// <summary>
        /// Gets the bytes that make up the supplied object.
        /// </summary>
        /// <param name="o">The object to be converted to a byte array.</param>
        /// <returns>Returns a byte array equivalent to the bytes representing the object.</returns>
        internal static byte[] GetBytes(this object o)
        {
            int size = Marshal.SizeOf(o);
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);

            Marshal.StructureToPtr(o, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);

            return arr;
        }

        internal static bool HasExitedSafe(this Process p)
        {
            System.Runtime.InteropServices.ComTypes.FILETIME create, exit, kernel, user;
            IntPtr handle = WinApi.OpenProcess(WinApi.ProcessRights.QUERY_LIMITED_INFORMATION, false, (uint)p.Id);
            WinApi.GetProcessTimes(handle, out create, out exit, out kernel, out user);
            WinApi.CloseHandle(handle);
            return (exit.dwHighDateTime != 0) && (exit.dwLowDateTime != 0);
        }
    }
}
