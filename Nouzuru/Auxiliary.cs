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
    }
}
