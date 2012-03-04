namespace Nouzuru
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// A class that is used to inject a DLL and initialize it in a remote process.
    /// </summary>
    public class DllInjector : PInteractor
    {
        /// <summary>
        /// Load a DLL in an external process.
        /// </summary>
        /// <param name="dllPath">The path to the DLL to be loaded.</param>
        /// <returns>Returns true if the DLL was successfully loaded in the target process.</returns>
        public bool InjectDll(string dllPath)
        {
            IntPtr injectHandle = IntPtr.Zero;
            IntPtr remoteString = IntPtr.Zero;
            IntPtr loadLibAddy = IntPtr.Zero;

            // If the PID has not been set, then not enough information is available for the injection.
            if (!this.IsOpen)
            {
                return false;
            }

            // Get a higher level access handle than the one originally used to open the process.
            injectHandle = WinApi.OpenProcess(
                WinApi.ProcessRights.CREATE_THREAD |
                WinApi.ProcessRights.QUERY_INFORMATION |
                WinApi.ProcessRights.VM_OPERATION |
                WinApi.ProcessRights.VM_WRITE |
                WinApi.ProcessRights.VM_READ,
                false,
                (uint)this.PID);

            if (injectHandle == null || injectHandle.Equals(IntPtr.Zero))
            {
#if DEBUG
                Console.Error.WriteLine("OpenProcess() failed: " + Marshal.GetLastWin32Error());
#endif
                return false;
            }

            // Get the address of the function that will load the DLL.
            loadLibAddy = WinApi.GetProcAddress(WinApi.GetModuleHandle("kernel32.dll"), "LoadLibraryA");

            // Allocate a section of memory in the target process to in which to store store the DLL path.
            remoteString = WinApi.VirtualAllocEx(
                injectHandle,
                IntPtr.Zero,
                (uint)dllPath.Length,
                WinApi.MemoryState.MEM_RESERVE | WinApi.MemoryState.MEM_COMMIT,
                WinApi.MemoryProtect.PAGE_READWRITE);

            if (remoteString == null || remoteString.Equals(IntPtr.Zero))
            {
#if DEBUG
                Console.Error.WriteLine("VirtualAllocEx() failed: " + Marshal.GetLastWin32Error());
#endif
                WinApi.CloseHandle(injectHandle);
                return false;
            }

            // Write the DLL name to the remote process' memory space.
            uint numBytesWritten = 0;
            bool wpm = WinApi.WriteProcessMemory(
                injectHandle, remoteString, this.GetBytes(dllPath), (uint)dllPath.Length, out numBytesWritten);

            if (!wpm)
            {
#if DEBUG
                Console.Error.WriteLine("WriteProcessMemory() failed: " + Marshal.GetLastWin32Error());
#endif
                WinApi.CloseHandle(injectHandle);
                return false;
            }

            // Load the DLL, by calling the LoadLibrary function in the remote process.
            uint threadId = 0;
            IntPtr result = WinApi.CreateRemoteThread(
                injectHandle, IntPtr.Zero, 0, loadLibAddy, remoteString, 0, out threadId);

            if (result == null || result.Equals(IntPtr.Zero) || threadId == 0)
            {
#if DEBUG
                Console.Error.WriteLine("CreateRemoteThread() failed: " + Marshal.GetLastWin32Error());
#endif
                WinApi.CloseHandle(injectHandle);
                return false;
            }

            // Clean up and exit.
            WinApi.CloseHandle(injectHandle);

            return true;
        }

        /// <summary>
        /// Converts a string to an array of bytes, without dealing with encoding.
        /// </summary>
        /// <param name="s">The string to be converted.</param>
        /// <returns>Returns an array of bytes that contains the same bytes as the provided string.</returns>
        private byte[] GetBytes(string s)
        {
            List<byte> bytes = new List<byte>();
            foreach (char c in s)
            {
                bytes.Add((byte)c);
            }

            return bytes.ToArray();
        }
    }
}
