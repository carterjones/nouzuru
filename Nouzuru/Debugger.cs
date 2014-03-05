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
    /// A simple, but extensible, debugger class that provides core debugging traps.
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
        /// The current address of the target process at the last point the process was paused.
        /// </summary>
        private IntPtr currentAddress = IntPtr.Zero;

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
        /// A lock that is used to pause the debugger loop when a second chance exception has occurred.
        /// </summary>
        private ManualResetEvent pauseDebuggerLock = new ManualResetEvent(false);

        /// <summary>
        /// If true, a step over operation is in progress.
        /// </summary>
        private bool stepOverOperationInProgress = false;

        /// <summary>
        /// If true, the debugging thread is permitted to exit after the debug loop has been exited. If false, the
        /// debug thread is not permitted to exit, since cleanup routines must first be performed.
        /// </summary>
        private bool threadMayExit = true;

        /// <summary>
        /// The state of the target process when the target is paused.
        /// </summary>
        private TargetState ts = new TargetState();

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the Debugger class.
        /// </summary>
        public Debugger()
            : base()
        {
            // Use WinApi.INFINITE to wait indefinitely until an event occurs.
            this.DebugEventTimeout = 1;
            this.Settings = new DebuggerSettings();
        }

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
        /// Gets or sets a value indicating whether the debugger should pause the debugger loop after a second chance
        /// exception has occurred and been handled.
        /// </summary>
        public bool PauseOnSecondChanceException { get; set; }

        /// <summary>
        /// Gets the current address if the target process is paused. Otherwise, returns IntPtr.Zero.
        /// </summary>
        public IntPtr CurrentAddress
        {
            get
            {
                if (this.IsDebuggingPaused)
                {
                    return this.currentAddress;
                }
                else
                {
                    return IntPtr.Zero;
                }
            }

            private set
            {
                this.currentAddress = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether 1st chance exceptions will be ignored.
        /// </summary>
        public bool IgnoreFirstChanceExceptions { get; set; }

        /// <summary>
        /// Gets a value indicating whether the debugger is currently processing a debug event.
        /// </summary>
        public bool IsBusy { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether the initial breakpoint has been encountered by the debugger.
        /// </summary>
        public bool InitialBreakpointHit { get; protected set; }

        /// <summary>
        /// Gets a value indicating whether the main debugging loop is currently paused.
        /// </summary>
        public bool IsDebuggingPaused { get; private set; }

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
        /// Gets or sets the number of milliseconds that WaitForDebugEvent() will wait for a debug event.
        /// </summary>
        public uint DebugEventTimeout { get; set; }

        /// <summary>
        /// Gets or sets a collection of user-specified settings relating to this debugger instance's behavior.
        /// </summary>
        public DebuggerSettings Settings { get; set; }

        /// <summary>
        /// Gets the current instruction if debugging is paused. If debugging is not paused, returns IntPtr.Zero.
        /// </summary>
        private IntPtr CurrentInstruction
        {
            get
            {
                return new IntPtr((long)this.ts.Context._ip);
            }
        }

        #endregion

        #region Methods

        #region Start, Stop, Pause, and Resume Routines

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
        /// <remarks>TODO: fix this function. it does not properly start the target file.</remarks>
        /// <returns>Returns true if the process was successfully started in debug mode.</returns>
        public bool StartAndDebug(string filePath, bool pauseOnFirstInst)
        {
            return this.StartAndDebug(filePath, string.Empty, pauseOnFirstInst);
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
                // Unblock the thread, if a second chance exception has occured.
                this.ResumeDebugging();

                // Wait for the thread to terminate.
                while (this.debugThread.IsAlive && !this.threadMayExit)
                {
                }

                this.debugThread.Join();
            }

            return true;
        }

        /// <summary>
        /// Pauses the target process by breaking into it via DebugBreakProcess.
        /// </summary>
        /// <returns>true if the target was successfully paused</returns>
        public bool PauseDebugging()
        {
            this.IsDebuggingPaused = WinApi.DebugBreakProcess(this.Proc.Handle);
            return this.IsDebuggingPaused;
        }

        /// <summary>
        /// Resumes the debugger loop, if it has been paused.
        /// </summary>
        public void ResumeDebugging()
        {
            this.pauseDebuggerLock.Set();
        }

        #endregion

        #region Breakpoint Management

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

            IntPtr threadHandle;
            WinApi.CONTEXT cx;
            this.BeginEditThread((uint)this.ThreadID, out threadHandle, out cx);

            cx.ContextFlags = WinApi.CONTEXT_FLAGS.FULL;
#if WIN64
            cx.Dr0 = (uint)address.ToInt64();
#else
            cx.Dr0 = (uint)address.ToInt32();
#endif
            cx.Dr7 =
                (uint)(Debugger.DRegSettings.reg0w | Debugger.DRegSettings.reg0len4 | Debugger.DRegSettings.reg0set);

            this.EndEditThread((uint)this.ThreadID, ref threadHandle, ref cx);

            WinApi.CloseHandle(threadHandle);
            return true;
        }

        /// <summary>
        /// Sets an INT3 breakpoint at the specified address. Saves the old value at the address, so that when the
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
        /// Removes all hardware breakpoints from Dr0 through Dr3 registers.
        /// </summary>
        /// <returns>Returns true if all hardware breakpoints are successfully removed.</returns>
        public bool UnsetAllHardBPs()
        {
            if (!this.IsOpen)
            {
                return false;
            }

            IntPtr threadHandle;
            WinApi.CONTEXT cx;
            this.BeginEditThread((uint)this.ThreadID, out threadHandle, out cx);

            cx.ContextFlags = WinApi.CONTEXT_FLAGS.FULL;
            cx.Dr0 = 0x0;
            cx.Dr1 = 0x0;
            cx.Dr2 = 0x0;
            cx.Dr3 = 0x0;
            cx.Dr7 = 0x0;

            this.EndEditThread((uint)this.ThreadID, ref threadHandle, ref cx);

            WinApi.CloseHandle(threadHandle);
            return true;
        }

        /// <summary>
        /// Remove all INT3 breakpoints that are tracked by this debugger.
        /// </summary>
        /// <returns>Returns true if all INT3 breakpoints are successfully removed.</returns>
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
        /// Unsets the INT3 breakpoint, if it exists, at the specified address.
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

        #endregion

        #region Step Routines

        /// <summary>
        /// Executes the next instruction and then pauses the debugger. If the next instruction is a call instruction,
        /// this will pause on the first instruction of the called function.
        /// </summary>
        public void StepInto()
        {
            this.VerifyDebuggingIsPaused();
            this.EnableSingleStepMode();
            this.ResumeDebugging();
        }

        /// <summary>
        /// Executes the next instruction and then pauses the debugger. If the next instruction is a call instruction,
        /// this will pause on the instruction following the call instruction.
        /// </summary>
        public void StepOver()
        {
            if (!this.IsOpen)
            {
                throw new System.InvalidOperationException("No process is attached");
            }
            this.VerifyDebuggingIsPaused();
            this.stepOverOperationInProgress = true;
            Instruction inst = this.DisassembleInstruction(this.CurrentInstruction);
            if (inst.FlowType == Instruction.ControlFlow.Call)
            {
                IntPtr nextInstructionAddress = IntPtr.Add(this.CurrentInstruction, (int)inst.NumBytes);
                this.SetSoftBP(nextInstructionAddress);
                this.ResumeDebugging();
            }
            else
            {
                this.StepInto();
            }

            IntPtr threadHandle;
            WinApi.CONTEXT cx;
            this.BeginEditThread((uint)this.ThreadID, out threadHandle, out cx);

            IntPtr currentInst = new IntPtr((long)cx._ip);
            int currentInstSize = this.GetInstructionSize(currentInst);
            IntPtr nextInst = IntPtr.Add(currentInst, currentInstSize);

            cx.ContextFlags = WinApi.CONTEXT_FLAGS.FULL;
#if WIN64
            cx.Dr0 = (uint)nextInst.ToInt64();
#else
            cx.Dr0 = (uint)nextInst.ToInt32();
#endif
            cx.Dr7 =
                (uint)(Debugger.DRegSettings.reg0rw |
                       Debugger.DRegSettings.reg0len4 |
                       Debugger.DRegSettings.reg0set);


            this.EndEditThread((uint)this.ThreadID, ref threadHandle, ref cx);

            this.ResumeDebugging();

            WinApi.CloseHandle(threadHandle);
        }

        #endregion

        #region Thread Manipulation

        /// <summary>
        /// Suspend the thread and prepare the thread context to be modified.
        /// </summary>
        /// <param name="threadId">The ID of the thread to be modified.</param>
        /// <param name="threadHandle">A handle of the thread to be modified.</param>
        /// <param name="cx">The context of the thread to be modified.</param>
        protected void BeginEditThread(uint threadId, out IntPtr threadHandle, out WinApi.CONTEXT cx)
        {
            WinApi.ThreadAccess threadRights =
                WinApi.ThreadAccess.SET_CONTEXT |
                WinApi.ThreadAccess.GET_CONTEXT |
                WinApi.ThreadAccess.SUSPEND_RESUME;
            threadHandle = WinApi.OpenThread(threadRights, false, threadId);
            if (threadHandle == null || threadHandle.Equals(IntPtr.Zero))
            {
                string msg =
                    "Unable to obtain a thread handle for TID: " + threadId + ". Error: " +
                    Marshal.GetLastWin32Error();
                WinApi.CloseHandle(threadHandle);
                throw new InvalidOperationException(msg);
            }

            uint result = WinApi.SuspendThread(threadHandle);
            unchecked
            {
                if (result == (uint)(-1))
                {
                    string msg =
                        "Unable to suspend thread, TID: " + threadId + ". Error: " + Marshal.GetLastWin32Error();
                    WinApi.CloseHandle(threadHandle);
                    throw new InvalidOperationException(msg);
                }
            }

            WinApi.CONTEXT context = new WinApi.CONTEXT();
            context.ContextFlags = WinApi.CONTEXT_FLAGS.FULL;
            if (!WinApi.GetThreadContext(threadHandle, ref context))
            {
                string msg =
                    "Unable to get thread context, TID: " + threadId + ". Error: " + Marshal.GetLastWin32Error();
                WinApi.CloseHandle(threadHandle);
                throw new InvalidOperationException(msg);
            }

            cx = context;
        }

        /// <summary>
        /// Apply the thread context modification and resume the thread.
        /// </summary>
        /// <param name="threadId">The ID of the thread to be modified.</param>
        /// <param name="threadHandle">A handle of the thread to be modified.</param>
        /// <param name="cx">The context of the thread to be modified.</param>
        protected void EndEditThread(uint threadId, ref IntPtr threadHandle, ref WinApi.CONTEXT cx)
        {
            // TODO: get the most context data from the thread, if FULL cannot get the most.
            cx.ContextFlags = WinApi.CONTEXT_FLAGS.FULL;
            if (!WinApi.SetThreadContext(threadHandle, ref cx))
            {
                string msg =
                    "Unable to set thread context for TID: " + threadId + ". Error: " +
                    Marshal.GetLastWin32Error();
                WinApi.CloseHandle(threadHandle);
                throw new InvalidOperationException(msg);
            }

            uint res = WinApi.ResumeThread(threadHandle);
            unchecked
            {
                if (res == (uint)(-1))
                {
                    string msg =
                        "Unable to resume thread, TID: " + threadId + ". Error: " + Marshal.GetLastWin32Error();
                    WinApi.CloseHandle(threadHandle);
                    throw new InvalidOperationException(msg);
                }
            }
        }

        #endregion

        #region Verification Routines

        /// <summary>
        /// Verify that the target process is paused.
        /// </summary>
        protected void VerifyDebuggingIsPaused()
        {
            if (!this.IsDebuggingPaused)
            {
                throw new System.InvalidOperationException("The debugger is not paused");
            }
        }

        /// <summary>
        /// Verifies that a target process is open.
        /// </summary>
        protected void VerifyTargetIsOpen()
        {
            if (!this.IsOpen)
            {
                throw new InvalidOperationException("No target process is open");
            }
        }

        #endregion

        private void UpdateCurrentAddress()
        {
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
            return WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
        }

        /// <summary>
        /// Handles the EXCEPTION_ARRAY_BOUNDS_EXCEEDED debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnArrayBoundsExceededDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
        }

        /// <summary>
        /// Handles the EXCEPTION_BREAKPOINT debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnBreakpointDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (!this.InitialBreakpointHit)
            {
                this.InitialBreakpointHit = true;
                return WinApi.DbgCode.CONTINUE;
            }
            else
            {
                return WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
            }
        }

        /// <summary>
        /// Handles the EXCEPTION_DATATYPE_MISALIGNMENT debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnDatatypeMisalignmentDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
        }

        /// <summary>
        /// Handles the EXCEPTION_FLT_DENORMAL_OPERAND debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnFltDenormalOperandDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
        }

        /// <summary>
        /// Handles the EXCEPTION_FLT_DIVIDE_BY_ZERO debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnFltDivideByZeroDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
        }

        /// <summary>
        /// Handles the EXCEPTION_FLT_INEXACT_RESULT debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnFltInexactResultDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
        }

        /// <summary>
        /// Handles the EXCEPTION_FLT_INVALID_OPERATION debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnFltInvalidOperationDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
        }

        /// <summary>
        /// Handles the EXCEPTION_FLT_OVERFLOW debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnFltOverflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
        }

        /// <summary>
        /// Handles the EXCEPTION_FLT_STACK_CHECK debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnFltStackCheckDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
        }

        /// <summary>
        /// Handles the EXCEPTION_FLT_UNDERFLOW debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnFltUnderflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
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
            return WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
        }

        /// <summary>
        /// Handles the EXCEPTION_IN_PAGE_ERROR debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnInPageErrorDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
        }

        /// <summary>
        /// Handles the EXCEPTION_INT_DIVIDE_BY_ZERO debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnIntDivideByZeroDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
        }

        /// <summary>
        /// Handles the EXCEPTION_INT_OVERFLOW debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnIntOverflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
        }

        /// <summary>
        /// Handles the EXCEPTION_INVALID_DISPOSITION debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnInvalidDispositionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
        }

        /// <summary>
        /// Handles the EXCEPTION_NONCONTINUABLE_EXCEPTION debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnNoncontinuableExceptionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
        }

        /// <summary>
        /// Handles the EXCEPTION_PRIV_INSTRUCTION debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnPrivInstructionDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
        }

        /// <summary>
        /// Handles the EXCEPTION_SINGLE_STEP debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnSingleStepDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
        }

        /// <summary>
        /// Handles the EXCEPTION_STACK_OVERFLOW debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected virtual WinApi.DbgCode OnStackOverflowDebugException(ref WinApi.DEBUG_EVENT de)
        {
            return WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
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
            return WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
        }

        #endregion

        #region Auxiliary Routines

        /// <summary>
        /// Sets the instruction pointer of the main thread to the specified value.
        /// </summary>
        /// <param name="address">The address to which the instruction pointer should be set.</param>
        protected void SetIP(IntPtr address)
        {
            this.VerifyTargetIsOpen();
            this.VerifyDebuggingIsPaused();

            // TODO: Make this look nicer. This seems like a hack.
            WinApi.CONTEXT cx = this.ts.Context;
#if WIN64
            cx.Rip = (ulong)address.ToInt64();
#else
            cx.Eip = (uint)address.ToInt32();
#endif
            this.ts.Context = cx;
        }

        /// <summary>
        /// Enables single step mode for the current thread.
        /// </summary>
        protected void EnableSingleStepMode()
        {
            this.VerifyTargetIsOpen();
            this.VerifyDebuggingIsPaused();
            this.WaitForTargetStateInitialization();

            // TODO: Make this look nicer. This seems like a hack.
            WinApi.CONTEXT cx = this.ts.Context;
            cx.EFlags |= 0x100;
            this.ts.Context = cx;
        }

        /// <summary>
        /// Determines if the target process is 32-bit or 64-bit and sets the debugger architecture appropriately.
        /// </summary>
        private void SetDebuggerArchitecture()
        {
            bool isWow64;
            WinApi.IsWow64Process(this.ProcHandle, out isWow64);

            // Note: This does not take into account for PAE. No plans to support PAE currently exist.
            if (isWow64)
            {
                // For scanning purposes, Wow64 processes will be treated as as 32-bit processes.
                this.Is64Bit = false;
                this.d.TargetArchitecture = Bunseki.Disassembler.Architecture.x86_32;
            }
            else
            {
                // If it is not Wow64, then the process is natively running, so set it according to the OS
                // architecture.
                this.Is64Bit = SysInteractor.Is64Bit;
                this.d.TargetArchitecture =
                    this.Is64Bit ? Disassembler.Architecture.x86_64 : Disassembler.Architecture.x86_32;
            }
        }

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
                    this.allowedToDebug = true;
                    this.threadMayExit = false;
                }

                WinApi.ThreadAccess thread_rights =
                    WinApi.ThreadAccess.SET_CONTEXT | WinApi.ThreadAccess.GET_CONTEXT | WinApi.ThreadAccess.SUSPEND_RESUME;
                IntPtr threadHandle = WinApi.OpenThread(thread_rights, false, (uint)this.ThreadID);
                WinApi.CONTEXT cx = new WinApi.CONTEXT();
                cx.ContextFlags = WinApi.CONTEXT_FLAGS.DEBUG_REGISTERS;
                WinApi.GetThreadContext(threadHandle, ref cx);
                cx.ContextFlags = WinApi.CONTEXT_FLAGS.DEBUG_REGISTERS;
                cx.Dr7 =
                    (uint)(Debugger.DRegSettings.reg0w | Debugger.DRegSettings.reg0len4 | Debugger.DRegSettings.reg0set);
                bool stc = WinApi.SetThreadContext(threadHandle, ref cx);
                WinApi.CloseHandle(threadHandle);
                this.SetDebuggerArchitecture();
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
                    commandLine = '"' + filePath + "\" " + parameters;
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

                    if (!WinApi.DebugSetProcessKillOnExit(false))
                    {
#if DEBUG
                        this.Status.Log("Cannot exit cleanly in the future.", Logger.Level.MEDIUM);
#endif
                    }
                    else
                    {
                        this.allowedToDebug = true;
                        this.threadMayExit = false;
                    }

                    this.SetDebuggerArchitecture();
                    this.DebugLoop();

                    return;
                }

                this.Status.Log("Unable to open the target process.", Logger.Level.HIGH);
            }
        }

        /// <summary>
        /// Wait for the target state to be properly initialized.
        /// </summary>
        private void WaitForTargetStateInitialization()
        {
            while (!this.ts.IsReady)
            {
                Thread.Sleep(1);
            }
        }

        #endregion

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
                    catch (NullReferenceException)
                    {
                        // Let the .NET Framework calm down and then continue once its feathers are unruffled.
                        continue;
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

                if (!WinApi.WaitForDebugEvent(ref de, this.DebugEventTimeout))
                {
                    continue;
                }

                this.IsBusy = true;
                bool pauseDebugger = false;
                bool isFirstBreakpointPass = false;

                switch (de.dwDebugEventCode)
                {
                    case (uint)WinApi.DebugEventType.EXCEPTION_DEBUG_EVENT:
                        // Only ignore first chance exceptions after the first breakpoint has been hit.
                        if (this.InitialBreakpointHit)
                        {
                            if (this.IgnoreFirstChanceExceptions && de.Exception.dwFirstChance != 0)
                            {
                                continueStatus = WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
                                break;
                            }
                        }

                        switch (de.Exception.ExceptionRecord.ExceptionCode)
                        {
                            case (uint)WinApi.ExceptionType.STATUS_WX86_SINGLE_STEP:
                            case (uint)WinApi.ExceptionType.SINGLE_STEP:
                                continueStatus = this.OnSingleStepDebugException(ref de);
                                if (this.Settings.PauseOnSingleStep || this.stepOverOperationInProgress)
                                {
                                    pauseDebugger = true;
                                    this.stepOverOperationInProgress = false;
                                }

                                break;

                            case (uint)WinApi.ExceptionType.ACCESS_VIOLATION:
                                continueStatus = this.OnAccessViolationDebugException(ref de);
                                if (this.Settings.PauseOnAccessViolation)
                                {
                                    pauseDebugger = true;
                                }

                                break;

                            case (uint)WinApi.ExceptionType.ARRAY_BOUNDS_EXCEEDED:
                                continueStatus = this.OnArrayBoundsExceededDebugException(ref de);
                                if (this.Settings.PauseOnArrayBoundsExceeded)
                                {
                                    pauseDebugger = true;
                                }

                                break;

                            case (uint)WinApi.ExceptionType.STATUS_WX86_BREAKPOINT:
                            case (uint)WinApi.ExceptionType.BREAKPOINT:
                                bool initialBreakpointStart = this.InitialBreakpointHit;
                                continueStatus = this.OnBreakpointDebugException(ref de);
                                bool initialBreakpointEnd = this.InitialBreakpointHit;
                                if (initialBreakpointStart != initialBreakpointEnd)
                                {
                                    isFirstBreakpointPass = true;
                                }

                                if (this.Settings.PauseOnBreakpoint)
                                {
                                    pauseDebugger = true;
                                }

                                break;

                            case (uint)WinApi.ExceptionType.DATATYPE_MISALIGNMENT:
                                continueStatus = this.OnDatatypeMisalignmentDebugException(ref de);
                                if (this.Settings.PauseOnDatatypeMisalignment)
                                {
                                    pauseDebugger = true;
                                }

                                break;

                            case (uint)WinApi.ExceptionType.FLT_DENORMAL_OPERAND:
                                continueStatus = this.OnFltDenormalOperandDebugException(ref de);
                                if (this.Settings.PauseOnFltDenormalOperand)
                                {
                                    pauseDebugger = true;
                                }

                                break;

                            case (uint)WinApi.ExceptionType.FLT_DIVIDE_BY_ZERO:
                                continueStatus = this.OnFltDivideByZeroDebugException(ref de);
                                if (this.Settings.PauseOnFltDivideByZero)
                                {
                                    pauseDebugger = true;
                                }

                                break;

                            case (uint)WinApi.ExceptionType.FLT_INEXACT_RESULT:
                                continueStatus = this.OnFltInexactResultDebugException(ref de);
                                if (this.Settings.PauseOnFltInexactResult)
                                {
                                    pauseDebugger = true;
                                }

                                break;

                            case (uint)WinApi.ExceptionType.FLT_INVALID_OPERATION:
                                continueStatus = this.OnFltInvalidOperationDebugException(ref de);
                                if (this.Settings.PauseOnFltInvalidOperation)
                                {
                                    pauseDebugger = true;
                                }

                                break;

                            case (uint)WinApi.ExceptionType.FLT_OVERFLOW:
                                continueStatus = this.OnFltOverflowDebugException(ref de);
                                if (this.Settings.PauseOnFltOverflow)
                                {
                                    pauseDebugger = true;
                                }

                                break;

                            case (uint)WinApi.ExceptionType.FLT_STACK_CHECK:
                                continueStatus = this.OnFltStackCheckDebugException(ref de);
                                if (this.Settings.PauseOnFltStackCheck)
                                {
                                    pauseDebugger = true;
                                }

                                break;

                            case (uint)WinApi.ExceptionType.FLT_UNDERFLOW:
                                continueStatus = this.OnFltUnderflowDebugException(ref de);
                                if (this.Settings.PauseOnFltUnderflow)
                                {
                                    pauseDebugger = true;
                                }

                                break;

                            case (uint)WinApi.ExceptionType.GUARD_PAGE:
                                continueStatus = this.OnGuardPageDebugException(ref de);
                                if (this.Settings.PauseOnGuardPage)
                                {
                                    pauseDebugger = true;
                                }

                                break;

                            case (uint)WinApi.ExceptionType.ILLEGAL_INSTRUCTION:
                                continueStatus = this.OnIllegalInstructionDebugException(ref de);
                                if (this.Settings.PauseOnIllegalInstruction)
                                {
                                    pauseDebugger = true;
                                }

                                break;

                            case (uint)WinApi.ExceptionType.IN_PAGE_ERROR:
                                continueStatus = this.OnInPageErrorDebugException(ref de);
                                if (this.Settings.PauseOnInPageError)
                                {
                                    pauseDebugger = true;
                                }

                                break;

                            case (uint)WinApi.ExceptionType.INT_DIVIDE_BY_ZERO:
                                continueStatus = this.OnIntDivideByZeroDebugException(ref de);
                                if (this.Settings.PauseOnIntDivideByZero)
                                {
                                    pauseDebugger = true;
                                }

                                break;

                            case (uint)WinApi.ExceptionType.INT_OVERFLOW:
                                continueStatus = this.OnIntOverflowDebugException(ref de);
                                if (this.Settings.PauseOnIntOVerflow)
                                {
                                    pauseDebugger = true;
                                }

                                break;

                            case (uint)WinApi.ExceptionType.INVALID_DISPOSITION:
                                continueStatus = this.OnInvalidDispositionDebugException(ref de);
                                if (this.Settings.PauseOnInvalidDisposition)
                                {
                                    pauseDebugger = true;
                                }

                                break;

                            case (uint)WinApi.ExceptionType.NONCONTINUABLE_EXCEPTION:
                                continueStatus = this.OnNoncontinuableExceptionDebugException(ref de);
                                if (this.Settings.PauseOnNoncontinuableException)
                                {
                                    pauseDebugger = true;
                                }

                                break;

                            case (uint)WinApi.ExceptionType.PRIV_INSTRUCTION:
                                continueStatus = this.OnPrivInstructionDebugException(ref de);
                                if (this.Settings.PauseOnPrivInstruction)
                                {
                                    pauseDebugger = true;
                                }

                                break;

                            case (uint)WinApi.ExceptionType.STACK_OVERFLOW:
                                continueStatus = this.OnStackOverflowDebugException(ref de);
                                if (this.Settings.PauseOnStackOverflow)
                                {
                                    pauseDebugger = true;
                                }

                                break;

                            default:
                                continueStatus = this.OnUnhandledDebugException(ref de);
                                if (this.Settings.PauseOnUnhandledDebugException)
                                {
                                    pauseDebugger = true;
                                }

                                break;
                        }

                        // Only break on second exceptions once the first breakpoint has been encountered.
                        if (this.PauseOnSecondChanceException && !isFirstBreakpointPass)
                        {
                            pauseDebugger = true;
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
                        if (this.stepOverOperationInProgress)
                        {
                            this.stepOverOperationInProgress = false;
                        }

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
                        continueStatus = WinApi.DbgCode.EXCEPTION_NOT_HANDLED;
                        break;
                }

                if (pauseDebugger)
                {
                    // Reset the target state information.
                    this.ts.Reset();

                    // Get the thread context.
                    IntPtr threadHandle;
                    WinApi.CONTEXT context = new WinApi.CONTEXT();
                    this.BeginEditThread(de.dwThreadId, out threadHandle, out context);

                    // Save the thread state information.
                    this.ts.ThreadId = de.dwThreadId;
                    this.ts.ThreadHandle = threadHandle;
                    this.ts.Context = context;
                    this.ts.IsReady = true;

                    // Pause the debugger and target.
                    this.pauseDebuggerLock.Reset();
                    this.IsDebuggingPaused = true;
                    this.IsBusy = false;
                    this.pauseDebuggerLock.WaitOne();

                    // Save any changes that have been made to the thread context.
                    threadHandle = this.ts.ThreadHandle;
                    context = this.ts.Context;
                    this.EndEditThread(this.ts.ThreadId, ref threadHandle, ref context);

                    // Flag the debugger as unpaused.
                    this.IsDebuggingPaused = false;

                    // Delete any target state information.
                    this.ts.Reset();
                }

                this.IsBusy = false;
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

        /// <summary>
        /// The state of the paused target process that is being debugged.
        /// </summary>
        private class TargetState
        {
            /// <summary>
            /// Gets or sets the context of the currently paused thread.
            /// </summary>
            public WinApi.CONTEXT Context { get; set; }

            /// <summary>
            /// Gets or sets the thread ID of the currently paused thread.
            /// </summary>
            public uint ThreadId { get; set; }

            /// <summary>
            /// Gets or sets the thread handle for the paused thread.
            /// </summary>
            public IntPtr ThreadHandle { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether this TargetState has been properly initialized.
            /// </summary>
            public bool IsReady { get; set; }

            /// <summary>
            /// Resets the values of this TargetState object.
            /// </summary>
            public void Reset()
            {
                this.Context = new WinApi.CONTEXT();
                this.ThreadId = 0;
                this.ThreadHandle = IntPtr.Zero;
                this.IsReady = false;
            }
        }

        #endregion
    }
}
