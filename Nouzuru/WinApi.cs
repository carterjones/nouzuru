namespace Nouzuru
{
#pragma warning disable 1591

    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.InteropServices;
    using System.Text;
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

        public static uint INFINITE = 0xFFFFFFFF;
        public static uint WM_CLOSE = 0x0010;

        #endregion

        #region Delegates

        public delegate uint PTHREAD_START_ROUTINE(IntPtr lpThreadParameter);

        #endregion

        #region Enumerations

        [Flags]
        public enum FileMapAccess : uint
        {
            FileMapCopy = 0x0001,
            FileMapWrite = 0x0002,
            FileMapRead = 0x0004,
            FileMapAllAccess = 0x001f,
            fileMapExecute = 0x0020,
        }

        [Flags]
        public enum FileMapProtection : uint
        {
            PageReadonly = 0x02,
            PageReadWrite = 0x04,
            PageWriteCopy = 0x08,
            PageExecuteRead = 0x20,
            PageExecuteReadWrite = 0x40,
            SectionCommit = 0x8000000,
            SectionImage = 0x1000000,
            SectionNoCache = 0x10000000,
            SectionReserve = 0x4000000,
        }

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
        public enum CONTEXT_FLAGS : uint
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

        public enum DbgCode : uint
        {
            CONTINUE = 0x00010002,
            EXCEPTION_NOT_HANDLED = 0x80010001
        }

        public enum DebugEventType : uint
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

        public enum ExceptionType : uint
        {
            // Normal debug execptions.
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

            // Wow64-related debug exceptions.
            STATUS_WX86_UNSIMULATE = 0x4000001C,
            STATUS_WX86_CONTINUE = 0x4000001D,
            STATUS_WX86_SINGLE_STEP = 0x4000001E,
            STATUS_WX86_BREAKPOINT = 0x4000001F,
            STATUS_WX86_EXCEPTION_CONTINUE = 0x40000020,
            STATUS_WX86_EXCEPTION_LASTCHANCE = 0x40000021,
            STATUS_WX86_EXCEPTION_CHAIN = 0x40000022,
        }

        [Flags]
        public enum MemoryState : uint
        {
            MEM_COMMIT = 0x1000,
            MEM_FREE = 0x10000,
            MEM_RESERVE = 0x2000,
            MEM_RESET = 0x80000
        }

        [Flags]
        public enum ProcessCreationFlags : uint
        {
            NONE = 0x00000000,
            CREATE_BREAKAWAY_FROM_JOB = 0x01000000,
            CREATE_DEFAULT_ERROR_MODE = 0x04000000,
            CREATE_NEW_CONSOLE = 0x00000010,
            CREATE_NEW_PROCESS_GROUP = 0x00000200,
            CREATE_NO_WINDOW = 0x08000000,
            CREATE_PROTECTED_PROCESS = 0x00040000,
            CREATE_PRESERVE_CODE_AUTHZ_LEVEL = 0x02000000,
            CREATE_SEPARATE_WOW_VDM = 0x00001000,
            CREATE_SHARED_WOW_VDM = 0x00001000,
            CREATE_SUSPENDED = 0x00000004,
            CREATE_UNICODE_ENVIRONMENT = 0x00000400,
            DEBUG_ONLY_THIS_PROCESS = 0x00000002,
            DEBUG_PROCESS = 0x00000001,
            DETACHED_PROCESS = 0x00000008,
            EXTENDED_STARTUPINFO_PRESENT = 0x00080000,
            INHERIT_PARENT_AFFINITY = 0x00010000
        }

        [Flags]
        public enum ProcessRights : uint
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

        public enum RipInfoTypes : uint
        {
            SLE_ERROR = 0x00000001,
            SLE_MINORERROR = 0x00000002,
            SLE_WARNING = 0x00000003
        }

        [Flags]
        public enum ThreadAccess : uint
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
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ContinueDebugEvent(uint dwProcessId, uint dwThreadId, DbgCode dwContinueStatus);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateFileMapping(
            IntPtr hFile,
            IntPtr lpFileMappingAttributes,
            FileMapProtection flProtect,
            uint dwMaximumSizeHigh,
            uint dwMaximumSizeLow,
            [MarshalAs(UnmanagedType.LPTStr)] string lpName);

        [DllImport("kernel32.dll")]
        public static extern bool CreateProcess(
            string lpApplicationName,
            string lpCommandLine,
            ref SECURITY_ATTRIBUTES lpProcessAttributes,
            ref SECURITY_ATTRIBUTES lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            [In] ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll")]
        public static extern bool DebugBreakProcess(IntPtr hProcess);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(IntPtr lpClassName, string lpWindowName);

        public static IntPtr FindWindow(string lpWindowName)
        {
            return WinApi.FindWindow(IntPtr.Zero, lpWindowName);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateRemoteThread(
            IntPtr hProcess,
            IntPtr lpThreadAttributes,
            uint dwStackSize,
            IntPtr lpStartAddress,
            IntPtr lpParameter,
            uint dwCreationFlags,
            out uint lpThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DebugActiveProcess(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DebugActiveProcessStop(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DebugSetProcessKillOnExit(bool killOnExit);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetFileSize(IntPtr hFile, IntPtr lpFileSizeHigh);

        [DllImport("psapi.dll", SetLastError = true)]
        public static extern uint GetMappedFileName(
            IntPtr hProcess, IntPtr lpv, StringBuilder lpFilename, uint nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetModuleFileName(
            [In] IntPtr hModule, [Out] StringBuilder lpFilename, [In] [MarshalAs(UnmanagedType.U4)] int nSize);

        [DllImport("psapi.dll", SetLastError = true)]
        public static extern uint GetModuleFileNameEx(
            IntPtr hProc, IntPtr hModule, StringBuilder lpFilename, [MarshalAs(UnmanagedType.U4)] int nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern void GetNativeSystemInfo(out SYSTEM_INFO lpSystemInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetProcessId(IntPtr hProcess);

        [DllImport("kernel32.dll")]
        public static extern bool GetProcessTimes(
            IntPtr hProcess,
            out System.Runtime.InteropServices.ComTypes.FILETIME lpCreationTime,
            out System.Runtime.InteropServices.ComTypes.FILETIME lpExitTime,
            out System.Runtime.InteropServices.ComTypes.FILETIME lpKernelTime,
            out System.Runtime.InteropServices.ComTypes.FILETIME lpUserTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetThreadContext(IntPtr hThread, ref CONTEXT lpContext);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr MapViewOfFile(
            IntPtr hFileMappingObject,
            FileMapAccess dwDesiredAccess,
            uint dwFileOffsetHigh,
            uint dwFileOffsetLow,
            uint dwNumberOfBytesToMap);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(ProcessRights dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(
            IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint dwSize, out uint lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            [Out, MarshalAs(UnmanagedType.AsAny)] object lpBuffer,
            uint dwSize,
            out uint lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetThreadContext(IntPtr hThread, ref CONTEXT lpContext);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint SuspendThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(
            IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, uint dwSize, out uint lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr VirtualAllocEx(
            IntPtr hProcess, IntPtr lpAddress, uint dwSize, MemoryState flAllocationType, MemoryProtect flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool VirtualQueryEx(
            IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WaitForDebugEvent(ref DEBUG_EVENT lpDebugEvent, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(
            IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out uint lpNumberOfBytesWritten);

        #endregion

        #region Structures

        /// <summary>
        /// Documentation available at http://msdn.microsoft.com/en-us/library/windows/desktop/aa366775.aspx.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public MemoryState State;
            public MemoryProtect Protect;
            public MemoryType Type;
        }

#if WIN64
        [StructLayout(LayoutKind.Sequential)]
        public struct M128A
        {
            public ulong Low;
            public long High;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XMM_SAVE_AREA32
        {
            public ushort ControlWord;
            public ushort StatusWord;
            public char TagWord;
            public char Reserved1;
            public ushort ErrorOpcode;
            public uint ErrorOffset;
            public ushort ErrorSelector;
            public ushort Reserved2;
            public uint DataOffset;
            public ushort DataSelector;
            public ushort Reserved3;
            public uint MxCsr;
            public uint MxCsrMask;

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 8)]
            public M128A[] FloatRegisters;

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 16)]
            public M128A[] XmmRegisters;

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 96)]
            public char[] Reserved4;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct _CONTEXT_FLOATING_POINT_STATE_UNION_STRUCT
        {
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 2)]
            public M128A[] Header;

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 8)]
            public M128A[] Legacy;
            
            public M128A Xmm0;
            public M128A Xmm1;
            public M128A Xmm2;
            public M128A Xmm3;
            public M128A Xmm4;
            public M128A Xmm5;
            public M128A Xmm6;
            public M128A Xmm7;
            public M128A Xmm8;
            public M128A Xmm9;
            public M128A Xmm10;
            public M128A Xmm11;
            public M128A Xmm12;
            public M128A Xmm13;
            public M128A Xmm14;
            public M128A Xmm15;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct _CONTEXT_FLOATING_POINT_STATE_UNION
        {
            // When this is uncommented, it causes runtime errors.
            ////[FieldOffset(0)]
            ////public XMM_SAVE_AREA32 FltSave;

            [FieldOffset(0)]
            public _CONTEXT_FLOATING_POINT_STATE_UNION_STRUCT s;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CONTEXT
        {
            // Register parameter home addresses.
            public ulong P1Home;
            public ulong P2Home;
            public ulong P3Home;
            public ulong P4Home;
            public ulong P5Home;
            public ulong P6Home;

            // Control flags.
            public CONTEXT_FLAGS ContextFlags;
            public uint MxCsr;

            // Segment Registers and processor flags.
            public ushort SegCs;
            public ushort SegDs;
            public ushort SegEs;
            public ushort SegFs;
            public ushort SegGs;
            public ushort SegSs;
            public uint EFlags;

            // Debug registers
            public ulong Dr0;
            public ulong Dr1;
            public ulong Dr2;
            public ulong Dr3;
            public ulong Dr6;
            public ulong Dr7;

            // Integer registers.
            public ulong Rax;
            public ulong Rcx;
            public ulong Rdx;
            public ulong Rbx;
            public ulong Rsp;
            public ulong Rbp;
            public ulong Rsi;
            public ulong Rdi;
            public ulong R8;
            public ulong R9;
            public ulong R10;
            public ulong R11;
            public ulong R12;
            public ulong R13;
            public ulong R14;
            public ulong R15;

            // Program counter.
            public ulong Rip;

            // Floating point state.
            public _CONTEXT_FLOATING_POINT_STATE_UNION u;

            // Vector registers.
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 26)]
            public M128A[] VectorRegister;
            public ulong VectorControl;

            // Special debug control registers.
            public ulong DebugControl;
            public ulong LastBranchToRip;
            public ulong LastBranchFromRip;
            public ulong LastExceptionToRip;
            public ulong LastExceptionFromRip;

            /// <summary>
            /// Gets a platform independent accessor for the largest *ax register.
            /// </summary>
            public ulong _ax
            {
                get { return this.Rax; }
            }

            /// <summary>
            /// Gets a platform independent accessor for the largest *bx register.
            /// </summary>
            public ulong _bx
            {
                get { return this.Rbx; }
            }

            /// <summary>
            /// Gets a platform independent accessor for the largest *cx register.
            /// </summary>
            public ulong _cx
            {
                get { return this.Rcx; }
            }

            /// <summary>
            /// Gets a platform independent accessor for the largest *dx register.
            /// </summary>
            public ulong _dx
            {
                get { return this.Rdx; }
            }

            /// <summary>
            /// Gets a platform independent accessor for the largest *sp register.
            /// </summary>
            public ulong _sp
            {
                get { return this.Rsp; }
            }

            /// <summary>
            /// Gets a platform independent accessor for the largest *bp register.
            /// </summary>
            public ulong _bp
            {
                get { return this.Rbp; }
            }

            /// <summary>
            /// Gets a platform independent accessor for the largest *di register.
            /// </summary>
            public ulong _di
            {
                get { return this.Rdi; }
            }

            /// <summary>
            /// Gets a platform independent accessor for the largest *si register.
            /// </summary>
            public ulong _si
            {
                get { return this.Rsi; }
            }

            /// <summary>
            /// Gets a platform independent accessor for the largest *ip register.
            /// </summary>
            public ulong _ip
            {
                get { return this.Rip; }
            }
        }
#else
        [StructLayout(LayoutKind.Sequential)]
        public struct CONTEXT
        {
            public CONTEXT_FLAGS ContextFlags;
            public uint Dr0;
            public uint Dr1;
            public uint Dr2;
            public uint Dr3;
            public uint Dr6;
            public uint Dr7;
            public FLOATING_SAVE_AREA FloatSave;
            public uint SegGs;
            public uint SegFs;
            public uint SegEs;
            public uint SegDs;
            public uint Edi;
            public uint Esi;
            public uint Ebx;
            public uint Edx;
            public uint Ecx;
            public uint Eax;
            public uint Ebp;
            public uint Eip;
            public uint SegCs;
            public uint EFlags;
            public uint Esp;
            public uint SegSs;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
            public byte[] ExtendedRegisters;

            /// <summary>
            /// Gets Gets a platform independent accessor for the largest *ax register.
            /// </summary>
            public uint _ax
            {
                get { return this.Eax; }
            }

            /// <summary>
            /// Gets Gets a platform independent accessor for the largest *bx register.
            /// </summary>
            public uint _bx
            {
                get { return this.Ebx; }
            }

            /// <summary>
            /// Gets a platform independent accessor for the largest *cx register.
            /// </summary>
            public uint _cx
            {
                get { return this.Ecx; }
            }

            /// <summary>
            /// Gets a platform independent accessor for the largest *dx register.
            /// </summary>
            public uint _dx
            {
                get { return this.Edx; }
            }

            /// <summary>
            /// Gets a platform independent accessor for the largest *sp register.
            /// </summary>
            public uint _sp
            {
                get { return this.Esp; }
            }

            /// <summary>
            /// Gets a platform independent accessor for the largest *bp register.
            /// </summary>
            public uint _bp
            {
                get { return this.Ebp; }
            }

            /// <summary>
            /// Gets a platform independent accessor for the largest *di register.
            /// </summary>
            public uint _di
            {
                get { return this.Edi; }
            }

            /// <summary>
            /// Gets a platform independent accessor for the largest *si register.
            /// </summary>
            public uint _si
            {
                get { return this.Esi; }
            }

            /// <summary>
            /// Gets a platform independent accessor for the largest *ip register.
            /// </summary>
            public uint _ip
            {
                get { return this.Eip; }
            }
        }
#endif

        [StructLayout(LayoutKind.Sequential)]
        public struct CREATE_PROCESS_DEBUG_INFO
        {
            public IntPtr hFile;
            public IntPtr hProcess;
            public IntPtr hThread;
            public IntPtr lpBaseOfImage;
            public uint dwDebugInfoFileOffset;
            public uint nDebugInfoSize;
            public IntPtr lpThreadLocalBase;
            public PTHREAD_START_ROUTINE lpStartAddress;
            public IntPtr lpImageName;
            public ushort fUnicode;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CREATE_THREAD_DEBUG_INFO
        {
            public IntPtr hThread;
            public IntPtr lpThreadLocalBase;
            public IntPtr lpStartAddress;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct DEBUG_EVENT
        {
            [FieldOffset(0)]
            public uint dwDebugEventCode;
            [FieldOffset(4)]
            public uint dwProcessId;
            [FieldOffset(8)]
            public uint dwThreadId;
#if WIN64
            [FieldOffset(16)]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 164, ArraySubType = UnmanagedType.U1)]
#else
            [FieldOffset(12)]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 86, ArraySubType = UnmanagedType.U1)]
#endif
            private byte[] debugInfo;

            public EXCEPTION_DEBUG_INFO Exception
            {
                get { return GetDebugInfo<EXCEPTION_DEBUG_INFO>(); }
            }

            public CREATE_THREAD_DEBUG_INFO CreateThread
            {
                get { return GetDebugInfo<CREATE_THREAD_DEBUG_INFO>(); }
            }

            public CREATE_PROCESS_DEBUG_INFO CreateProcessInfo
            {
                get { return GetDebugInfo<CREATE_PROCESS_DEBUG_INFO>(); }
            }

            public EXIT_THREAD_DEBUG_INFO ExitThread
            {
                get { return GetDebugInfo<EXIT_THREAD_DEBUG_INFO>(); }
            }

            public EXIT_PROCESS_DEBUG_INFO ExitProcess
            {
                get { return GetDebugInfo<EXIT_PROCESS_DEBUG_INFO>(); }
            }

            public LOAD_DLL_DEBUG_INFO LoadDll
            {
                get { return GetDebugInfo<LOAD_DLL_DEBUG_INFO>(); }
            }

            public UNLOAD_DLL_DEBUG_INFO UnloadDll
            {
                get { return GetDebugInfo<UNLOAD_DLL_DEBUG_INFO>(); }
            }

            public OUTPUT_DEBUG_STRING_INFO DebugString
            {
                get { return GetDebugInfo<OUTPUT_DEBUG_STRING_INFO>(); }
            }

            public RIP_INFO RipInfo
            {
                get { return GetDebugInfo<RIP_INFO>(); }
            }

            private T GetDebugInfo<T>() where T : struct
            {
                GCHandle handle = GCHandle.Alloc(this.debugInfo, GCHandleType.Pinned);
                T result = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
                handle.Free();
                return result;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct EXCEPTION_DEBUG_INFO
        {
#if WIN64
            public EXCEPTION_RECORD64 ExceptionRecord;
#else
            public EXCEPTION_RECORD32 ExceptionRecord;
#endif
            public uint dwFirstChance;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct EXCEPTION_RECORD32
        {
            public uint ExceptionCode;
            public uint ExceptionFlags;
            public IntPtr ExceptionRecord;
            public IntPtr ExceptionAddress;
            public uint NumberParameters;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15)]
            public uint[] ExceptionInformation;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct EXCEPTION_RECORD64
        {
            [FieldOffset(0)]
            public uint ExceptionCode;
            [FieldOffset(4)]
            public uint ExceptionFlags;
            [FieldOffset(8)]
            public IntPtr ExceptionRecord;
            [FieldOffset(16)]
            public IntPtr ExceptionAddress;
            [FieldOffset(24)]
            public uint NumberParameters;
            [FieldOffset(32)]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15)]
            public ulong[] ExceptionInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct EXIT_PROCESS_DEBUG_INFO
        {
            public uint dwExitCode;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct EXIT_THREAD_DEBUG_INFO
        {
            public uint dwExitCode;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FLOATING_SAVE_AREA
        {
            public uint ControlWord;
            public uint StatusWord;
            public uint TagWord;
            public uint ErrorOffset;
            public uint ErrorSelector;
            public uint DataOffset;
            public uint DataSelector;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 80)]
            public byte[] RegisterArea;
            public uint Cr0NpxState;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LOAD_DLL_DEBUG_INFO
        {
            public IntPtr hFile;
            public IntPtr lpBaseOfDll;
            public uint dwDebugInfoFileOffset;
            public uint nDebugInfoSize;
            public IntPtr lpImageName;
            public ushort fUnicode;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct OUTPUT_DEBUG_STRING_INFO
        {
            public IntPtr lpDebugStringData;
            public ushort fUnicode;
            public ushort nDebugStringLength;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RIP_INFO
        {
            public uint dwError;
            public uint dwType;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [SuppressMessage("Microsoft.StyleCop.CSharp.OrderingRules", "SA1202:ElementsMustBeOrderedByAccess",
            Justification = "The ordering must not change within structures, since this would affect functionality.")]
        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_INFO
        {
            public ushort processorArchitecture;
            private ushort reserved;
            public uint pageSize;
            public IntPtr minimumApplicationAddress;
            public IntPtr maximumApplicationAddress;
            public IntPtr activeProcessorMask;
            public uint numberOfProcessors;
            public uint processorType;
            public uint allocationGranularity;
            public ushort processorLevel;
            public ushort processorRevision;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct UNLOAD_DLL_DEBUG_INFO
        {
            public IntPtr lpBaseOfDll;
        }

        #endregion
    }

#pragma warning restore 1591
}
