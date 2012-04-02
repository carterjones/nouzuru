namespace Nouzuru
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Logger;

    public class DebugEventMonitor : Debugger
    {
        #region Fields

        private Logger monitorLogger;

        #endregion

        #region Constructors

        public DebugEventMonitor(string filename)
        {
            this.monitorLogger = new Logger(Logger.Type.FILE | Logger.Type.CONSOLE, Logger.Level.NONE, filename);
        }

        #endregion

        #region Properties

        public bool InitialBreakpointHit { get; private set; }

        #endregion

        #region Methods

        protected override WinApi.DbgCode OnAccessViolationDebugException(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnAccessViolationDebugException called.");
            return base.OnAccessViolationDebugException(ref de);
        }

        protected override WinApi.DbgCode OnArrayBoundsExceededDebugException(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnArrayBoundsExceededDebugException called.");
            return base.OnArrayBoundsExceededDebugException(ref de);
        }

        protected override WinApi.DbgCode OnBreakpointDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (!this.InitialBreakpointHit)
            {
                this.InitialBreakpointHit = true;
                this.monitorLogger.Log("Initial breakpoint hit.");
            }
            else
            {
                this.monitorLogger.Log("Breakpoint hit at: " + de.Exception.ExceptionRecord.ExceptionAddress);
            }

            return base.OnBreakpointDebugException(ref de);
        }

        protected override WinApi.DbgCode OnCreateProcessDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            string imageName = Auxiliary.GetFileNameFromHandle(de.CreateProcessInfo.hFile);
            uint pid = WinApi.GetProcessId(de.CreateProcessInfo.hProcess);
            this.monitorLogger.Log("Process created: " + imageName + " (PID: " + pid + ")");
            return base.OnCreateProcessDebugEvent(ref de);
        }

        protected override WinApi.DbgCode OnCreateThreadDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnCreateThreadDebugEvent called.");
            return base.OnCreateThreadDebugEvent(ref de);
        }

        protected override WinApi.DbgCode OnDatatypeMisalignmentDebugException(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnDatatypeMisalignmentDebugException called.");
            return base.OnDatatypeMisalignmentDebugException(ref de);
        }

        protected override WinApi.DbgCode OnExitProcessDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnExitProcessDebugEvent called.");
            return base.OnExitProcessDebugEvent(ref de);
        }

        protected override WinApi.DbgCode OnExitThreadDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnExitThreadDebugEvent called.");
            return base.OnExitThreadDebugEvent(ref de);
        }

        protected override WinApi.DbgCode OnFltDenormalOperandDebugException(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnFltDenormalOperandDebugException called.");
            return base.OnFltDenormalOperandDebugException(ref de);
        }

        protected override WinApi.DbgCode OnFltDivideByZeroDebugException(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnFltDivideByZeroDebugException called.");
            return base.OnFltDivideByZeroDebugException(ref de);
        }

        protected override WinApi.DbgCode OnFltInexactResultDebugException(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnFltInexactResultDebugException called.");
            return base.OnFltInexactResultDebugException(ref de);
        }

        protected override WinApi.DbgCode OnFltInvalidOperationDebugException(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnFltInvalidOperationDebugException called.");
            return base.OnFltInvalidOperationDebugException(ref de);
        }

        protected override WinApi.DbgCode OnFltOverflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnFltOverflowDebugException called.");
            return base.OnFltOverflowDebugException(ref de);
        }

        protected override WinApi.DbgCode OnFltStackCheckDebugException(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnFltStackCheckDebugException called.");
            return base.OnFltStackCheckDebugException(ref de);
        }

        protected override WinApi.DbgCode OnFltUnderflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnFltUnderflowDebugException called.");
            return base.OnFltUnderflowDebugException(ref de);
        }

        protected override WinApi.DbgCode OnGuardPageDebugException(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnGuardPageDebugException called.");
            return base.OnGuardPageDebugException(ref de);
        }

        protected override WinApi.DbgCode OnIllegalInstructionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnIllegalInstructionDebugException called.");
            return base.OnIllegalInstructionDebugException(ref de);
        }

        protected override WinApi.DbgCode OnInPageErrorDebugException(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnInPageErrorDebugException called.");
            return base.OnInPageErrorDebugException(ref de);
        }

        protected override WinApi.DbgCode OnIntDivideByZeroDebugException(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnIntDivideByZeroDebugException called.");
            return base.OnIntDivideByZeroDebugException(ref de);
        }

        protected override WinApi.DbgCode OnIntOverflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnIntOverflowDebugException called.");
            return base.OnIntOverflowDebugException(ref de);
        }

        protected override WinApi.DbgCode OnInvalidDispositionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnInvalidDispositionDebugException called.");
            return base.OnInvalidDispositionDebugException(ref de);
        }

        protected override WinApi.DbgCode OnLoadDllDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            string dllName = Auxiliary.GetFileNameFromHandle(de.LoadDll.hFile);
            this.monitorLogger.Log("DLL loaded: " + dllName);
            return base.OnLoadDllDebugEvent(ref de);
        }

        protected override WinApi.DbgCode OnNoncontinuableExceptionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnNoncontinuableExceptionDebugException called.");
            return base.OnNoncontinuableExceptionDebugException(ref de);
        }

        protected override WinApi.DbgCode OnOutputDebugStringEvent(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnOutputDebugStringEvent called.");
            return base.OnOutputDebugStringEvent(ref de);
        }

        protected override WinApi.DbgCode OnPrivInstructionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnPrivInstructionDebugException called.");
            return base.OnPrivInstructionDebugException(ref de);
        }

        protected override WinApi.DbgCode OnRipEvent(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnRipEvent called.");
            return base.OnRipEvent(ref de);
        }

        protected override WinApi.DbgCode OnSingleStepDebugException(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnSingleStepDebugException called.");
            return base.OnSingleStepDebugException(ref de);
        }

        protected override WinApi.DbgCode OnStackOverflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            this.monitorLogger.Log("OnStackOverflowDebugException called.");
            return base.OnStackOverflowDebugException(ref de);
        }

        protected override WinApi.DbgCode OnUnloadDllDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            string dllName = Auxiliary.GetFileNameFromHModule(de.UnloadDll.lpBaseOfDll);
            this.monitorLogger.Log("DLL unloaded: " + dllName);
            return base.OnUnloadDllDebugEvent(ref de);
        }

        #endregion
    }
}
