namespace Nouzuru
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text;
    using Logger;
    using Distorm3cs;

    /// <summary>
    /// A class that logs each possible debug event and debug exception, attempting to provide detailed data about the
    /// most useful and relevant information for each handled event and exception.
    /// </summary>
    public class DebugMon : Debugger
    {
        #region Fields

        /// <summary>
        /// An event filter that will log all debug events.
        /// </summary>
        public const EventFilter AllEvents =
            EventFilter.EXCEPTION_DEBUG_EVENT |
            EventFilter.CREATE_THREAD_DEBUG_EVENT |
            EventFilter.CREATE_PROCESS_DEBUG_EVENT |
            EventFilter.EXIT_THREAD_DEBUG_EVENT |
            EventFilter.EXIT_PROCESS_DEBUG_EVENT |
            EventFilter.LOAD_DLL_DEBUG_EVENT |
            EventFilter.UNLOAD_DLL_DEBUG_EVENT |
            EventFilter.OUTPUT_DEBUG_STRING_EVENT |
            EventFilter.RIP_EVENT;

        /// <summary>
        /// An exception filter that will log all debug exceptions.
        /// </summary>
        public const ExceptionFilter AllExceptions =
            ExceptionFilter.GUARD_PAGE |
            ExceptionFilter.DATATYPE_MISALIGNMENT |
            ExceptionFilter.BREAKPOINT |
            ExceptionFilter.SINGLE_STEP |
            ExceptionFilter.INVALID_HANDLE |
            ExceptionFilter.ACCESS_VIOLATION |
            ExceptionFilter.IN_PAGE_ERROR |
            ExceptionFilter.ILLEGAL_INSTRUCTION |
            ExceptionFilter.NONCONTINUABLE_EXCEPTION |
            ExceptionFilter.INVALID_DISPOSITION |
            ExceptionFilter.ARRAY_BOUNDS_EXCEEDED |
            ExceptionFilter.FLT_DENORMAL_OPERAND |
            ExceptionFilter.FLT_DIVIDE_BY_ZERO |
            ExceptionFilter.FLT_INEXACT_RESULT |
            ExceptionFilter.FLT_INVALID_OPERATION |
            ExceptionFilter.FLT_OVERFLOW |
            ExceptionFilter.FLT_STACK_CHECK |
            ExceptionFilter.FLT_UNDERFLOW |
            ExceptionFilter.INT_DIVIDE_BY_ZERO |
            ExceptionFilter.INT_OVERFLOW |
            ExceptionFilter.PRIV_INSTRUCTION |
            ExceptionFilter.STACK_OVERFLOW;

        /// <summary>
        /// An exception filter that will log all (potentially) exploitable debug exceptions.
        /// </summary>
        public const ExceptionFilter ExploitableExceptions =
            ExceptionFilter.GUARD_PAGE |
            ExceptionFilter.ACCESS_VIOLATION |
            ExceptionFilter.INT_OVERFLOW |
            ExceptionFilter.STACK_OVERFLOW;

        /// <summary>
        /// All event and exception handlers in DebugMon send log entries to this logger.
        /// </summary>
        private Logger monitorLogger;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the DebugMon class.
        /// </summary>
        /// <param name="filename">The name of the file that will log all of the monitor's results.</param>
        public DebugMon(string filename)
        {
            this.monitorLogger = new Logger(Logger.Type.FILE | Logger.Type.CONSOLE, Logger.Level.NONE, filename);
        }

        #endregion

        #region Enumerations

        /// <summary>
        /// A collection of flags that correspond to debug events. These are used in filtering log entries.
        /// </summary>
        [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "*", Justification = "Documented by MSFT")]
        [Flags]
        public enum EventFilter : uint
        {
            EXCEPTION_DEBUG_EVENT = 0x0001,
            CREATE_THREAD_DEBUG_EVENT = 0x0002,
            CREATE_PROCESS_DEBUG_EVENT = 0x0004,
            EXIT_THREAD_DEBUG_EVENT = 0x0008,
            EXIT_PROCESS_DEBUG_EVENT = 0x0010,
            LOAD_DLL_DEBUG_EVENT = 0x0020,
            UNLOAD_DLL_DEBUG_EVENT = 0x0040,
            OUTPUT_DEBUG_STRING_EVENT = 0x0080,
            RIP_EVENT = 0x0100
        }

        /// <summary>
        /// A collection of flags that correspond to debug exceptions. These are used in filtering log entries.
        /// </summary>
        [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "*", Justification = "Documented by MSFT")]
        [Flags]
        public enum ExceptionFilter : ulong
        {
            GUARD_PAGE = 0x00000001,
            DATATYPE_MISALIGNMENT = 0x00000002,
            BREAKPOINT = 0x00000004,
            SINGLE_STEP = 0x00000008,
            INVALID_HANDLE = 0x00000010,
            ACCESS_VIOLATION = 0x00000020,
            IN_PAGE_ERROR = 0x00000040,
            ILLEGAL_INSTRUCTION = 0x00000080,
            NONCONTINUABLE_EXCEPTION = 0x00000100,
            INVALID_DISPOSITION = 0x00000200,
            ARRAY_BOUNDS_EXCEEDED = 0x00000400,
            FLT_DENORMAL_OPERAND = 0x00000800,
            FLT_DIVIDE_BY_ZERO = 0x00001000,
            FLT_INEXACT_RESULT = 0x00002000,
            FLT_INVALID_OPERATION = 0x00004000,
            FLT_OVERFLOW = 0x00008000,
            FLT_STACK_CHECK = 0x00010000,
            FLT_UNDERFLOW = 0x00020000,
            INT_DIVIDE_BY_ZERO = 0x00040000,
            INT_OVERFLOW = 0x00080000,
            PRIV_INSTRUCTION = 0x00100000,
            STACK_OVERFLOW = 0x00200000
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets a value that determines which debug events will be monitored.
        /// </summary>
        public EventFilter EventsMonitored { get; set; }

        /// <summary>
        /// Gets or sets a value that determines which debug exceptions will be monitored.
        /// </summary>
        public ExceptionFilter ExceptionsMonitored { get; set; }

        /// <summary>
        /// Gets a value indicating whether the initial breakpoint has been encountered by the debugger.
        /// </summary>
        public bool InitialBreakpointHit { get; private set; }

        /// <summary>
        /// Gets or sets an identifier that is appended to log entriess. This can be used to identify which instance
        /// of a debugger caused an event or exception.
        /// </summary>
        public string InstanceIdentifier { get; set; }

        #endregion

        #region Methods

        #region Events

        /// <summary>
        /// Handles the CREATE_PROCESS_DEBUG_EVENT debug event.
        /// </summary>
        /// <param name="de">The debug event that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnCreateProcessDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventsMonitored.HasFlag(EventFilter.CREATE_PROCESS_DEBUG_EVENT))
            {
                string imageName = Auxiliary.GetFileNameFromHandle(de.CreateProcessInfo.hFile);
                uint pid = de.dwProcessId;
                string message = "Process created: " + imageName + " (PID: " + pid + ")";
                this.monitorLogger.Log(this.AppendInstanceIdentifier(message));
            }

            return base.OnCreateProcessDebugEvent(ref de);
        }

        /// <summary>
        /// Handles the CREATE_THREAD_DEBUG_EVENT debug event.
        /// </summary>
        /// <param name="de">The debug event that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnCreateThreadDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventsMonitored.HasFlag(EventFilter.CREATE_THREAD_DEBUG_EVENT))
            {
                string message = "Thread " + de.dwThreadId + " has been created.";
                this.monitorLogger.Log(this.AppendInstanceIdentifier(message));
            }

            return base.OnCreateThreadDebugEvent(ref de);
        }

        /// <summary>
        /// Handles the EXIT_PROCESS_DEBUG_EVENT debug event.
        /// </summary>
        /// <param name="de">The debug event that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnExitProcessDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventsMonitored.HasFlag(EventFilter.EXIT_PROCESS_DEBUG_EVENT))
            {
                string message =
                    "Process exited with code " + de.ExitProcess.dwExitCode + ". (PID: " + de.dwProcessId + ")";
                this.monitorLogger.Log(this.AppendInstanceIdentifier(message));
            }

            return base.OnExitProcessDebugEvent(ref de);
        }

        /// <summary>
        /// Handles the EXIT_THREAD_DEBUG_EVENT debug event.
        /// </summary>
        /// <param name="de">The debug event that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnExitThreadDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventsMonitored.HasFlag(EventFilter.EXIT_THREAD_DEBUG_EVENT))
            {
                string message = "Thread " + de.dwThreadId + " has exited.";
                this.monitorLogger.Log(this.AppendInstanceIdentifier(message));
            }

            return base.OnExitThreadDebugEvent(ref de);
        }

        /// <summary>
        /// Handles the LOAD_DLL_DEBUG_EVENT debug event.
        /// </summary>
        /// <param name="de">The debug event that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnLoadDllDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventsMonitored.HasFlag(EventFilter.LOAD_DLL_DEBUG_EVENT))
            {
                string dllName = Auxiliary.GetFileNameFromHandle(de.LoadDll.hFile);
                string message = "DLL loaded: " + dllName;
                this.monitorLogger.Log(this.AppendInstanceIdentifier(message));
            }

            return base.OnLoadDllDebugEvent(ref de);
        }

        /// <summary>
        /// Handles the OUTPUT_DEBUG_STRING_EVENT debug event.
        /// </summary>
        /// <param name="de">The debug event that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnOutputDebugStringEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventsMonitored.HasFlag(EventFilter.OUTPUT_DEBUG_STRING_EVENT))
            {
                this.monitorLogger.Log("OnOutputDebugStringEvent called. (" + this.InstanceIdentifier + ")");
            }

            return base.OnOutputDebugStringEvent(ref de);
        }

        /// <summary>
        /// Handles the RIP_EVENT debug event.
        /// </summary>
        /// <param name="de">The debug event that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnRipEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventsMonitored.HasFlag(EventFilter.RIP_EVENT))
            {
                this.monitorLogger.Log("OnRipEvent called. (" + this.InstanceIdentifier + ")");
            }

            return base.OnRipEvent(ref de);
        }

        /// <summary>
        /// Handles the UNLOAD_DLL_DEBUG_EVENT debug event.
        /// </summary>
        /// <param name="de">The debug event that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnUnloadDllDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventsMonitored.HasFlag(EventFilter.UNLOAD_DLL_DEBUG_EVENT))
            {
                // TODO: Fix the name resolution function that is used when logging DLL unload events.
                string dllName = this.GetFileNameFromHModule(de.UnloadDll.lpBaseOfDll);
                string message = "DLL unloaded: " + dllName;
                this.monitorLogger.Log(this.AppendInstanceIdentifier(message));
            }

            return base.OnUnloadDllDebugEvent(ref de);
        }

        #endregion

        #region Exceptions

        /// <summary>
        /// Logs information about an EXCEPTION_ACCESS_VIOLATION debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnAccessViolationDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.ACCESS_VIOLATION))
            {
                IntPtr exceptionAddress = de.Exception.ExceptionRecord.ExceptionAddress;
                string inst = this.DisassembleInstructionAtAddress(exceptionAddress);
                string message =
                    "Access violation at " + this.IntPtrToFormattedAddress(exceptionAddress) + " - " + inst;
                this.monitorLogger.Log(this.AppendInstanceIdentifier(message));
            }

            return base.OnAccessViolationDebugException(ref de);
        }

        /// <summary>
        /// Logs information about an EXCEPTION_ARRAY_BOUNDS_EXCEEDED debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnArrayBoundsExceededDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.ARRAY_BOUNDS_EXCEEDED))
            {
                this.monitorLogger.Log("OnArrayBoundsExceededDebugException called. (" + this.InstanceIdentifier + ")");
            }

            return base.OnArrayBoundsExceededDebugException(ref de);
        }

        /// <summary>
        /// Logs information about an EXCEPTION_BREAKPOINT debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnBreakpointDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (!this.InitialBreakpointHit)
            {
                this.InitialBreakpointHit = true;
                if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.BREAKPOINT))
                {
                    this.monitorLogger.Log("Initial breakpoint hit.");
                }
            }
            else
            {
                if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.BREAKPOINT))
                {
                    this.monitorLogger.Log("Breakpoint hit at: " + de.Exception.ExceptionRecord.ExceptionAddress);
                }
            }

            return base.OnBreakpointDebugException(ref de);
        }

        /// <summary>
        /// Logs information about an EXCEPTION_DATATYPE_MISALIGNMENT debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnDatatypeMisalignmentDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.DATATYPE_MISALIGNMENT))
            {
                this.monitorLogger.Log("OnDatatypeMisalignmentDebugException called. (" + this.InstanceIdentifier + ")");
            }

            return base.OnDatatypeMisalignmentDebugException(ref de);
        }

        /// <summary>
        /// Logs information about an EXCEPTION_FLT_DENORMAL_OPERAND debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnFltDenormalOperandDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.FLT_DENORMAL_OPERAND))
            {
                this.monitorLogger.Log("OnFltDenormalOperandDebugException called. (" + this.InstanceIdentifier + ")");
            }

            return base.OnFltDenormalOperandDebugException(ref de);
        }

        /// <summary>
        /// Logs information about an EXCEPTION_FLT_DIVIDE_BY_ZERO debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnFltDivideByZeroDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.FLT_DIVIDE_BY_ZERO))
            {
                this.monitorLogger.Log("OnFltDivideByZeroDebugException called. (" + this.InstanceIdentifier + ")");
            }

            return base.OnFltDivideByZeroDebugException(ref de);
        }

        /// <summary>
        /// Logs information about an EXCEPTION_FLT_INEXACT_RESULT debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnFltInexactResultDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.FLT_INEXACT_RESULT))
            {
                this.monitorLogger.Log("OnFltInexactResultDebugException called. (" + this.InstanceIdentifier + ")");
            }

            return base.OnFltInexactResultDebugException(ref de);
        }

        /// <summary>
        /// Logs information about an EXCEPTION_FLT_INVALID_OPERATION debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnFltInvalidOperationDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.FLT_INVALID_OPERATION))
            {
                this.monitorLogger.Log("OnFltInvalidOperationDebugException called. (" + this.InstanceIdentifier + ")");
            }

            return base.OnFltInvalidOperationDebugException(ref de);
        }

        /// <summary>
        /// Logs information about an EXCEPTION_FLT_OVERFLOW debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnFltOverflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.FLT_OVERFLOW))
            {
                this.monitorLogger.Log("OnFltOverflowDebugException called. (" + this.InstanceIdentifier + ")");
            }

            return base.OnFltOverflowDebugException(ref de);
        }

        /// <summary>
        /// Logs information about an EXCEPTION_FLT_STACK_CHECK debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnFltStackCheckDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.FLT_STACK_CHECK))
            {
                this.monitorLogger.Log("OnFltStackCheckDebugException called. (" + this.InstanceIdentifier + ")");
            }

            return base.OnFltStackCheckDebugException(ref de);
        }

        /// <summary>
        /// Logs information about an EXCEPTION_FLT_UNDERFLOW debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnFltUnderflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.FLT_UNDERFLOW))
            {
                this.monitorLogger.Log("OnFltUnderflowDebugException called. (" + this.InstanceIdentifier + ")");
            }

            return base.OnFltUnderflowDebugException(ref de);
        }

        /// <summary>
        /// Logs information about an EXCEPTION_GUARD_PAGE debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnGuardPageDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.GUARD_PAGE))
            {
                this.monitorLogger.Log("OnGuardPageDebugException called. (" + this.InstanceIdentifier + ")");
            }

            return base.OnGuardPageDebugException(ref de);
        }

        /// <summary>
        /// Logs information about an EXCEPTION_ILLEGAL_INSTRUCTION debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnIllegalInstructionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.ILLEGAL_INSTRUCTION))
            {
                this.monitorLogger.Log("OnIllegalInstructionDebugException called. (" + this.InstanceIdentifier + ")");
            }

            return base.OnIllegalInstructionDebugException(ref de);
        }

        /// <summary>
        /// Logs information about an EXCEPTION_IN_PAGE_ERROR debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnInPageErrorDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.IN_PAGE_ERROR))
            {
                this.monitorLogger.Log("OnInPageErrorDebugException called. (" + this.InstanceIdentifier + ")");
            }

            return base.OnInPageErrorDebugException(ref de);
        }

        /// <summary>
        /// Logs information about an EXCEPTION_INT_DIVIDE_BY_ZERO debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnIntDivideByZeroDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.INT_DIVIDE_BY_ZERO))
            {
                this.monitorLogger.Log("OnIntDivideByZeroDebugException called. (" + this.InstanceIdentifier + ")");
            }

            return base.OnIntDivideByZeroDebugException(ref de);
        }

        /// <summary>
        /// Logs information about an EXCEPTION_INT_OVERFLOW debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnIntOverflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.INT_OVERFLOW))
            {
                this.monitorLogger.Log("OnIntOverflowDebugException called. (" + this.InstanceIdentifier + ")");
            }

            return base.OnIntOverflowDebugException(ref de);
        }

        /// <summary>
        /// Logs information about an EXCEPTION_INVALID_DISPOSITION debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnInvalidDispositionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.INVALID_DISPOSITION))
            {
                this.monitorLogger.Log("OnInvalidDispositionDebugException called. (" + this.InstanceIdentifier + ")");
            }

            return base.OnInvalidDispositionDebugException(ref de);
        }

        /// <summary>
        /// Logs information about an EXCEPTION_NONCONTINUABLE_EXCEPTION debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnNoncontinuableExceptionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.NONCONTINUABLE_EXCEPTION))
            {
                this.monitorLogger.Log("OnNoncontinuableExceptionDebugException called. (" + this.InstanceIdentifier + ")");
            }

            return base.OnNoncontinuableExceptionDebugException(ref de);
        }

        /// <summary>
        /// Logs information about an EXCEPTION_PRIV_INSTRUCTION debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnPrivInstructionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.PRIV_INSTRUCTION))
            {
                this.monitorLogger.Log("OnPrivInstructionDebugException called. (" + this.InstanceIdentifier + ")");
            }

            return base.OnPrivInstructionDebugException(ref de);
        }

        /// <summary>
        /// Logs information about an EXCEPTION_SINGLE_STEP debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnSingleStepDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.SINGLE_STEP))
            {
                this.monitorLogger.Log("OnSingleStepDebugException called. (" + this.InstanceIdentifier + ")");
            }

            return base.OnSingleStepDebugException(ref de);
        }

        /// <summary>
        /// Logs information about an EXCEPTION_STACK_OVERFLOW debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnStackOverflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.STACK_OVERFLOW))
            {
                this.monitorLogger.Log("OnStackOverflowDebugException called. (" + this.InstanceIdentifier + ")");
            }

            return base.OnStackOverflowDebugException(ref de);
        }

        #endregion

        private string AppendInstanceIdentifier(string message)
        {
            if (!string.IsNullOrEmpty(this.InstanceIdentifier))
            {
                return message + " (" + this.InstanceIdentifier + ")";
            }
            else
            {
                return message;
            }
        }

        private string DisassembleInstructionAtAddress(IntPtr address)
        {
            byte[] instData = new byte[16];
            if (!this.Read(address, instData))
            {
                this.Status.Log(
                    "Unable to read address " + this.IntPtrToFormattedAddress(address),
                    Logger.Level.HIGH);
                return "<instruction disassembly failed>";
            }

            List<string> insts = new List<string>();
            if (this.Is64Bit)
            {
                insts = Distorm.Disassemble(instData, 0, Distorm.DecodeType.Decode64Bits);
            }
            else
            {
                insts = Distorm.Disassemble(instData, 0, Distorm.DecodeType.Decode32Bits);
            }

            if (insts.Count == 0)
            {
                string dataAsHexString =
                    string.Join(" ", instData.Select(x => x.ToString("x").PadLeft(2, '0')).ToArray());
                this.Status.Log("Distorm3cs failed to disassemble the following bytes: " + dataAsHexString);
                return "<instruction disassembly failed>";
            }

            return insts[0];
        }

        #endregion
    }
}
