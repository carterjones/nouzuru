namespace Nouzuru
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using Bunseki;
    using Logger;

    /// <summary>
    /// A class that logs each possible debug event and debug exception, attempting to provide detailed data about the
    /// most useful and relevant information for each handled event and exception.
    /// </summary>
    public class DebugMon : Debugger
    {
        #region Fields

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
        public DebugMon(string filename = "debugmon.log")
        {
            this.monitorLogger = new Logger(Logger.Type.FILE | Logger.Type.CONSOLE, Logger.Level.NONE, filename);
        }

        #endregion

        #region Enumerations

#pragma warning disable 1591
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
            RIP_EVENT = 0x0100,

            /// <summary>
            /// All is not an official MSDN event type, but is a shorthand for all debug events.
            /// </summary>
            All =
                EXCEPTION_DEBUG_EVENT | CREATE_THREAD_DEBUG_EVENT | CREATE_PROCESS_DEBUG_EVENT |
                EXIT_THREAD_DEBUG_EVENT | EXIT_PROCESS_DEBUG_EVENT | LOAD_DLL_DEBUG_EVENT | UNLOAD_DLL_DEBUG_EVENT |
                OUTPUT_DEBUG_STRING_EVENT | RIP_EVENT,
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
            STACK_OVERFLOW = 0x00200000,

            /// <summary>
            /// All is not an official MSDN exception type, but is a shorthand for all debug exceptions.
            /// </summary>
            All =
                GUARD_PAGE | DATATYPE_MISALIGNMENT | BREAKPOINT | SINGLE_STEP | INVALID_HANDLE | ACCESS_VIOLATION |
                IN_PAGE_ERROR | ILLEGAL_INSTRUCTION | NONCONTINUABLE_EXCEPTION | INVALID_DISPOSITION |
                ARRAY_BOUNDS_EXCEEDED | FLT_DENORMAL_OPERAND | FLT_DIVIDE_BY_ZERO | FLT_INEXACT_RESULT |
                FLT_INVALID_OPERATION | FLT_OVERFLOW | FLT_STACK_CHECK | FLT_UNDERFLOW | INT_DIVIDE_BY_ZERO |
                INT_OVERFLOW | PRIV_INSTRUCTION | STACK_OVERFLOW,

            /// <summary>
            /// Exploitable is not an official MSDN exception type, but is a shorthand for all debug exceptions that
            /// could be exploitable or could indicate an exploitable condition exists.
            /// </summary>
            Exploitable = GUARD_PAGE | ACCESS_VIOLATION | INT_OVERFLOW | STACK_OVERFLOW,
        }

