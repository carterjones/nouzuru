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

        public const DebugEventTypeFilter AllEvents =
            DebugEventTypeFilter.EXCEPTION_DEBUG_EVENT |
            DebugEventTypeFilter.CREATE_THREAD_DEBUG_EVENT |
            DebugEventTypeFilter.CREATE_PROCESS_DEBUG_EVENT |
            DebugEventTypeFilter.EXIT_THREAD_DEBUG_EVENT |
            DebugEventTypeFilter.EXIT_PROCESS_DEBUG_EVENT |
            DebugEventTypeFilter.LOAD_DLL_DEBUG_EVENT |
            DebugEventTypeFilter.UNLOAD_DLL_DEBUG_EVENT |
            DebugEventTypeFilter.OUTPUT_DEBUG_STRING_EVENT |
            DebugEventTypeFilter.RIP_EVENT;

        public const DebugExceptionTypeFilter AllExceptions =
            DebugExceptionTypeFilter.GUARD_PAGE |
            DebugExceptionTypeFilter.DATATYPE_MISALIGNMENT |
            DebugExceptionTypeFilter.BREAKPOINT |
            DebugExceptionTypeFilter.SINGLE_STEP |
            DebugExceptionTypeFilter.INVALID_HANDLE |
            DebugExceptionTypeFilter.ACCESS_VIOLATION |
            DebugExceptionTypeFilter.IN_PAGE_ERROR |
            DebugExceptionTypeFilter.ILLEGAL_INSTRUCTION |
            DebugExceptionTypeFilter.NONCONTINUABLE_EXCEPTION |
            DebugExceptionTypeFilter.INVALID_DISPOSITION |
            DebugExceptionTypeFilter.ARRAY_BOUNDS_EXCEEDED |
            DebugExceptionTypeFilter.FLT_DENORMAL_OPERAND |
            DebugExceptionTypeFilter.FLT_DIVIDE_BY_ZERO |
            DebugExceptionTypeFilter.FLT_INEXACT_RESULT |
            DebugExceptionTypeFilter.FLT_INVALID_OPERATION |
            DebugExceptionTypeFilter.FLT_OVERFLOW |
            DebugExceptionTypeFilter.FLT_STACK_CHECK |
            DebugExceptionTypeFilter.FLT_UNDERFLOW |
            DebugExceptionTypeFilter.INT_DIVIDE_BY_ZERO |
            DebugExceptionTypeFilter.INT_OVERFLOW |
            DebugExceptionTypeFilter.PRIV_INSTRUCTION |
            DebugExceptionTypeFilter.STACK_OVERFLOW;

        public const DebugExceptionTypeFilter ExploitableExceptions =
            DebugExceptionTypeFilter.GUARD_PAGE |
            DebugExceptionTypeFilter.ACCESS_VIOLATION |
            DebugExceptionTypeFilter.INT_OVERFLOW |
            DebugExceptionTypeFilter.STACK_OVERFLOW;

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
        public enum DebugEventTypeFilter : uint
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
        public enum DebugExceptionTypeFilter : ulong
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

        public DebugEventTypeFilter EventFilter { get; set; }

        public DebugExceptionTypeFilter ExceptionFilter { get; set; }

        public bool InitialBreakpointHit { get; private set; }

        #endregion

        #region Methods

        protected override WinApi.DbgCode OnAccessViolationDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.ACCESS_VIOLATION))
            {
                this.monitorLogger.Log("OnAccessViolationDebugException called.");
            }

            return base.OnAccessViolationDebugException(ref de);
        }

        protected override WinApi.DbgCode OnArrayBoundsExceededDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.ARRAY_BOUNDS_EXCEEDED))
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
                if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.BREAKPOINT))
                {
                    this.monitorLogger.Log("Initial breakpoint hit.");
                }
            }
            else
            {
                if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.BREAKPOINT))
                {
                    this.monitorLogger.Log("Breakpoint hit at: " + de.Exception.ExceptionRecord.ExceptionAddress);
                }
            }

            return base.OnBreakpointDebugException(ref de);
        }

        protected override WinApi.DbgCode OnCreateProcessDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventFilter.HasFlag(DebugEventTypeFilter.CREATE_PROCESS_DEBUG_EVENT))
            {
                string imageName = Auxiliary.GetFileNameFromHandle(de.CreateProcessInfo.hFile);
                uint pid = WinApi.GetProcessId(de.CreateProcessInfo.hProcess);
                this.monitorLogger.Log("Process created: " + imageName + " (PID: " + pid + ")");
            }

            return base.OnCreateProcessDebugEvent(ref de);
        }

        protected override WinApi.DbgCode OnCreateThreadDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventFilter.HasFlag(DebugEventTypeFilter.CREATE_THREAD_DEBUG_EVENT))
            {
                this.monitorLogger.Log("OnCreateThreadDebugEvent called.");
            }

            return base.OnCreateThreadDebugEvent(ref de);
        }

        protected override WinApi.DbgCode OnDatatypeMisalignmentDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.DATATYPE_MISALIGNMENT))
            {
                this.monitorLogger.Log("OnDatatypeMisalignmentDebugException called.");
            }

            return base.OnDatatypeMisalignmentDebugException(ref de);
        }

        protected override WinApi.DbgCode OnExitProcessDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventFilter.HasFlag(DebugEventTypeFilter.EXIT_PROCESS_DEBUG_EVENT))
            {
                this.monitorLogger.Log("OnExitProcessDebugEvent called.");
            }

            return base.OnExitProcessDebugEvent(ref de);
        }

        protected override WinApi.DbgCode OnExitThreadDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventFilter.HasFlag(DebugEventTypeFilter.EXIT_THREAD_DEBUG_EVENT))
            {
                this.monitorLogger.Log("OnExitThreadDebugEvent called.");
            }

            return base.OnExitThreadDebugEvent(ref de);
        }

        protected override WinApi.DbgCode OnFltDenormalOperandDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.FLT_DENORMAL_OPERAND))
            {
                this.monitorLogger.Log("OnFltDenormalOperandDebugException called.");
            }

            return base.OnFltDenormalOperandDebugException(ref de);
        }

        protected override WinApi.DbgCode OnFltDivideByZeroDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.FLT_DIVIDE_BY_ZERO))
            {
                this.monitorLogger.Log("OnFltDivideByZeroDebugException called.");
            }

            return base.OnFltDivideByZeroDebugException(ref de);
        }

        protected override WinApi.DbgCode OnFltInexactResultDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.FLT_INEXACT_RESULT))
            {
                this.monitorLogger.Log("OnFltInexactResultDebugException called.");
            }

            return base.OnFltInexactResultDebugException(ref de);
        }

        protected override WinApi.DbgCode OnFltInvalidOperationDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.FLT_INVALID_OPERATION))
            {
                this.monitorLogger.Log("OnFltInvalidOperationDebugException called.");
            }

            return base.OnFltInvalidOperationDebugException(ref de);
        }

        protected override WinApi.DbgCode OnFltOverflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.FLT_OVERFLOW))
            {
                this.monitorLogger.Log("OnFltOverflowDebugException called.");
            }

            return base.OnFltOverflowDebugException(ref de);
        }

        protected override WinApi.DbgCode OnFltStackCheckDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.FLT_STACK_CHECK))
            {
                this.monitorLogger.Log("OnFltStackCheckDebugException called.");
            }

            return base.OnFltStackCheckDebugException(ref de);
        }

        protected override WinApi.DbgCode OnFltUnderflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.FLT_UNDERFLOW))
            {
                this.monitorLogger.Log("OnFltUnderflowDebugException called.");
            }

            return base.OnFltUnderflowDebugException(ref de);
        }

        protected override WinApi.DbgCode OnGuardPageDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.GUARD_PAGE))
            {
                this.monitorLogger.Log("OnGuardPageDebugException called.");
            }

            return base.OnGuardPageDebugException(ref de);
        }

        protected override WinApi.DbgCode OnIllegalInstructionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.ILLEGAL_INSTRUCTION))
            {
                this.monitorLogger.Log("OnIllegalInstructionDebugException called.");
            }

            return base.OnIllegalInstructionDebugException(ref de);
        }

        protected override WinApi.DbgCode OnInPageErrorDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.IN_PAGE_ERROR))
            {
                this.monitorLogger.Log("OnInPageErrorDebugException called.");
            }

            return base.OnInPageErrorDebugException(ref de);
        }

        protected override WinApi.DbgCode OnIntDivideByZeroDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.INT_DIVIDE_BY_ZERO))
            {
                this.monitorLogger.Log("OnIntDivideByZeroDebugException called.");
            }

            return base.OnIntDivideByZeroDebugException(ref de);
        }

        protected override WinApi.DbgCode OnIntOverflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.INT_OVERFLOW))
            {
                this.monitorLogger.Log("OnIntOverflowDebugException called.");
            }

            return base.OnIntOverflowDebugException(ref de);
        }

        protected override WinApi.DbgCode OnInvalidDispositionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.INVALID_DISPOSITION))
            {
                this.monitorLogger.Log("OnInvalidDispositionDebugException called.");
            }

            return base.OnInvalidDispositionDebugException(ref de);
        }

        protected override WinApi.DbgCode OnLoadDllDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventFilter.HasFlag(DebugEventTypeFilter.LOAD_DLL_DEBUG_EVENT))
            {
                string dllName = Auxiliary.GetFileNameFromHandle(de.LoadDll.hFile);
                this.monitorLogger.Log("DLL loaded: " + dllName);
            }

            return base.OnLoadDllDebugEvent(ref de);
        }

        protected override WinApi.DbgCode OnNoncontinuableExceptionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.NONCONTINUABLE_EXCEPTION))
            {
                this.monitorLogger.Log("OnNoncontinuableExceptionDebugException called.");
            }

            return base.OnNoncontinuableExceptionDebugException(ref de);
        }

        protected override WinApi.DbgCode OnOutputDebugStringEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventFilter.HasFlag(DebugEventTypeFilter.OUTPUT_DEBUG_STRING_EVENT))
            {
                this.monitorLogger.Log("OnOutputDebugStringEvent called.");
            }

            return base.OnOutputDebugStringEvent(ref de);
        }

        protected override WinApi.DbgCode OnPrivInstructionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.PRIV_INSTRUCTION))
            {
                this.monitorLogger.Log("OnPrivInstructionDebugException called.");
            }

            return base.OnPrivInstructionDebugException(ref de);
        }

        protected override WinApi.DbgCode OnRipEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventFilter.HasFlag(DebugEventTypeFilter.RIP_EVENT))
            {
                this.monitorLogger.Log("OnRipEvent called.");
            }

            return base.OnRipEvent(ref de);
        }

        protected override WinApi.DbgCode OnSingleStepDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.SINGLE_STEP))
            {
                this.monitorLogger.Log("OnSingleStepDebugException called.");
            }

            return base.OnSingleStepDebugException(ref de);
        }

        protected override WinApi.DbgCode OnStackOverflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.ExceptionFilter.HasFlag(DebugExceptionTypeFilter.STACK_OVERFLOW))
            {
                this.monitorLogger.Log("OnStackOverflowDebugException called.");
            }

            return base.OnStackOverflowDebugException(ref de);
        }

        protected override WinApi.DbgCode OnUnloadDllDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            if (this.EventFilter.HasFlag(DebugEventTypeFilter.UNLOAD_DLL_DEBUG_EVENT))
            {
                string dllName = Auxiliary.GetFileNameFromHModule(de.UnloadDll.lpBaseOfDll);
                this.monitorLogger.Log("DLL unloaded: " + dllName);
            }

            return base.OnUnloadDllDebugEvent(ref de);
        }

        #endregion
    }
}
