namespace Nouzuru
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.InteropServices;
    using Microsoft.Win32.SafeHandles;

    /// <summary>
    /// An interface to some common Win32 API constants, structures, and methods.
    /// </summary>
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented",
        Justification = "These are documented on MSDN.")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1602:EnumerationItemsMustBeDocumented",
        Justification = "These are documented on MSDN.")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.NamingRules", "SA1307:AccessibleFieldsMustBeginWithUpperCaseLetter",
        Justification = "The naming scheme shall remain the same as the Win32 API, where appropriate.")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation",
        Justification = "The naming scheme shall remain the same as the Win32 API, where appropriate.")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate",
        Justification = "Some constant values do not fall under any enumeration group and properties are overkill " +
        "for constant values.")]
    public class WinApi
    {
        #region Constants

        internal static uint INFINITE = 0xFFFFFFFF;

        #endregion

        #region Delegates

        internal delegate uint PTHREAD_START_ROUTINE(IntPtr lpThreadParameter);

        #endregion

        #region Enumerations

        /// <summary>
        /// Documentation available at http://msdn.microsoft.com/en-us/library/windows/desktop/aa366786.aspx.
        /// </summary>
        [Flags]
        public enum MemoryProtect : uint
        {
            PAGE_EXECUTE = 0x10,
            PAGE_EXECUTE_READ = 0x20,
            PAGE_EXECUTE_READWRITE = 0x40,
            PAGE_EXECUTE_WRITECOPY = 0x80,
            PAGE_NOACCESS = 0x01,
            PAGE_READONLY = 0x02,
            PAGE_READWRITE = 0x04,
            PAGE_WRITECOPY = 0x08,
            PAGE_GUARD = 0x100,
            PAGE_NOCACHE = 0x200,
            PAGE_WRITECOMBINE = 0x400
        }

        /// <summary>
        /// Documentation available at http://msdn.microsoft.com/en-us/library/windows/desktop/aa366775.aspx.
        /// </summary>
        [Flags]
        public enum MemoryType : uint
        {
            MEM_IMAGE = 0x1000000,
            MEM_MAPPED = 0x40000,
            MEM_PRIVATE = 0x20000
        }

        [Flags]
        internal enum CONTEXT_FLAGS : uint
        {
            i386 = 0x10000,
            i486 = 0x10000,
            CONTROL = CONTEXT_FLAGS.i386 | 0x01,
            INTEGER = CONTEXT_FLAGS.i386 | 0x02,
            SEGMENTS = CONTEXT_FLAGS.i386 | 0x04,
            FLOATING_POINT = CONTEXT_FLAGS.i386 | 0x08,
            DEBUG_REGISTERS = CONTEXT_FLAGS.i386 | 0x10,
            EXTENDED_REGISTERS = CONTEXT_FLAGS.i386 | 0x20,
            FULL = CONTEXT_FLAGS.CONTROL | CONTEXT_FLAGS.INTEGER | CONTEXT_FLAGS.SEGMENTS,
            ALL =
                CONTEXT_FLAGS.CONTROL | CONTEXT_FLAGS.INTEGER | CONTEXT_FLAGS.SEGMENTS |
                CONTEXT_FLAGS.FLOATING_POINT | CONTEXT_FLAGS.DEBUG_REGISTERS | CONTEXT_FLAGS.EXTENDED_REGISTERS
        }

        internal enum DbgCode : uint
        {
            CONTINUE = 0x00010002,
            EXCEPTION_NOT_HANDLED = 0x80010001
        }

        internal enum DebugEventType : uint
        {
            EXCEPTION_DEBUG_EVENT = 1,
            CREATE_THREAD_DEBUG_EVENT = 2,
            CREATE_PROCESS_DEBUG_EVENT = 3,
            EXIT_THREAD_DEBUG_EVENT = 4,
            EXIT_PROCESS_DEBUG_EVENT = 5,
            LOAD_DLL_DEBUG_EVENT = 6,
            UNLOAD_DLL_DEBUG_EVENT = 7,
            OUTPUT_DEBUG_STRING_EVENT = 8,
            RIP_EVENT = 9
        }

        internal enum ExceptionType : uint
        {
            GUARD_PAGE = 0x80000001,
            DATATYPE_MISALIGNMENT = 0x80000002,
            BREAKPOINT = 0x80000003,
            SINGLE_STEP = 0x80000004,
            INVALID_HANDLE = 0xC0000008,
            ACCESS_VIOLATION = 0xC0000005,
            IN_PAGE_ERROR = 0xC0000006,
            ILLEGAL_INSTRUCTION = 0xC000001D,
            NONCONTINUABLE_EXCEPTION = 0xC0000025,
            INVALID_DISPOSITION = 0xC0000026,
            ARRAY_BOUNDS_EXCEEDED = 0xC000008C,
            FLT_DENORMAL_OPERAND = 0xC000008D,
            FLT_DIVIDE_BY_ZERO = 0xC000008E,
            FLT_INEXACT_RESULT = 0xC000008F,
            FLT_INVALID_OPERATION = 0xC0000090,
            FLT_OVERFLOW = 0xC0000091,
            FLT_STACK_CHECK = 0xC0000092,
            FLT_UNDERFLOW = 0xC0000093,
            INT_DIVIDE_BY_ZERO = 0xC0000094,
            INT_OVERFLOW = 0xC0000095,
            PRIV_INSTRUCTION = 0xC0000096,
            STACK_OVERFLOW = 0xC00000FD,
        }

        [Flags]
        internal enum MemoryState : uint
        {
            MEM_COMMIT = 0x1000,
            MEM_FREE = 0x10000,
            MEM_RESERVE = 0x2000,
            MEM_RESET = 0x80000
        }

        [Flags]
        internal enum ProcessRights : uint
        {
            ALL_ACCESS = 0x001F0FFF,
            TERMINATE = 0x00000001,
            CREATE_THREAD = 0x00000002,
            VM_OPERATION = 0x00000008,
            VM_READ = 0x00000010,
            VM_WRITE = 0x00000020,
            DUP_HANDLE = 0x00000040,
            CREATE_PROCESS = 0x00000080,
            SET_QUOTA = 0x00000100,
            SET_INFORMATION = 0x00000200,
            QUERY_INFORMATION = 0x00000400,
            SUSPEND_RESUME = 0x00000800,
            QUERY_LIMITED_INFORMATION = 0x1000
        }

        [Flags]
        internal enum ThreadAccess : uint
        {
            TERMINATE = 0x0001,
            SUSPEND_RESUME = 0x0002,
            GET_CONTEXT = 0x0008,
            SET_CONTEXT = 0x0010,
            SET_INFORMATION = 0x0020,
            QUERY_INFORMATION = 0x0040,
            SET_THREAD_TOKEN = 0x0080,
            IMPERSONATE = 0x0100,
            DIRECT_IMPERSONATION = 0x0200,
            SET_LIMITED_INFORMATION = 0x0400,
            QUERY_LIMITED_INFORMATION = 0x0800,
            SYNCHRONIZE = 0x00100000,
            ALL_ACCESS =
                ThreadAccess.TERMINATE | ThreadAccess.SUSPEND_RESUME | ThreadAccess.GET_CONTEXT |
                ThreadAccess.SET_CONTEXT | ThreadAccess.SET_INFORMATION | ThreadAccess.QUERY_INFORMATION |
                ThreadAccess.SET_THREAD_TOKEN | ThreadAccess.IMPERSONATE | ThreadAccess.DIRECT_IMPERSONATION |
                ThreadAccess.SET_LIMITED_INFORMATION | ThreadAccess.QUERY_LIMITED_INFORMATION |
                ThreadAccess.SYNCHRONIZE
        }

        #endregion

        #region Methods

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool ContinueDebugEvent(uint dwProcessId, uint dwThreadId, DbgCode dwContinueStatus);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr CreateRemoteThread(
            IntPtr hProcess,
            IntPtr lpThreadAttributes,
            uint dwStackSize,
            IntPtr lpStartAddress,
            IntPtr lpParameter,
            uint dwCreationFlags,
            out uint lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool DebugActiveProcess(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool DebugActiveProcessStop(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool DebugSetProcessKillOnExit(bool killOnExit);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern void GetNativeSystemInfo(out SYSTEM_INFO lpSystemInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetThreadContext(IntPtr hThread, ref CONTEXT lpContext);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr OpenProcess(ProcessRights dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool ReadProcessMemory(
            IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint dwSize, out uint lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            [Out, MarshalAs(UnmanagedType.AsAny)] object lpBuffer,
            uint dwSize,
            out uint lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetThreadContext(IntPtr hThread, ref CONTEXT lpContext);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint SuspendThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool ReadProcessMemory(
            IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, uint dwSize, out uint lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr VirtualAllocEx(
            IntPtr hProcess, IntPtr lpAddress, uint dwSize, MemoryState flAllocationType, MemoryProtect flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool VirtualQueryEx(
            IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool WaitForDebugEvent(ref DEBUG_EVENT lpDebugEvent, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool WriteProcessMemory(
            IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out uint lpNumberOfBytesWritten);

        #endregion

        #region Structures

        /// <summary>
        /// Documentation available at http://msdn.microsoft.com/en-us/library/windows/desktop/aa366775.aspx.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORY_BASIC_INFORMATION
        {
            internal IntPtr BaseAddress;
            internal IntPtr AllocationBase;
            internal uint AllocationProtect;
            internal IntPtr RegionSize;
            internal MemoryState State;
            internal MemoryProtect Protect;
            internal MemoryType Type;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CONTEXT
        {
            internal CONTEXT_FLAGS ContextFlags;
            internal uint Dr0;
            internal uint Dr1;
            internal uint Dr2;
            internal uint Dr3;
            internal uint Dr6;
            internal uint Dr7;
            internal FLOATING_SAVE_AREA FloatSave;
            internal uint SegGs;
            internal uint SegFs;
            internal uint SegEs;
            internal uint SegDs;
            internal uint Edi;
            internal uint Esi;
            internal uint Ebx;
            internal uint Edx;
            internal uint Ecx;
            internal uint Eax;
            internal uint Ebp;
            internal uint Eip;
            internal uint SegCs;
            internal uint EFlags;
            internal uint Esp;
            internal uint SegSs;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
            internal byte[] ExtendedRegisters;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CREATE_PROCESS_DEBUG_INFO
        {
            internal IntPtr hFile;
            internal IntPtr hProcess;
            internal IntPtr hThread;
            internal IntPtr lpBaseOfImage;
            internal uint dwDebugInfoFileOffset;
            internal uint nDebugInfoSize;
            internal IntPtr lpThreadLocalBase;
            internal PTHREAD_START_ROUTINE lpStartAddress;
            internal IntPtr lpImageName;
            internal ushort fUnicode;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CREATE_THREAD_DEBUG_INFO
        {
            internal IntPtr hThread;
            internal IntPtr lpThreadLocalBase;
            internal PTHREAD_START_ROUTINE lpStartAddress;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct DEBUG_EVENT_Union
        {
            [FieldOffset(0)]
            internal EXCEPTION_DEBUG_INFO Exception;
            /*
            [FieldOffset(0)]
            internal CREATE_THREAD_DEBUG_INFO CreateThread;

            [FieldOffset(0)]
            internal CREATE_PROCESS_DEBUG_INFO CreateProcessInfo;

            [FieldOffset(0)]
            internal EXIT_THREAD_DEBUG_INFO ExitThread;

            [FieldOffset(0)]
            internal EXIT_PROCESS_DEBUG_INFO ExitProcess;

            [FieldOffset(0)]
            internal LOAD_DLL_DEBUG_INFO LoadDll;

            [FieldOffset(0)]
            internal UNLOAD_DLL_DEBUG_INFO UnloadDll;

            [FieldOffset(0)]
            internal OUTPUT_DEBUG_STRING_INFO DebugString;

            [FieldOffset(0)]
            internal RIP_INFO RipInfo;*/
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct DEBUG_EVENT
        {
            internal uint dwDebugEventCode;
            internal uint dwProcessId;
            internal uint dwThreadId;
            internal DEBUG_EVENT_Union u;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct EXCEPTION_DEBUG_INFO
        {
            internal EXCEPTION_RECORD ExceptionRecord;
            internal uint dwFirstChance;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct EXCEPTION_RECORD
        {
            internal uint ExceptionCode;
            internal uint ExceptionFlags;
            internal IntPtr ExceptionRecord;
            internal IntPtr ExceptionAddress;
            internal uint NumberParameters;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15, ArraySubType = UnmanagedType.U4)]
            internal uint[] ExceptionInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct EXIT_PROCESS_DEBUG_INFO
        {
            internal uint dwExitCode;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct EXIT_THREAD_DEBUG_INFO
        {
            internal uint dwExitCode;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct FLOATING_SAVE_AREA
        {
            internal uint ControlWord;
            internal uint StatusWord;
            internal uint TagWord;
            internal uint ErrorOffset;
            internal uint ErrorSelector;
            internal uint DataOffset;
            internal uint DataSelector;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 80)]
            internal byte[] RegisterArea;
            internal uint Cr0NpxState;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct LOAD_DLL_DEBUG_INFO
        {
            internal IntPtr hFile;
            internal IntPtr lpBaseOfDll;
            internal uint dwDebugInfoFileOffset;
            internal uint nDebugInfoSize;
            internal IntPtr lpImageName;
            internal ushort fUnicode;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct OUTPUT_DEBUG_STRING_INFO
        {
            [MarshalAs(UnmanagedType.LPStr)]
            internal string lpDebugStringData;
            internal ushort fUnicode;
            internal ushort nDebugStringLength;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RIP_INFO
        {
            internal uint dwError;
            internal uint dwType;
        }

        [SuppressMessage("Microsoft.StyleCop.CSharp.OrderingRules", "SA1202:ElementsMustBeOrderedByAccess",
            Justification = "The ordering must not change within structures, since this would affect functionality.")]
        [StructLayout(LayoutKind.Sequential)]
        internal struct SYSTEM_INFO
        {
            internal ushort processorArchitecture;
            private ushort reserved;
            internal uint pageSize;
            internal IntPtr minimumApplicationAddress;
            internal IntPtr maximumApplicationAddress;
            internal IntPtr activeProcessorMask;
            internal uint numberOfProcessors;
            internal uint processorType;
            internal uint allocationGranularity;
            internal ushort processorLevel;
            internal ushort processorRevision;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct UNLOAD_DLL_DEBUG_INFO
        {
            internal IntPtr lpBaseOfDll;
        }

        #endregion
    }
}