#pragma warning restore 1591

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
        /// Gets or sets an identifier that is appended to log entriess. This can be used to identify which instance
        /// of a debugger caused an event or exception.
        /// </summary>
        public string InstanceIdentifier { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Wait until the initial breakpoint has been hit.
        /// </summary>
        public void WaitUntilInitialBreakpointIsHit()
        {
            while (!this.InitialBreakpointHit)
            {
                Thread.Sleep(1);
            }

            return;
        }

        public string DisassembleCurrentInstruction()
        {
            this.VerifyDebuggingIsPaused();
            return this.DisassembleInstructionAtAddress(this.CurrentAddress);
        }

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
                bool isAscii = de.DebugString.fUnicode == 0;
                ushort size = de.DebugString.nDebugStringLength;
                byte[] data = new byte[size];
                if (!this.Read(de.DebugString.lpDebugStringData, data))
                {
                    return base.OnOutputDebugStringEvent(ref de);
                }

                string debugString = string.Empty;
                if (isAscii)
                {
                    debugString = Encoding.ASCII.GetString(data);
                }
                else
                {
                    debugString = Encoding.Unicode.GetString(data);
                }

                debugString = debugString.TrimEnd(new char[] { '\r', '\n', '\0' });

                this.monitorLogger.Log(
                    this.AppendInstanceIdentifier("Debug string received: \"" + debugString + "\""));
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
                StringBuilder msg = new StringBuilder();
                msg.Append("A RIP event occured");
                if (Enum.IsDefined(typeof(WinApi.RipInfoTypes), (WinApi.RipInfoTypes)de.RipInfo.dwType))
                {
                    msg.Append(" (" + ((WinApi.RipInfoTypes)de.RipInfo.dwType).ToString() + ")");
                }

                msg.Append(". Error code: " + de.RipInfo.dwError);
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
                string message = this.GetExceptionAddressData(de.Exception.ExceptionRecord.ExceptionAddress);
                this.monitorLogger.Log(this.AppendInstanceIdentifier(message));
                this.LogRegisters(ref de);
                this.LogSurroundingInstructions(de.Exception.ExceptionRecord.ExceptionAddress, 5, 2);
                this.LogExceptionRecord(de.Exception.ExceptionRecord);
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
                this.LogGenericException(ref de);
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

                return WinApi.DbgCode.CONTINUE;
            }
            else
            {
                if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.BREAKPOINT))
                {
                    this.monitorLogger.Log(
                        "Breakpoint hit at: " +
                        this.IntPtrToFormattedAddress(de.Exception.ExceptionRecord.ExceptionAddress));
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
                this.LogGenericException(ref de);
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
                this.LogGenericException(ref de);
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
                this.LogGenericException(ref de);
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
                this.LogGenericException(ref de);
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
                this.LogGenericException(ref de);
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
                this.LogGenericException(ref de);
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
                this.LogGenericException(ref de);
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
                this.LogGenericException(ref de);
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
                this.GetExceptionAddressData(de.Exception.ExceptionRecord.ExceptionAddress);
                this.LogGenericException(ref de);
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
                this.LogGenericException(ref de);
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
                this.LogGenericException(ref de);
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
                this.LogGenericException(ref de);
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
                this.GetExceptionAddressData(de.Exception.ExceptionRecord.ExceptionAddress);
                this.LogGenericException(ref de);
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
                this.LogGenericException(ref de);
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
                this.LogGenericException(ref de);
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
                this.LogGenericException(ref de);
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
                this.LogGenericException(ref de);
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
                this.GetExceptionAddressData(de.Exception.ExceptionRecord.ExceptionAddress);
                this.LogGenericException(ref de);
            }

            return base.OnStackOverflowDebugException(ref de);
        }

        #endregion

        /// <summary>
        /// Takes a message and adds the instance identifier, if one exists.
        /// </summary>
        /// <param name="message">The message on which an instance identifier may be applied.</param>
        /// <returns>
        /// Returns the message with the appended instance identifier, if an identifier has been defiened.
        /// </returns>
        protected string AppendInstanceIdentifier(string message)
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

        /// <summary>
        /// Disassembles the instruction that occurs at the specified address.
        /// </summary>
        /// <param name="address">The address where the instruction to be disassembled resides.</param>
        /// <returns>Returns the disassembled instruction.</returns>
        protected string DisassembleInstructionAtAddress(IntPtr address)
        {
            byte[] instData = new byte[16];
            if (!this.Read(address, instData))
            {
                this.Status.Log(
                    "Unable to read address " + this.IntPtrToFormattedAddress(address),
                    Logger.Level.HIGH);
                return "<instruction disassembly failed>";
            }

            List<Instruction> insts = this.d.DisassembleInstructions(instData, IntPtr.Zero).ToList();

            if (insts.Count == 0)
            {
                string dataAsHexString =
                    string.Join(" ", instData.Select(x => x.ToString("x").PadLeft(2, '0')).ToArray());
                this.Status.Log("Distorm3cs failed to disassemble the following bytes: " + dataAsHexString);
                return "<instruction disassembly failed>";
            }

            return insts[0].ToString();
        }

        /// <summary>
        /// Attempt to identify the exception code and return a string representation of that code.
        /// </summary>
        /// <param name="exceptionCode">The exception code as an unsigned integer.</param>
        /// <returns>
        /// On success, returns the exception code as a string. On failure, returns "UNKNOWN_EXCEPTION".
        /// </returns>
        protected string ExceptionCodeToString(uint exceptionCode)
        {
            Array values = Enum.GetValues(typeof(WinApi.ExceptionType));
            foreach (var value in values)
            {
                if ((uint)value == exceptionCode)
                {
                    return ((WinApi.ExceptionType)exceptionCode).ToString();
                }
            }

            return "UNKNOWN_EXCEPTION";
        }

        /// <summary>
        /// Attempts to identify the exception code and return a properly capitalized string representation of that
        /// code.
        /// </summary>
        /// <param name="exceptionCode">The exception code as an unsigned integer.</param>
        /// <returns>
        /// On success, returns the exception code as a string. On failure, returns "Unknown Exception".
        /// </returns>
        protected string ExceptionCodeToPrettyString(uint exceptionCode)
        {
            string code = this.ExceptionCodeToString(exceptionCode).ToLower();
            string[] parts =
                code.Split('_').Select(x => x[0].ToString().ToUpper() + new string(x.Skip(1).ToArray())).ToArray();
            return string.Join(" ", parts);
        }

        /// <summary>
        /// Get information that relates to the instruction where an exception has occurred.
        /// </summary>
        /// <param name="address">The address of the execption.</param>
        /// <returns>Returns the information as a single line string.</returns>
        protected string GetExceptionAddressData(IntPtr address)
        {
            string inst = this.DisassembleInstructionAtAddress(address);
            string message =
                "An access violation occurred at " +
                this.IntPtrToFormattedAddress(address) + " - " + inst;
            return message;
        }

        /// <summary>
        /// Get both the disassembled and the decomposed instructions that surround the specified address.
        /// </summary>
        /// <param name="address">The address in the middle of the surrounding instructions.</param>
        /// <param name="numBefore">The number of instructions before the middle instruction.</param>
        /// <param name="numAfter">The number of instructions after the middle instruction.</param>
        /// <returns>
        /// Returns the disassembled and decomposed instructions that surround the specified address.
        /// </returns>
        protected List<Instruction> GetSurroundingInsts(IntPtr address, uint numBefore, uint numAfter)
        {
            long firstAddress = address.ToInt64() - (numBefore * 15);
            long readSize = (numBefore + 1 + numAfter) * 15;
            byte[] data = new byte[readSize];
            if (!this.Read(new IntPtr(firstAddress), data))
            {
                this.monitorLogger.Log(
                    "Unable to read the code surrounding debug exception address.", Logger.Level.MEDIUM);
                return new List<Instruction>();
            }

            List<Instruction> insts;
            if (this.Is64Bit)
            {
                this.d.TargetArchitecture = Disassembler.Architecture.x86_64;
            }
            else
            {
                this.d.TargetArchitecture = Disassembler.Architecture.x86_32;
            }
            insts = this.d.DisassembleInstructions(data, (IntPtr)firstAddress).ToList();

            if (insts.Count == 0)
            {
                this.monitorLogger.Log(
                    "Unable to disassemble the code surrounding debug exception address.", Logger.Level.MEDIUM);
                return new List<Instruction>();
            }

            int instIndex = -1;
            for (int i = 0; i < insts.Count; ++i)
            {
                if (insts[i].Address == address)
                {
                    instIndex = i;
                    break;
                }
            }

            if (instIndex == -1)
            {
                this.monitorLogger.Log(
                    "Unable to detect valid instruction at " + this.IntPtrToFormattedAddress(address),
                    Logger.Level.MEDIUM);
                return new List<Instruction>();
            }

            uint firstIndex = (uint)instIndex - numBefore;
            List<Instruction> surroundingInsts =
                insts.Skip((int)firstIndex).Take((int)(numBefore + 1 + numAfter)).ToList();

            return surroundingInsts;
        }

        /// <summary>
        /// Log each piece of information available from an exception record.
        /// </summary>
        /// <param name="er">The exception record to be logged.</param>
