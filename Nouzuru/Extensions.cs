namespace Nouzuru
{
    using System;
    using System.Runtime.InteropServices;

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
    }
}
