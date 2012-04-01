namespace Nouzuru
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using Logger;

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
        /// True if the debug thread initialization has completed.
        /// </summary>
        private bool debugThreadInitComplete = false;

        /// <summary>
        /// True if the debug thread initialization was successful.
        /// </summary>
        private bool debugThreadInitSuccess = false;

        /// <summary>
        /// If true, the debugger will set a breakpoint on the first instruction that will be executed by the main
        /// module of the executable.
        /// </summary>
        private bool pauseOnFirstInst = false;

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
#if WIN64
        private enum DRegSettings : ulong
#else
        private enum DRegSettings : uint
#endif
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
        /// Gets or sets a value indicating whether 1st chance exceptions will be ignored.
        /// </summary>
        public bool IgnoreFirstChanceExceptions { get; set; }

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

        /// <summary>
        /// Gets or sets a value indicating whether breakpoint accesses will be logged.
        /// </summary>
        public bool LogBreakpointAccesses { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the values of common registers will be logged when a breakpoint
        /// is hit.
        /// </summary>
        public bool LogRegistersOnBreakpoint { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Launches a process in debug mode using the executable at the supplied path.
        /// </summary>
        /// <param name="filePath">The location of the executable to be launched.</param>
        /// <param name="parameters">The command line parameters to pass to the executable when it is launched.</param>
        /// <param name="pauseOnFirstInst">
        /// If true, the debugger will set a breakpoint on the first instruction that will be executed by the main
        /// module of the executable.
        /// </param>
        /// <returns>Returns true if the process was successfully started in debug mode.</returns>
        public bool StartAndDebug(string filePath, string parameters = "", bool pauseOnFirstInst = false)
        {
            if (this.debugThread != null && this.debugThread.IsAlive)
            {
                this.debugThread.Join();
            }

            this.pauseOnFirstInst = pauseOnFirstInst;
            DebugLoopArguments dla = new DebugLoopArguments();
            dla.FilePath = filePath;
            dla.Parameters = parameters;
            this.debugThread = new Thread(this.StartDebugLoop);
            this.debugThread.Start(dla);
            while (!this.debugThreadInitComplete)
            {
                Thread.Sleep(1);
            }

            return this.debugThreadInitSuccess;
        }

        /// <summary>
        /// Launches a process in debug mode using the executable at the supplied path.
        /// </summary>
        /// <param name="filePath">The location of the executable to be launched.</param>
        /// <param name="pauseOnFirstInst">
        /// If true, the debugger will set a breakpoint on the first instruction that will be executed by the main
        /// module of the executable.
        /// </param>
        /// <returns>Returns true if the process was successfully started in debug mode.</returns>
        public bool StartAndDebug(string filePath, bool pauseOnFirstInst)
        {
            return this.StartAndDebug(filePath, string.Empty, pauseOnFirstInst);
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

            IntPtr threadHandle;
            WinApi.CONTEXT cx;
            if (!this.BeginEditThread((uint)this.ThreadID, out threadHandle, out cx))
            {
                return false;
            }

#if WIN64
            // TODO: fix Rip for x64
            ////cx.Rip = (ulong)address.ToInt64();
#else
            cx.Eip = (uint)address.ToInt32();
#endif

            if (!this.EndEditThread(ref threadHandle, ref cx))
            {
                return false;
            }

            return WinApi.CloseHandle(threadHandle);
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

            IntPtr threadHandle;
            WinApi.CONTEXT cx;
            if (!this.BeginEditThread((uint)this.ThreadID, out threadHandle, out cx))
            {
                return false;
            }

#if WIN64
            // TODO: fix Rip for x64
            ////cx.Rip = (ulong)address.ToInt64();
#else
            cx.EFlags = 0x100;
#endif
            if (!this.EndEditThread(ref threadHandle, ref cx))
            {
                return false;
            }

            return WinApi.CloseHandle(threadHandle);
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

            this.debugThread = new Thread(this.StartDebugLoop);
            this.debugThread.Start();
            while (!this.debugThreadInitComplete)
            {
                Thread.Sleep(1);
            }

            return this.debugThreadInitSuccess;
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

        /// <summary>
        /// Suspend the thread and prepare the thread context to be modified.
        /// </summary>
        /// <param name="threadId">The ID of the thread to be modified.</param>
        /// <param name="threadHandle">A handle of the thread to be modified.</param>
        /// <param name="cx">The context of the thread to be modified.</param>
        /// <returns>Returns true if the thread was successfully paused and prepared for modification.</returns>
        protected bool BeginEditThread(uint threadId, out IntPtr threadHandle, out WinApi.CONTEXT cx)
        {
            WinApi.ThreadAccess threadRights =
                WinApi.ThreadAccess.SET_CONTEXT |
                WinApi.ThreadAccess.GET_CONTEXT |
                WinApi.ThreadAccess.SUSPEND_RESUME;
            threadHandle = WinApi.OpenThread(threadRights, false, threadId);
            if (threadHandle == null || threadHandle.Equals(IntPtr.Zero))
            {
                this.Status.Log(
                    "Could not open thread to add hardware breakpoint. Error: " +
                    Marshal.GetLastWin32Error() + ", tid: " + threadId);
                threadHandle = IntPtr.Zero;
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
                    threadHandle = IntPtr.Zero;
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
                threadHandle = IntPtr.Zero;
                cx = new WinApi.CONTEXT();
                return false;
            }

            cx = context;
            return true;
        }

        /// <summary>
        /// Apply the thread context modification and resume the thread.
        /// </summary>
        /// <param name="threadHandle">A handle of the thread to be modified.</param>
        /// <param name="cx">The context of the thread to be modified.</param>
        /// <returns>Returns true if the thread was successfully modified and resumed.</returns>
        protected bool EndEditThread(ref IntPtr threadHandle, ref WinApi.CONTEXT cx)
        {
            // TODO: get the most context data from the thread, if FULL cannot get the most.
            cx.ContextFlags = WinApi.CONTEXT_FLAGS.FULL;
            if (!WinApi.SetThreadContext(threadHandle, ref cx))
            {
                this.Status.Log(
                    "Unable to set thread context when setting instruction pointer. Error: " +
                    Marshal.GetLastWin32Error() + ", tid: " + this.ThreadID);
                WinApi.CloseHandle(threadHandle);
                return false;
            }

            uint res = WinApi.ResumeThread(threadHandle);
            unchecked
            {
                if (res == (uint)(-1))
                {
                    this.Status.Log(
                        "Unable to resume thread when setting instruction pointer. Error: " +
                        Marshal.GetLastWin32Error() + ", tid: " + this.ThreadID);
                    WinApi.CloseHandle(threadHandle);
                    return false;
                }
            }

            return true;
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
        /// Handles the EXCEPTION_GUARD_PAGE debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnGuardPageDebugException(ref WinApi.DEBUG_EVENT de)
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

        /// <summary>
        /// Handles the default case where no debug exception can handle this debug event.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnUnhandledDebugException(ref WinApi.DEBUG_EVENT de)
        {
            this.Status.Log(
                "An unhandled (default) debug exception occurred. " +
                "Exception code: 0x" + de.Exception.ExceptionRecord.ExceptionCode.ToString("x"),
                Logger.Level.DEBUG);
            return WinApi.DbgCode.CONTINUE;
        }

        #endregion

        /// <summary>
        /// Starts the main debug loop, optionally creating a process from the supplied file path.
        /// </summary>
        /// <param name="arguments">An optional set of arguments that dictate how an executable is debugged.</param>
        private void StartDebugLoop(object arguments = null)
        {
            string filePath;
            string parameters;
            if (arguments == null)
            {
                filePath = string.Empty;
                parameters = string.Empty;
            }
            else if (arguments is DebugLoopArguments)
            {
                DebugLoopArguments dla = (DebugLoopArguments)arguments;
                filePath = dla.FilePath;
                parameters = dla.Parameters;
            }
            else
            {
                filePath = string.Empty;
                parameters = string.Empty;
            }

            if (arguments == null)
            {
                if (!this.IsOpen)
                {
                    return;
                }

                if (!WinApi.DebugActiveProcess((uint)this.PID))
                {
#if DEBUG
                    this.Status.Log("Cannot debug. Error: " + Marshal.GetLastWin32Error(), Logger.Level.HIGH);
#endif
                    return;
                }
                else if (!WinApi.DebugSetProcessKillOnExit(false))
                {
#if DEBUG
                    this.Status.Log("Cannot exit cleanly in the future.", Logger.Level.MEDIUM);
#endif
                }
                else
                {
#if DEBUG
                    this.Status.Log("Now debugging.", Logger.Level.NONE);
#endif
                    this.allowedToDebug = true;
                    this.threadMayExit = false;
                }

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
                this.DebugLoop();
                return;
            }
            else
            {
                if (!SysInteractor.IsInitialized)
                {
                    SysInteractor.Init();
                }

                bool res = false;
                string application = filePath;
                string commandLine = string.Empty;
                if (!string.IsNullOrEmpty(parameters))
                {
                    commandLine = filePath + " " + parameters;
                }

                WinApi.PROCESS_INFORMATION procInfo = new WinApi.PROCESS_INFORMATION();
                WinApi.STARTUPINFO startupInfo = new WinApi.STARTUPINFO();
                WinApi.SECURITY_ATTRIBUTES processSecurity = new WinApi.SECURITY_ATTRIBUTES();
                WinApi.SECURITY_ATTRIBUTES threadSecurity = new WinApi.SECURITY_ATTRIBUTES();
                processSecurity.nLength = Marshal.SizeOf(processSecurity);
                threadSecurity.nLength = Marshal.SizeOf(threadSecurity);

                // Open the process.
                res = WinApi.CreateProcess(
                    application,
                    commandLine,
                    ref processSecurity,
                    ref threadSecurity,
                    false,
                    (uint)WinApi.ProcessCreationFlags.DEBUG_PROCESS,
                    IntPtr.Zero,
                    null,
                    ref startupInfo,
                    out procInfo);
                if (!res)
                {
                    return;
                }

                this.ProcHandle = procInfo.hProcess;
                if (this.ProcHandle != null)
                {
                    try
                    {
                        this.Proc = System.Diagnostics.Process.GetProcessById(procInfo.dwProcessId);
                    }
                    catch (ArgumentException)
                    {
                        WinApi.CloseHandle(this.ProcHandle);
                        return;
                    }

                    bool isWow64;
                    WinApi.IsWow64Process(this.ProcHandle, out isWow64);

                    // 64-bit process detection.
                    // Note: This does not take into account for PAE. No plans to support PAE currently exist.
                    if (isWow64)
                    {
                        // For scanning purposes, Wow64 processes will be treated as as 32-bit processes.
                        this.Is64Bit = false;
                    }
                    else
                    {
                        // If it is not Wow64, then the process is natively running, so set it according to the OS
                        // architecture.
                        this.Is64Bit = SysInteractor.Is64Bit;
                    }

                    if (!WinApi.DebugSetProcessKillOnExit(false))
                    {
#if DEBUG
                        this.Status.Log("Cannot exit cleanly in the future.", Logger.Level.MEDIUM);
#endif
                    }
                    else
                    {
#if DEBUG
                        this.Status.Log("Now debugging.", Logger.Level.NONE);
#endif
                        this.allowedToDebug = true;
                        this.threadMayExit = false;
                    }

                    this.DebugLoop();

                    return;
                }

                this.Status.Log("Unable to open the target process.", Logger.Level.HIGH);
            }
        }

        /// <summary>
        /// This is the main debug loop, which is called as a single thread that attaches to the target process, using
        /// debugging privileges.
        /// </summary>
        private void DebugLoop()
        {
            WinApi.DEBUG_EVENT de = new WinApi.DEBUG_EVENT();
            WinApi.DbgCode continueStatus = WinApi.DbgCode.CONTINUE;
            bool isFirstInstBreakpointSet = false;

            while (this.Proc.HasExited == false && this.allowedToDebug == true)
            {
                if (!isFirstInstBreakpointSet && this.pauseOnFirstInst)
                {
                    try
                    {
                        // Attempt to set a breakpoint at the main module's entry point.
                        this.SetSoftBP(this.Proc.MainModule.EntryPointAddress);
                        isFirstInstBreakpointSet = true;
                    }
                    catch (System.ComponentModel.Win32Exception e)
                    {
                        if (e.NativeErrorCode == 299)
                        {
                            // Catch and ignore the partial ReadProcessMemory/WriteProcessMemory exception.
                        }
                        else
                        {
                            // Otherwise, log the exception message.
                            this.Status.Log("Unhandled win32 exception: " + e.Message, Logger.Level.HIGH);
                        }
                    }
                }

                WinApi.WaitForDebugEvent(ref de, 100);
                switch (de.dwDebugEventCode)
                {
                    case (uint)WinApi.DebugEventType.EXCEPTION_DEBUG_EVENT:
                        if (this.IgnoreFirstChanceExceptions && de.Exception.dwFirstChance != 0)
                        {
                            continueStatus = WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
                            break;
                        }

                        switch (de.Exception.ExceptionRecord.ExceptionCode)
                        {
                            case (uint)WinApi.ExceptionType.SINGLE_STEP:
                                continueStatus = this.OnSingleStepDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.ACCESS_VIOLATION:
                                continueStatus = this.OnAccessViolationDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.ARRAY_BOUNDS_EXCEEDED:
                                continueStatus = this.OnArrayBoundsExceededDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.BREAKPOINT:
                                continueStatus = this.OnBreakpointDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.DATATYPE_MISALIGNMENT:
                                continueStatus = this.OnDatatypeMisalignmentDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.FLT_DENORMAL_OPERAND:
                                continueStatus = this.OnFltDenormalOperandDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.FLT_DIVIDE_BY_ZERO:
                                continueStatus = this.OnFltDivideByZeroDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.FLT_INEXACT_RESULT:
                                continueStatus = this.OnFltInexactResultDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.FLT_INVALID_OPERATION:
                                continueStatus = this.OnFltInvalidOperationDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.FLT_OVERFLOW:
                                continueStatus = this.OnFltOverflowDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.FLT_STACK_CHECK:
                                continueStatus = this.OnFltStackCheckDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.FLT_UNDERFLOW:
                                continueStatus = this.OnFltUnderflowDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.GUARD_PAGE:
                                continueStatus = this.OnGuardPageDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.ILLEGAL_INSTRUCTION:
                                continueStatus = this.OnIllegalInstructionDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.IN_PAGE_ERROR:
                                continueStatus = this.OnInPageErrorDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.INT_DIVIDE_BY_ZERO:
                                continueStatus = this.OnIntDivideByZeroDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.INT_OVERFLOW:
                                continueStatus = this.OnIntOverflowDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.INVALID_DISPOSITION:
                                continueStatus = this.OnInvalidDispositionDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.NONCONTINUABLE_EXCEPTION:
                                continueStatus = this.OnNoncontinuableExceptionDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.PRIV_INSTRUCTION:
                                continueStatus = this.OnPrivInstructionDebugException(ref de);
                                break;

                            case (uint)WinApi.ExceptionType.STACK_OVERFLOW:
                                continueStatus = this.OnStackOverflowDebugException(ref de);
                                break;

                            default:
                                continueStatus = this.OnUnhandledDebugException(ref de);
                                break;
                        }

                        break;

                    case (uint)WinApi.DebugEventType.CREATE_THREAD_DEBUG_EVENT:
                        continueStatus = this.OnCreateThreadDebugEvent(ref de);
                        break;

                    case (uint)WinApi.DebugEventType.CREATE_PROCESS_DEBUG_EVENT:
                        this.debugThreadInitSuccess = true;
                        this.debugThreadInitComplete = true;
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

            this.debugThreadInitComplete = true;

            if (!WinApi.DebugActiveProcessStop((uint)this.PID))
            {
                this.Status.Log("Failed to stop debugging. Error: " + Marshal.GetLastWin32Error());
            }

            this.threadMayExit = true;
            return;
        }
        
        #endregion

        #region Structures

        /// <summary>
        /// Arguments passed to the debug thread.
        /// </summary>
        private struct DebugLoopArguments
        {
            /// <summary>
            /// The path to the executable to be debugged.
            /// </summary>
            public string FilePath;

            /// <summary>
            /// The command line parameters to pass to the executable when it is launched.
            /// </summary>
            public string Parameters;
        }

        #endregion
    }
}