#if WIN64
        protected void LogExceptionRecord(WinApi.EXCEPTION_RECORD64 er)
#else
        protected void LogExceptionRecord(WinApi.EXCEPTION_RECORD32 er)
#endif
        {
            this.monitorLogger.Log("ExceptionAddress: " + this.IntPtrToFormattedAddress(er.ExceptionAddress));
            string code =
                er.ExceptionCode.ToString("x").PadLeft(8, '0') +
                " (" + this.ExceptionCodeToString(er.ExceptionCode) + ")";
            this.monitorLogger.Log("ExceptionCode:    0x" + code);
            this.monitorLogger.Log("ExceptionFlags:   0x" + er.ExceptionFlags.ToString("x").PadLeft(8, '0'));
            this.monitorLogger.Log("ExceptionRecord:  " + this.IntPtrToFormattedAddress(er.ExceptionRecord));
            this.monitorLogger.Log("NumberParameters: " + er.NumberParameters);
            this.monitorLogger.Log("ExceptionInformation:");
            for (int i = 0; i < er.ExceptionInformation.Length; ++i)
            {
                string counter = "  " + i.ToString().PadLeft(2, '0');
                string info = " - 0x" + er.ExceptionInformation[i].ToString("x").PadLeft(8, '0');
                this.monitorLogger.Log(counter + info);
            }
        }

        /// <summary>
        /// Logs generic information about an exception.
        /// </summary>
        /// <param name="de">The debug event that contains the exception that was thrown.</param>
        protected void LogGenericException(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log(this.AppendInstanceIdentifier(
                "An exception occurred: " +
                this.ExceptionCodeToPrettyString(de.Exception.ExceptionRecord.ExceptionCode)));
            this.LogRegisters(ref de);
            this.LogSurroundingInstructions(de.Exception.ExceptionRecord.ExceptionAddress, 5, 2);
            this.LogExceptionRecord(de.Exception.ExceptionRecord);
        }

        /// <summary>
        /// Logs the registers at the time the debug event was caught.
        /// </summary>
        /// <param name="de">The debug event that was caught.</param>
        protected void LogRegisters(ref WinApi.DEBUG_EVENT de)
        {
            IntPtr threadHandle;
            WinApi.CONTEXT cx;
            this.BeginEditThread(de.dwThreadId, out threadHandle, out cx);
            this.LogRegisters(ref cx);
            this.EndEditThread(de.dwThreadId, ref threadHandle, ref cx);
        }

        /// <summary>
        /// Logs the registers at the time the debug event was caught.
        /// </summary>
        /// <param name="cx">The context of the paused thread that will have its registers logged.</param>
        protected void LogRegisters(ref WinApi.CONTEXT cx)
        {
            int paddingSize = Marshal.SizeOf(IntPtr.Zero) * 2;
            string registerPrefix = string.Empty;
            if (paddingSize == 8)
            {
                registerPrefix = "e";
            }
            else
            {
                registerPrefix = "r";
            }

            this.monitorLogger.Log(
                registerPrefix + "ax:" + cx._ax.ToString("X").PadLeft(paddingSize, '0') + " " +
                registerPrefix + "bx:" + cx._bx.ToString("X").PadLeft(paddingSize, '0') + " " +
                registerPrefix + "cx:" + cx._cx.ToString("X").PadLeft(paddingSize, '0') + " " +
                registerPrefix + "dx:" + cx._dx.ToString("X").PadLeft(paddingSize, '0') + " " +
                registerPrefix + "ip:" + cx._ip.ToString("X").PadLeft(paddingSize, '0') + " " +
                registerPrefix + "bp:" + cx._bp.ToString("X").PadLeft(paddingSize, '0'));
            this.monitorLogger.Log(
                "dr0:" + cx.Dr0.ToString("X").PadLeft(paddingSize, '0') +
                " dr1:" + cx.Dr1.ToString("X").PadLeft(paddingSize, '0') +
                " dr2:" + cx.Dr2.ToString("X").PadLeft(paddingSize, '0') +
                " dr3:" + cx.Dr3.ToString("X").PadLeft(paddingSize, '0') +
                " dr6:" + cx.Dr6.ToString("X").PadLeft(paddingSize, '0') +
                " dr7:" + cx.Dr7.ToString("X").PadLeft(paddingSize, '0'));
        }

        /// <summary>
        /// Log both the disassembled instructions that surround the specified address.
        /// </summary>
        /// <param name="address">The address in the middle of the surrounding instructions.</param>
        /// <param name="numBefore">The number of instructions before the middle instruction.</param>
        /// <param name="numAfter">The number of instructions after the middle instruction.</param>
        /// <returns>Returns true if the logging process was successful.</returns>
        protected bool LogSurroundingInstructions(IntPtr address, uint numBefore, uint numAfter)
        {
            List<Instruction> surroundingInsts = this.GetSurroundingInsts(address, numBefore, numAfter);

            if (surroundingInsts.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < surroundingInsts.Count; ++i)
            {
                string currentAddress =
                    this.IntPtrToFormattedAddress(new IntPtr((long)surroundingInsts[i].Address));
                string spacer = string.Empty;
                if ((ulong)surroundingInsts[i].Address == (ulong)address)
                {
                    spacer = "> ";
                }
                else
                {
                    spacer = "  ";
                }

                this.monitorLogger.Log(spacer + currentAddress + " - " + surroundingInsts[i]);
            }

            return true;
        }

        #endregion
    }
}
