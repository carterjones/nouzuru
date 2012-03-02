namespace Nouzuru
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using Distorm3cs;

    /// <summary>
    /// A simple, but extensible debugger class that provides core debugging traps.
    /// </summary>
    public class Debugger : Patcher
    {
        #region Fields

        /// <summary>
        /// If true, the debugger is permitted to run the debug thread. If false, the debugger is not allowed to
        /// process any further debug events and will shortly exit the debug thread.
        /// </summary>
        private bool allowedToDebug = false;

        /// <summary>
        /// The thread that is used to run the debug loop.
        /// </summary>
        private Thread debugThread;

        /// <summary>
        /// If true, the debugging thread is permitted to exit after the debug loop has been exited. If false, the
        /// debug thread is not permitted to exit, since cleanup routines must first be performed.
        /// </summary>
        private bool threadMayExit = true;

        #endregion

        #region Destructors

        /// <summary>
        /// Finalizes an instance of the Debugger class.
        /// </summary>
        ~Debugger()
        {
            this.UnsetAllBPs();
            if (this.IsDebugging)
            {
                this.StopDebugging();
            }
        }

        #endregion

        #region Enumerations

        /// <summary>
        /// A collection of bit combinations that can be set to a DR# register to adjust hardware breakpoint settings.
        /// </summary>
        /// <remarks>
        /// Most of these values were taken from: http://ce.colddot.nl/browser/Cheat%20Engine/Debugger.pas
        /// </remarks>
        [Flags]
        [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1602:EnumerationItemsMustBeDocumented",
            Justification = "These values are documented at the above referenced URL.")]
        private enum DRegSettings : uint
        {
            reg0set = 0x3,          // (00000000000000000000000000000011)
            reg1set = 0xC,          // (00000000000000000000000000001100)
            reg2set = 0x30,         // (00000000000000000000000000110000)
            reg3set = 0xc0,         // (00000000000000000000000011000000)
            debugexact = 0x300,     // (00000000000000000000001100000000)
            gdflag = 0x2000,        // (00000000000000000010000000000000)
            reg0w = 0x10000,        // (00000000000000010000000000000000)
            reg0r = 0x20000,        // (00000000000000100000000000000000)
            reg0rw = 0x30000,       // (00000000000000110000000000000000)
            reg0len2 = 0x40000,     // (00000000000001000000000000000000)
            reg0len4 = 0xc0000,     // (00000000000011000000000000000000)
            reg1w = 0x100000,       // (00000000000100000000000000000000)
            reg1r = 0x200000,       // (00000000001000000000000000000000)
            reg1rw = 0x300000,      // (00000000001100000000000000000000)
            reg1len2 = 0x400000,    // (00000000010000000000000000000000)
            reg1len4 = 0xc00000,    // (00000000110000000000000000000000)
            reg2w = 0x1000000,      // (00000001000000000000000000000000)
            reg2r = 0x2000000,      // (00000010000000000000000000000000)
            reg2rw = 0x3000000,     // (00000011000000000000000000000000)
            reg2len2 = 0x4000000,   // (00000100000000000000000000000000)
            reg2len4 = 0xc000000,   // (00001100000000000000000000000000)
            reg3w = 0x10000000,     // (00010000000000000000000000000000)
            reg3r = 0x20000000,     // (00100000000000000000000000000000)
            reg3rw = 0x30000000,    // (00110000000000000000000000000000)
            reg3len2 = 0x40000000,  // (01000000000000000000000000000000)
            reg3len4 = 0xc0000000,  // (11000000000000000000000000000000)
            reg0len0 = 0x0,
            reg1len0 = 0x0,
            reg2len0 = 0x0,
            reg3len0 = 0x0,
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether this Debugger is active.
        /// </summary>
        public bool IsDebugging
        {
            get
            {
                return this.debugThread != null && this.debugThread.IsAlive;
            }
        }

        protected bool RestoreBreakpointOnExceptionSingleStep { get; set; }

        protected IntPtr BreakpointAddressJustHit { get; set; }

        protected bool IgnoreNextBreakpoint { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether breakpoint accesses will be logged.
        /// </summary>
        private bool LogBreakpointAccesses { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the values of common registers will be logged when a breakpoint
        /// is hit.
        /// </summary>
        private bool LogRegistersOnBreakpoint { get; set; }

        #endregion

        #region Methods

        protected bool BeginEditThread(uint threadId, out IntPtr hThread, out WinApi.CONTEXT cx)
        {
            WinApi.ThreadAccess threadRights =
                WinApi.ThreadAccess.SET_CONTEXT |
                WinApi.ThreadAccess.GET_CONTEXT |
                WinApi.ThreadAccess.SUSPEND_RESUME;
            IntPtr threadHandle = WinApi.OpenThread(threadRights, false, threadId);
            if (threadHandle == null || threadHandle.Equals(IntPtr.Zero))
            {
                this.Status.Log(
                    "Could not open thread to add hardware breakpoint. Error: " +
                    Marshal.GetLastWin32Error() + ", tid: " + threadId);
                hThread = IntPtr.Zero;
                cx = new WinApi.CONTEXT();
                return false;
            }

            uint res = WinApi.SuspendThread(threadHandle);
            unchecked
            {
                if (res == (uint)(-1))
                {
                    this.Status.Log(
                        "Unable to suspend thread when setting instruction pointer. Error: " +
                        Marshal.GetLastWin32Error() + ", tid: " + this.ThreadID);
                    WinApi.CloseHandle(threadHandle);
                    cx = new WinApi.CONTEXT();
                    hThread = IntPtr.Zero;
                    return false;
                }
            }

            WinApi.CONTEXT context = new WinApi.CONTEXT();

            // TODO: get the most context data from the thread, if FULL cannot get the most.
            context.ContextFlags = WinApi.CONTEXT_FLAGS.FULL;
            if (!WinApi.GetThreadContext(threadHandle, ref context))
            {
                this.Status.Log(
                    "Unable to get thread context when setting instruction pointer. Error: " +
                    Marshal.GetLastWin32Error() + ", tid: " + this.ThreadID);
                WinApi.CloseHandle(threadHandle);
                hThread = IntPtr.Zero;
                cx = new WinApi.CONTEXT();
                return false;
            }

            hThread = threadHandle;
            cx = context;
            return true;
        }

        protected bool EndEditThread(ref IntPtr hThread, ref WinApi.CONTEXT cx)
        {
            // TODO: get the most context data from the thread, if FULL cannot get the most.
            cx.ContextFlags = WinApi.CONTEXT_FLAGS.FULL;
            if (!WinApi.SetThreadContext(hThread, ref cx))
            {
                this.Status.Log(
                    "Unable to set thread context when setting instruction pointer. Error: " +
                    Marshal.GetLastWin32Error() + ", tid: " + this.ThreadID);
                WinApi.CloseHandle(hThread);
                return false;
            }

            uint res = WinApi.ResumeThread(hThread);
            unchecked
            {
                if (res == (uint)(-1))
                {
                    this.Status.Log(
                        "Unable to resume thread when setting instruction pointer. Error: " +
                        Marshal.GetLastWin32Error() + ", tid: " + this.ThreadID);
                    WinApi.CloseHandle(hThread);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Sets the instruction pointer of the main thread to the specified value.
        /// </summary>
        /// <param name="address">The address to which the instruction pointer should be set.</param>
        /// <returns>Returns true if the instruction pointer was successfully set.</returns>
        public bool SetIP(IntPtr address)
        {
            if (!this.IsOpen)
            {
                return false;
            }

            IntPtr hThread;
            WinApi.CONTEXT cx;
            if (!this.BeginEditThread((uint)this.ThreadID, out hThread, out cx))
            {
                return false;
            }

#if WIN64
            // TODO: fix Rip for x64
            //cx.Rip = (ulong)address.ToInt64();
#else
            cx.Eip = (uint)address.ToInt32();
#endif

            if (!this.EndEditThread(ref hThread, ref cx))
            {
                return false;
            }

            return WinApi.CloseHandle(hThread);
        }

        /// <summary>
        /// Sets the instruction pointer of the main thread to the specified value.
        /// </summary>
        /// <param name="address">The address to which the instruction pointer should be set.</param>
        /// <returns>Returns true if the instruction pointer was successfully set.</returns>
        public bool PrepareForSingleStep(IntPtr address)
        {
            if (!this.IsOpen)
            {
                return false;
            }

            IntPtr hThread;
            WinApi.CONTEXT cx;
            if (!this.BeginEditThread((uint)this.ThreadID, out hThread, out cx))
            {
                return false;
            }

#if WIN64
            // TODO: fix Rip for x64
            //cx.Rip = (ulong)address.ToInt64();
#else
            cx.EFlags = 0x100;
#endif
            if (!this.EndEditThread(ref hThread, ref cx))
            {
                return false;
            }

            return WinApi.CloseHandle(hThread);
        }

        /// <summary>
        /// Adds specified address to the Dr0 register.
        /// </summary>
        /// <param name="address">The address at which the breakpoint will be placed.</param>
        /// <returns>Returns true if the breakpoint is added successfully.</returns>
        /// <remarks>Later versions will allow up to 4 concurrent hard breakpoints.</remarks>
        public bool SetHardBP(IntPtr address)
        {
            // TODO: add capability to have up to 4 breakpoints.
            // TODO: write this function so that all threads have the breakpoint.
            if (!this.IsOpen)
            {
                return false;
            }

            WinApi.ThreadAccess threadRights =
                WinApi.ThreadAccess.SET_CONTEXT |
                WinApi.ThreadAccess.GET_CONTEXT |
                WinApi.ThreadAccess.SUSPEND_RESUME;
            IntPtr threadHandle = WinApi.OpenThread(threadRights, false, (uint)this.ThreadID);
            if (threadHandle == null || threadHandle.Equals(IntPtr.Zero))
            {
                this.Status.Log(
                    "Could not open thread to add hardware breakpoint. Error: " +
                    Marshal.GetLastWin32Error() + ", tid: " + this.ThreadID);
                return false;
            }

            uint res = WinApi.SuspendThread(threadHandle);
            unchecked
            {
                if (res == (uint)(-1))
                {
                    this.Status.Log(
                        "Unable to suspend thread when setting breakpoint. Error: " +
                        Marshal.GetLastWin32Error() + ", tid: " + this.ThreadID);
                    WinApi.CloseHandle(threadHandle);
                    return false;
                }
            }

            WinApi.CONTEXT cx = new WinApi.CONTEXT();
            cx.ContextFlags = WinApi.CONTEXT_FLAGS.FULL;
            if (!WinApi.GetThreadContext(threadHandle, ref cx))
            {
                this.Status.Log(
                    "Unable to get thread context when setting breakpoint. Error: " +
                    Marshal.GetLastWin32Error() + ", tid: " + this.ThreadID);
                WinApi.CloseHandle(threadHandle);
                return false;
            }

            cx.ContextFlags = WinApi.CONTEXT_FLAGS.FULL;
#if WIN64
            cx.Dr0 = (uint)address.ToInt64();
#else
            cx.Dr0 = (uint)address.ToInt32();
#endif
            cx.Dr7 =
                (uint)(Debugger.DRegSettings.reg0w | Debugger.DRegSettings.reg0len4 | Debugger.DRegSettings.reg0set);
            if (!WinApi.SetThreadContext(threadHandle, ref cx))
            {
                this.Status.Log(
                    "Unable to set thread context when setting breakpoint. Error: " +
                    Marshal.GetLastWin32Error() + ", tid: " + this.ThreadID);
                WinApi.CloseHandle(threadHandle);
                return false;
            }

            res = WinApi.ResumeThread(threadHandle);
            unchecked
            {
                if (res == (uint)(-1))
                {
                    this.Status.Log(
                        "Unable to resume thread when setting breakpoint. Error: " +
                        Marshal.GetLastWin32Error() + ", tid: " + this.ThreadID);
                    WinApi.CloseHandle(threadHandle);
                    return false;
                }
            }

            WinApi.CloseHandle(threadHandle);
            return true;
        }

        /// <summary>
        /// Sets an int3 breakpoint at the specified address. Saves the old value at the address, so that when the
        /// breakpoint is hit, it can be temporarily replaced, so the code can continue to run, if desired.
        /// </summary>
        /// <param name="address">The address at which the breakpoint will be placed.</param>
        /// <returns>Returns true if the breakpoint is successfully set.</returns>
        public bool SetSoftBP(IntPtr address)
        {
            if (!this.IsOpen)
            {
                return false;
            }

            byte int3Bp = 0xcc;
            return this.Write(address, int3Bp, Patcher.WriteOptions.SaveOldValue);
        }

        /// <summary>
        /// Starts the thread that runs DebugLoop.
        /// </summary>
        /// <returns>Returns true if the thread is started successfully.</returns>
        public bool StartDebugging()
        {
            if (!this.IsOpen)
            {
                return false;
            }

            if (this.debugThread != null && this.debugThread.IsAlive)
            {
                this.debugThread.Join();
            }

            this.debugThread = new Thread(this.DebugLoop);
            this.debugThread.Start();
            return this.debugThread.IsAlive;
        }

        /// <summary>
        /// Performs necessary cleanup and then stops the debugging thread.
        /// </summary>
        /// <returns>Returns true if debugging was successfully stopped.</returns>
        public bool StopDebugging()
        {
            if (!this.IsOpen)
            {
                return false;
            }

            this.UnsetAllBPs();
            this.allowedToDebug = false;

            if (this.debugThread != null)
            {
                while (this.debugThread.IsAlive && !this.threadMayExit)
                {
                }

                this.debugThread.Join();
            }

            return true;
        }

        /// <summary>
        /// Removes all hardware breakpoints from Dr0 through Dr3 registers.
        /// </summary>
        /// <returns>Returns true if all hardware breakpoints are successfully removed.</returns>
        public bool UnsetAllHardBPs()
        {
            if (!this.IsOpen)
            {
                return false;
            }

            WinApi.ThreadAccess threadRights =
                WinApi.ThreadAccess.SET_CONTEXT |
                WinApi.ThreadAccess.GET_CONTEXT |
                WinApi.ThreadAccess.SUSPEND_RESUME;
            IntPtr threadHandle = WinApi.OpenThread(threadRights, false, (uint)this.ThreadID);
            if (threadHandle == null || threadHandle.Equals(IntPtr.Zero))
            {
                this.Status.Log(
                    "Could not open thread to remove hardware breakpoints. Error: " +
                    Marshal.GetLastWin32Error() + ", tid: " + this.ThreadID);
            }

            uint res = WinApi.SuspendThread(threadHandle);
            unchecked
            {
                if (res == (uint)(-1))
                {
                    this.Status.Log(
                        "Unable to suspend thread when removing breakpoints. Error: " +
                        Marshal.GetLastWin32Error() + ", tid: " + this.ThreadID);
                    WinApi.CloseHandle(threadHandle);
                    return false;
                }
            }

            WinApi.CONTEXT cx = new WinApi.CONTEXT();
            cx.ContextFlags = WinApi.CONTEXT_FLAGS.FULL;
            if (!WinApi.GetThreadContext(threadHandle, ref cx))
            {
                WinApi.CloseHandle(threadHandle);
                this.Status.Log(
                    "Unable to get thread context when removing breakpoints. Error: " +
                    Marshal.GetLastWin32Error() + ", tid: " + this.ThreadID);
                return false;
            }

            cx.ContextFlags = WinApi.CONTEXT_FLAGS.FULL;
            cx.Dr0 = 0x0;
            cx.Dr1 = 0x0;
            cx.Dr2 = 0x0;
            cx.Dr3 = 0x0;
            cx.Dr7 = 0x0;
            if (!WinApi.SetThreadContext(threadHandle, ref cx))
            {
                WinApi.CloseHandle(threadHandle);
                this.Status.Log(
                    "Unable to get thread context when removing breakpoints. Error: " +
                    Marshal.GetLastWin32Error() + ", tid: " + this.ThreadID);
                return false;
            }

            res = WinApi.ResumeThread(threadHandle);
            unchecked
            {
                if (res == (uint)(-1))
                {
                    this.Status.Log(
                        "Unable to resume thread when removing breakpoints. Error: " +
                        Marshal.GetLastWin32Error() + ", tid: " + this.ThreadID);
                    WinApi.CloseHandle(threadHandle);
                    return false;
                }
            }

            WinApi.CloseHandle(threadHandle);
            return true;
        }

        /// <summary>
        /// Remove all int3 breakpoints that are tracked by this debugger.
        /// </summary>
        /// <returns>Returns true if all int3 breakpoints are successfully removed.</returns>
        public bool UnsetAllSoftBPs()
        {
            if (!this.IsOpen)
            {
                return false;
            }

            return this.RestoreAll();
        }

        /// <summary>
        /// Removes a hardware breakpoints that targets the specified address.
        /// </summary>
        /// <param name="address">The address at which the breakpoint had been set.</param>
        /// <returns>Returns true if the hardware breakpoint was successfully removed.</returns>
        public bool UnsetHardBP(IntPtr address)
        {
            // TODO: implement UnsetHardBP
            if (!this.IsOpen)
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// Unsets the int3 breakpoint, if it exists, at the specified address.
        /// </summary>
        /// <param name="address">The address at which the breakpoint had been set.</param>
        /// <returns>Returns true if the breakpoint is successfully removed.</returns>
        public bool UnsetSoftBP(IntPtr address)
        {
            if (!this.IsOpen)
            {
                return false;
            }

            return this.Restore(address);
        }

        /// <summary>
        /// Removes all soft and hard breakpoints that were set by this debugger.
        /// </summary>
        /// <returns>Returns true if all breakpoints are successfully removed.</returns>
        public bool UnsetAllBPs()
        {
            if (!this.IsOpen)
            {
                return false;
            }

            bool res = true;
            res &= this.UnsetAllSoftBPs();
            res &= this.UnsetAllHardBPs();
            return res;
        }

        #region Debug Events

        /// <summary>
        /// Handles the CREATE_THREAD_DEBUG_EVENT debug event.
        /// </summary>
        /// <param name="de">The debug event that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnCreateThreadDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the CREATE_PROCESS_DEBUG_EVENT debug event.
        /// </summary>
        /// <param name="de">The debug event that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnCreateProcessDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the EXIT_THREAD_DEBUG_EVENT debug event.
        /// </summary>
        /// <param name="de">The debug event that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnExitThreadDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the EXIT_PROCESS_DEBUG_EVENT debug event.
        /// </summary>
        /// <param name="de">The debug event that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnExitProcessDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the LOAD_DLL_DEBUG_EVENT debug event.
        /// </summary>
        /// <param name="de">The debug event that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnLoadDllDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the UNLOAD_DLL_DEBUG_EVENT debug event.
        /// </summary>
        /// <param name="de">The debug event that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnUnloadDllDebugEvent(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the OUTPUT_DEBUG_STRING_EVENT debug event.
        /// </summary>
        /// <param name="de">The debug event that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnOutputDebugStringEvent(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the RIP_EVENT debug event.
        /// </summary>
        /// <param name="de">The debug event that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnRipEvent(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        #endregion

        #region Debug Exceptions

        /// <summary>
        /// Handles the EXCEPTION_ACCESS_VIOLATION debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnAccessViolationDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the EXCEPTION_ARRAY_BOUNDS_EXCEEDED debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnArrayBoundsExceededDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the EXCEPTION_BREAKPOINT debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnBreakpointDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.IgnoreNextBreakpoint)
            {
                this.IgnoreNextBreakpoint = false;
            }
            else
            {
                this.Restore(de.u.Exception.ExceptionRecord.ExceptionAddress, false);
                this.SetIP(de.u.Exception.ExceptionRecord.ExceptionAddress);
                this.PrepareForSingleStep(de.u.Exception.ExceptionRecord.ExceptionAddress);
                this.BreakpointAddressJustHit = de.u.Exception.ExceptionRecord.ExceptionAddress;
                this.RestoreBreakpointOnExceptionSingleStep = true;
            }

            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the EXCEPTION_DATATYPE_MISALIGNMENT debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnDatatypeMisalignmentDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the EXCEPTION_FLT_DENORMAL_OPERAND debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnFltDenormalOperandDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the EXCEPTION_FLT_DIVIDE_BY_ZERO debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnFltDivideByZeroDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the EXCEPTION_FLT_INEXACT_RESULT debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnFltInexactResultDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the EXCEPTION_FLT_INVALID_OPERATION debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnFltInvalidOperationDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the EXCEPTION_FLT_OVERFLOW debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnFltOverflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the EXCEPTION_FLT_STACK_CHECK debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnFltStackCheckDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the EXCEPTION_FLT_UNDERFLOW debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnFltUnderflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the EXCEPTION_ILLEGAL_INSTRUCTION debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnIllegalInstructionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the EXCEPTION_IN_PAGE_ERROR debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnInPageErrorDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the EXCEPTION_INT_DIVIDE_BY_ZERO debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnIntDivideByZeroDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the EXCEPTION_INT_OVERFLOW debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnIntOverflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the EXCEPTION_INVALID_DISPOSITION debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnInvalidDispositionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the EXCEPTION_NONCONTINUABLE_EXCEPTION debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnNoncontinuableExceptionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the EXCEPTION_PRIV_INSTRUCTION debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnPrivInstructionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the EXCEPTION_SINGLE_STEP debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnSingleStepDebugException(ref WinApi.DEBUG_EVENT de)
        {
            WinApi.ThreadAccess thread_rights =
                WinApi.ThreadAccess.SET_CONTEXT | WinApi.ThreadAccess.GET_CONTEXT | WinApi.ThreadAccess.SUSPEND_RESUME;
            WinApi.CONTEXT cx = new WinApi.CONTEXT();
            IntPtr threadHandle = WinApi.OpenThread(thread_rights, false, de.dwThreadId);
            WinApi.SuspendThread(threadHandle);
#if WIN64
                                cx.ContextFlags = WinApi.CONTEXT_FLAGS.FULL | WinApi.CONTEXT_FLAGS.FLOATING_POINT |
                                    WinApi.CONTEXT_FLAGS.DEBUG_REGISTERS;
#else
            cx.ContextFlags = WinApi.CONTEXT_FLAGS.FULL | WinApi.CONTEXT_FLAGS.FLOATING_POINT |
                WinApi.CONTEXT_FLAGS.EXTENDED_REGISTERS | WinApi.CONTEXT_FLAGS.DEBUG_REGISTERS;
#endif
            WinApi.GetThreadContext(threadHandle, ref cx);
            WinApi.ResumeThread(threadHandle);
            if (this.LogRegistersOnBreakpoint)
            {
#if WIN64
                                    this.Status.Log(
                                        "rax:" + cx.Rax.ToString("X").PadLeft(16, '0') +
                                        "rbx:" + cx.Rbx.ToString("X").PadLeft(16, '0') +
                                        "rcx:" + cx.Rcx.ToString("X").PadLeft(16, '0') +
                                        "rdx:" + cx.Rdx.ToString("X").PadLeft(16, '0') +
                                        "rip:" + cx.Rip.ToString("X").PadLeft(16, '0') +
                                        "rbp:" + cx.Rbp.ToString("X").PadLeft(16, '0'));
#else
                this.Status.Log(
                    "eax:" + cx.Eax.ToString("X").PadLeft(8, '0') +
                    "ebx:" + cx.Ebx.ToString("X").PadLeft(8, '0') +
                    "ecx:" + cx.Ecx.ToString("X").PadLeft(8, '0') +
                    "edx:" + cx.Edx.ToString("X").PadLeft(8, '0') +
                    "eip:" + cx.Eip.ToString("X").PadLeft(8, '0') +
                    "ebp:" + cx.Ebp.ToString("X").PadLeft(8, '0'));
#endif
                this.Status.Log(
                    "dr0:" + cx.Dr0.ToString("X").PadLeft(8, '0') +
                    "dr1:" + cx.Dr1.ToString("X").PadLeft(8, '0') +
                    "dr2:" + cx.Dr2.ToString("X").PadLeft(8, '0') +
                    "dr3:" + cx.Dr3.ToString("X").PadLeft(8, '0') +
                    "dr6:" + cx.Dr6.ToString("X").PadLeft(8, '0') +
                    "dr7:" + cx.Dr7.ToString("X").PadLeft(8, '0'));
            }
#if WIN64
                                prevInstSize = GetPreviousInstructionSize(new IntPtr(cx.Rip));
                                if (PrintAccesses)
                                {
                                    logger.Log(
                                        "Modifying address is " +
                                        this.IntPtrToFormattedAddress(new IntPtr((cx.Rip - prevInstSize))) +
                                        " with instruction length " + prevInstSize);
                                }
#else
            uint prevInstSize = this.GetPreviousInstructionSize(new IntPtr(cx.Eip));
            IntPtr prevInstruction = new IntPtr(cx.Eip - prevInstSize);
            if (this.LogBreakpointAccesses)
            {
                this.Status.Log(
                    "Modifying address is " +
                    this.IntPtrToFormattedAddress(prevInstruction) +
                    " with instruction length " + prevInstSize);
            }

            if (this.RestoreBreakpointOnExceptionSingleStep == true)
            {
                // TODO: eventually replace breakpointAddressJustHit with a check of Dr6.
                if (this.BreakpointAddressJustHit != IntPtr.Zero)
                {
                    this.Write(this.BreakpointAddressJustHit, (byte)0xcc, WriteOptions.None);
                    this.Status.Log(
                        "Restoring breakpoint at " +
                        this.IntPtrToFormattedAddress(this.BreakpointAddressJustHit));
                    this.BreakpointAddressJustHit = IntPtr.Zero;
                }
                else
                {
                    this.Status.Log(
                        "Unexpected series of events during breakpoint restoration.",
                        Logger.Logger.Level.HIGH);
                }

                this.RestoreBreakpointOnExceptionSingleStep = false;
            }
#endif
            return WinApi.DbgCode.CONTINUE;
        }

        /// <summary>
        /// Handles the EXCEPTION_STACK_OVERFLOW debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnStackOverflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.CONTINUE;
        }

        #endregion

        /// <summary>
        /// This is the main debug loop, which is called as a single thread that attaches to the target process, using
        /// debugging privileges.
        /// </summary>
        private void DebugLoop()
        {
            if (!this.IsOpen)
            {
                return;
            }

            // Set IgnoreNextBreakpoint to true to account for the first breakpoint that is encountered when attaching
            // to a process in debug mode.
            this.IgnoreNextBreakpoint = true;

#if DEBUG
            this.Status.Log("pid: " + this.PID);
#endif
            if (!WinApi.DebugActiveProcess((uint)this.PID))
            {
#if DEBUG
                this.Status.Log("Cannot debug.", Logger.Logger.Level.HIGH);
#endif
            }
            else if (!WinApi.DebugSetProcessKillOnExit(false))
            {
#if DEBUG
                this.Status.Log("Cannot exit cleanly in the future.", Logger.Logger.Level.MEDIUM);
#endif
            }
            else
            {
#if DEBUG
                this.Status.Log("Now debugging.", Logger.Logger.Level.NONE);
#endif
                this.allowedToDebug = true;
                this.threadMayExit = false;
            }

            WinApi.DEBUG_EVENT de = new WinApi.DEBUG_EVENT();
            WinApi.DbgCode continueStatus = WinApi.DbgCode.CONTINUE;
#if DEBUG
            this.Status.Log("ProcessThreadId: " + this.ThreadID);
#endif
            WinApi.ThreadAccess thread_rights =
                WinApi.ThreadAccess.SET_CONTEXT | WinApi.ThreadAccess.GET_CONTEXT | WinApi.ThreadAccess.SUSPEND_RESUME;
            IntPtr threadHandle = WinApi.OpenThread(thread_rights, false, (uint)this.ThreadID);
#if DEBUG
            this.Status.Log("hThread: " + this.IntPtrToFormattedAddress(threadHandle));
#endif
            WinApi.CONTEXT cx = new WinApi.CONTEXT();
            cx.ContextFlags = WinApi.CONTEXT_FLAGS.DEBUG_REGISTERS;
            WinApi.GetThreadContext(threadHandle, ref cx);
            cx.ContextFlags = WinApi.CONTEXT_FLAGS.DEBUG_REGISTERS;
            cx.Dr7 =
                (uint)(Debugger.DRegSettings.reg0w | Debugger.DRegSettings.reg0len4 | Debugger.DRegSettings.reg0set);
            bool stc = WinApi.SetThreadContext(threadHandle, ref cx);
            WinApi.CloseHandle(threadHandle);
            threadHandle = IntPtr.Zero;

            while (this.Proc.HasExited == false && this.allowedToDebug == true)
            {
                WinApi.WaitForDebugEvent(ref de, 100);
                switch (de.dwDebugEventCode)
                {
                    case (uint)WinApi.DebugEventType.EXCEPTION_DEBUG_EVENT:
                        switch (de.u.Exception.ExceptionRecord.ExceptionCode)
                        {
                            case (uint)WinApi.ExceptionType.SINGLE_STEP:
                                continueStatus = OnSingleStepDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.ACCESS_VIOLATION:
                                continueStatus = OnAccessViolationDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.ARRAY_BOUNDS_EXCEEDED:
                                continueStatus = OnArrayBoundsExceededDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.BREAKPOINT:
                                continueStatus = OnBreakpointDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.DATATYPE_MISALIGNMENT:
                                continueStatus = OnDatatypeMisalignmentDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.FLT_DENORMAL_OPERAND:
                                continueStatus = OnFltDenormalOperandDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.FLT_DIVIDE_BY_ZERO:
                                continueStatus = OnFltDivideByZeroDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.FLT_INEXACT_RESULT:
                                continueStatus = OnFltInexactResultDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.FLT_INVALID_OPERATION:
                                continueStatus = OnFltInvalidOperationDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.FLT_OVERFLOW:
                                continueStatus = OnFltOverflowDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.FLT_STACK_CHECK:
                                continueStatus = OnFltStackCheckDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.FLT_UNDERFLOW:
                                continueStatus = OnFltUnderflowDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.ILLEGAL_INSTRUCTION:
                                continueStatus = OnIllegalInstructionDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.IN_PAGE_ERROR:
                                continueStatus = OnInPageErrorDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.INT_DIVIDE_BY_ZERO:
                                continueStatus = OnIntDivideByZeroDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.INT_OVERFLOW:
                                continueStatus = OnIntOverflowDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.INVALID_DISPOSITION:
                                continueStatus = OnInvalidDispositionDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.NONCONTINUABLE_EXCEPTION:
                                continueStatus = OnNoncontinuableExceptionDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.PRIV_INSTRUCTION:
                                continueStatus = OnPrivInstructionDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.STACK_OVERFLOW:
                                continueStatus = OnStackOverflowDebugException(ref de);
                                break;

                            default:
#if WIN64
                                // TODO: figure out why this occurs here in 64 bit mode, rather than up higher
                                logger.Log("stepped.");
                                hThread = WinApi.OpenThread(thread_rights, false, de.dwThreadId);
                                WinApi.SuspendThread(hThread);
                                cx.ContextFlags = WinApi.CONTEXT_FLAGS.FULL | WinApi.CONTEXT_FLAGS.FLOATING_POINT |
                                    WinApi.CONTEXT_FLAGS.DEBUG_REGISTERS;
                                WinApi.GetThreadContext(hThread, ref cx);
                                WinApi.ResumeThread(hThread);
                                if (PrintRegistersOnBreakpoint)
                                {
                                    logger.Log(
                                        "rax:" + cx.Rax.ToString("X").PadLeft(16, '0') +
                                        "rbx:" + cx.Rbx.ToString("X").PadLeft(16, '0') +
                                        "rcx:" + cx.Rcx.ToString("X").PadLeft(16, '0') +
                                        "rdx:" + cx.Rdx.ToString("X").PadLeft(16, '0') +
                                        "rip:" + cx.Rip.ToString("X").PadLeft(16, '0') +
                                        "rbp:" + cx.Rbp.ToString("X").PadLeft(16, '0'));
                                    logger.Log(
                                        "dr0:" + cx.Dr0.ToString("X").PadLeft(8, '0') +
                                        "dr1:" + cx.Dr1.ToString("X").PadLeft(8, '0') +
                                        "dr2:" + cx.Dr2.ToString("X").PadLeft(8, '0') +
                                        "dr3:" + cx.Dr3.ToString("X").PadLeft(8, '0') +
                                        "dr6:" + cx.Dr6.ToString("X").PadLeft(8, '0') +
                                        "dr7:" + cx.Dr7.ToString("X").PadLeft(8, '0'));
                                }

                                prevInstSize = GetPreviousInstructionSize(new IntPtr(cx.Rip));
                                if (PrintAccesses)
                                {
                                    logger.Log(
                                        "Modifying address is " +
                                        this.IntPtrToFormattedAddress(new IntPtr((cx.Rip - prevInstSize))) +
                                        " with instruction length " + prevInstSize);
                                }
#endif
                                break;
                        }

                        break;

                    case (uint)WinApi.DebugEventType.CREATE_THREAD_DEBUG_EVENT:
                        continueStatus = this.OnCreateThreadDebugEvent(ref de);
                        break;

                    case (uint)WinApi.DebugEventType.CREATE_PROCESS_DEBUG_EVENT:
                        continueStatus = this.OnCreateProcessDebugEvent(ref de);
                        break;

                    case (uint)WinApi.DebugEventType.EXIT_THREAD_DEBUG_EVENT:
                        continueStatus = this.OnExitThreadDebugEvent(ref de);
                        break;

                    case (uint)WinApi.DebugEventType.EXIT_PROCESS_DEBUG_EVENT:
                        continueStatus = this.OnExitProcessDebugEvent(ref de);
                        break;

                    case (uint)WinApi.DebugEventType.LOAD_DLL_DEBUG_EVENT:
                        continueStatus = this.OnLoadDllDebugEvent(ref de);
                        break;

                    case (uint)WinApi.DebugEventType.UNLOAD_DLL_DEBUG_EVENT:
                        continueStatus = this.OnUnloadDllDebugEvent(ref de);
                        break;

                    case (uint)WinApi.DebugEventType.OUTPUT_DEBUG_STRING_EVENT:
                        continueStatus = this.OnOutputDebugStringEvent(ref de);
                        break;

                    case (uint)WinApi.DebugEventType.RIP_EVENT:
                        continueStatus = this.OnRipEvent(ref de);
                        break;

                    default:
                        break;
                }

                WinApi.ContinueDebugEvent(de.dwProcessId, de.dwThreadId, continueStatus);
            }

            if (!WinApi.DebugActiveProcessStop((uint)this.PID))
            {
                this.Status.Log("Failed to stop debugging. Error: " + Marshal.GetLastWin32Error());
            }

            this.threadMayExit = true;
            return;
        }

        /// <summary>
        /// Disassembles the area around the specified address to find the length of the instruction that precedes the
        /// instruction at the specified address.
        /// </summary>
        /// <param name="address">The address that occurs after the instruction of interest.</param>
        /// <returns>Returns the size of the instruction that occurs before the specified address.</returns>
        private uint GetPreviousInstructionSize(IntPtr address)
        {
            if (!this.IsOpen)
            {
                return 0;
            }

            // Read the memory around the address. Read from an address that is at least two instructions away from
            // where the target address's instruction occurs.
            IntPtr beginBufAddress = IntPtr.Subtract(address, 28);
            int bufSize = 50;
            byte[] buffer = Enumerable.Repeat((byte)0, bufSize).ToArray();
            if (!this.Read(beginBufAddress, buffer))
            {
#if DEBUG
                this.Status.Log("Could not read memory at " + this.IntPtrToFormattedAddress(beginBufAddress));
#endif
                return 0;
            }

            // Disassemble the memory around the address.
            Distorm.DInst[] insts = Distorm.Decompose(buffer, (uint)beginBufAddress.ToInt64());

            for (uint i = 0; i < insts.Length; ++i)
            {
                if (insts[i].addr >= (ulong)address.ToInt64())
                {
                    return insts[i - 1].size;
                }
            }

            // Return nothing if a compatible instruction is not found.
            return 0;
        }
        
        #endregion
    }
}
