namespace Nouzuru
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Logger;

    public class DebugMon : Debugger
    {
        #region Fields

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

        public const ExceptionFilter ExploitableExceptions =
            ExceptionFilter.GUARD_PAGE |
            ExceptionFilter.ACCESS_VIOLATION |
            ExceptionFilter.INT_OVERFLOW |
            ExceptionFilter.STACK_OVERFLOW;

        private Logger monitorLogger;

        #endregion

        #region Constructors

        public DebugMon(string filename)
        {
            this.monitorLogger = new Logger(Logger.Type.FILE | Logger.Type.CONSOLE, Logger.Level.NONE, filename);
        }

        #endregion

        #region Enumerations

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

        public EventFilter EventsMonitored { get; set; }

        public ExceptionFilter ExceptionsMonitored { get; set; }

        public bool InitialBreakpointHit { get; private set; }

        #endregion

        #region Methods

        protected override WinApi.DbgCode OnAccessViolationDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.ACCESS_VIOLATION))
            {
                this.monitorLogger.Log("OnAccessViolationDebugException called.");
            }

            return base.OnAccessViolationDebugException(ref de);
        }

        protected override WinApi.DbgCode OnArrayBoundsExceededDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.ARRAY_BOUNDS_EXCEEDED))
            {
                this.monitorLogger.Log("OnArrayBoundsExceededDebugException called.");
            }

            return base.OnArrayBoundsExceededDebugException(ref de);
        }

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

        protected override WinApi.DbgCode OnCreateProcessDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventsMonitored.HasFlag(EventFilter.CREATE_PROCESS_DEBUG_EVENT))
            {
                string imageName = Auxiliary.GetFileNameFromHandle(de.CreateProcessInfo.hFile);
                uint pid = WinApi.GetProcessId(de.CreateProcessInfo.hProcess);
                this.monitorLogger.Log("Process created: " + imageName + " (PID: " + pid + ")");
            }

            return base.OnCreateProcessDebugEvent(ref de);
        }

        protected override WinApi.DbgCode OnCreateThreadDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventsMonitored.HasFlag(EventFilter.CREATE_THREAD_DEBUG_EVENT))
            {
                this.monitorLogger.Log("OnCreateThreadDebugEvent called.");
            }

            return base.OnCreateThreadDebugEvent(ref de);
        }

        protected override WinApi.DbgCode OnDatatypeMisalignmentDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.DATATYPE_MISALIGNMENT))
            {
                this.monitorLogger.Log("OnDatatypeMisalignmentDebugException called.");
            }

            return base.OnDatatypeMisalignmentDebugException(ref de);
        }

        protected override WinApi.DbgCode OnExitProcessDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventsMonitored.HasFlag(EventFilter.EXIT_PROCESS_DEBUG_EVENT))
            {
                this.monitorLogger.Log("OnExitProcessDebugEvent called.");
            }

            return base.OnExitProcessDebugEvent(ref de);
        }

        protected override WinApi.DbgCode OnExitThreadDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventsMonitored.HasFlag(EventFilter.EXIT_THREAD_DEBUG_EVENT))
            {
                this.monitorLogger.Log("OnExitThreadDebugEvent called.");
            }

            return base.OnExitThreadDebugEvent(ref de);
        }

        protected override WinApi.DbgCode OnFltDenormalOperandDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.FLT_DENORMAL_OPERAND))
            {
                this.monitorLogger.Log("OnFltDenormalOperandDebugException called.");
            }

            return base.OnFltDenormalOperandDebugException(ref de);
        }

        protected override WinApi.DbgCode OnFltDivideByZeroDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.FLT_DIVIDE_BY_ZERO))
            {
                this.monitorLogger.Log("OnFltDivideByZeroDebugException called.");
            }

            return base.OnFltDivideByZeroDebugException(ref de);
        }

        protected override WinApi.DbgCode OnFltInexactResultDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.FLT_INEXACT_RESULT))
            {
                this.monitorLogger.Log("OnFltInexactResultDebugException called.");
            }

            return base.OnFltInexactResultDebugException(ref de);
        }

        protected override WinApi.DbgCode OnFltInvalidOperationDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.FLT_INVALID_OPERATION))
            {
                this.monitorLogger.Log("OnFltInvalidOperationDebugException called.");
            }

            return base.OnFltInvalidOperationDebugException(ref de);
        }

        protected override WinApi.DbgCode OnFltOverflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.FLT_OVERFLOW))
            {
                this.monitorLogger.Log("OnFltOverflowDebugException called.");
            }

            return base.OnFltOverflowDebugException(ref de);
        }

        protected override WinApi.DbgCode OnFltStackCheckDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.FLT_STACK_CHECK))
            {
                this.monitorLogger.Log("OnFltStackCheckDebugException called.");
            }

            return base.OnFltStackCheckDebugException(ref de);
        }

        protected override WinApi.DbgCode OnFltUnderflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.FLT_UNDERFLOW))
            {
                this.monitorLogger.Log("OnFltUnderflowDebugException called.");
            }

            return base.OnFltUnderflowDebugException(ref de);
        }

        protected override WinApi.DbgCode OnGuardPageDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.GUARD_PAGE))
            {
                this.monitorLogger.Log("OnGuardPageDebugException called.");
            }

            return base.OnGuardPageDebugException(ref de);
        }

        protected override WinApi.DbgCode OnIllegalInstructionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.ILLEGAL_INSTRUCTION))
            {
                this.monitorLogger.Log("OnIllegalInstructionDebugException called.");
            }

            return base.OnIllegalInstructionDebugException(ref de);
        }

        protected override WinApi.DbgCode OnInPageErrorDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.IN_PAGE_ERROR))
            {
                this.monitorLogger.Log("OnInPageErrorDebugException called.");
            }

            return base.OnInPageErrorDebugException(ref de);
        }

        protected override WinApi.DbgCode OnIntDivideByZeroDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.INT_DIVIDE_BY_ZERO))
            {
                this.monitorLogger.Log("OnIntDivideByZeroDebugException called.");
            }

            return base.OnIntDivideByZeroDebugException(ref de);
        }

        protected override WinApi.DbgCode OnIntOverflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.INT_OVERFLOW))
            {
                this.monitorLogger.Log("OnIntOverflowDebugException called.");
            }

            return base.OnIntOverflowDebugException(ref de);
        }

        protected override WinApi.DbgCode OnInvalidDispositionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.INVALID_DISPOSITION))
            {
                this.monitorLogger.Log("OnInvalidDispositionDebugException called.");
            }

            return base.OnInvalidDispositionDebugException(ref de);
        }

        protected override WinApi.DbgCode OnLoadDllDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventsMonitored.HasFlag(EventFilter.LOAD_DLL_DEBUG_EVENT))
            {
                string dllName = Auxiliary.GetFileNameFromHandle(de.LoadDll.hFile);
                this.monitorLogger.Log("DLL loaded: " + dllName);
            }

            return base.OnLoadDllDebugEvent(ref de);
        }

        protected override WinApi.DbgCode OnNoncontinuableExceptionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.NONCONTINUABLE_EXCEPTION))
            {
                this.monitorLogger.Log("OnNoncontinuableExceptionDebugException called.");
            }

            return base.OnNoncontinuableExceptionDebugException(ref de);
        }

        protected override WinApi.DbgCode OnOutputDebugStringEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventsMonitored.HasFlag(EventFilter.OUTPUT_DEBUG_STRING_EVENT))
            {
                this.monitorLogger.Log("OnOutputDebugStringEvent called.");
            }

            return base.OnOutputDebugStringEvent(ref de);
        }

        protected override WinApi.DbgCode OnPrivInstructionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.PRIV_INSTRUCTION))
            {
                this.monitorLogger.Log("OnPrivInstructionDebugException called.");
            }

            return base.OnPrivInstructionDebugException(ref de);
        }

        protected override WinApi.DbgCode OnRipEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventsMonitored.HasFlag(EventFilter.RIP_EVENT))
            {
                this.monitorLogger.Log("OnRipEvent called.");
            }

            return base.OnRipEvent(ref de);
        }

        protected override WinApi.DbgCode OnSingleStepDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.SINGLE_STEP))
            {
                this.monitorLogger.Log("OnSingleStepDebugException called.");
            }

            return base.OnSingleStepDebugException(ref de);
        }

        protected override WinApi.DbgCode OnStackOverflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionsMonitored.HasFlag(ExceptionFilter.STACK_OVERFLOW))
            {
                this.monitorLogger.Log("OnStackOverflowDebugException called.");
            }

            return base.OnStackOverflowDebugException(ref de);
        }

        protected override WinApi.DbgCode OnUnloadDllDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventsMonitored.HasFlag(EventFilter.UNLOAD_DLL_DEBUG_EVENT))
            {
                string dllName = Auxiliary.GetFileNameFromHModule(de.UnloadDll.lpBaseOfDll);
                this.monitorLogger.Log("DLL unloaded: " + dllName);
            }

            return base.OnUnloadDllDebugEvent(ref de);
        }

        #endregion
    }
}
