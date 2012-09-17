namespace Nouzuru
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Bunseki;

    /// <summary>
    /// A type of debugger that is used for monitoring the execution flow of a process.
    /// </summary>
    public class Profiler : DebugMon
    {
        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether breakpoint accesses will be logged.
        /// </summary>
        public bool LogBreakpointAccesses { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the values of common registers will be logged when a breakpoint
        /// is hit.
        /// </summary>
        public bool LogRegistersOnBreakpoint { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a value should be restored when a single step exception is hit.
        /// </summary>
        protected bool RestoreBreakpointOnExceptionSingleStep { get; set; }

        /// <summary>
        /// Gets or sets the address of a breakpoint that was just hit.
        /// </summary>
        protected IntPtr BreakpointAddressJustHit { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Sets a soft breakpoint in the target at the first instruction of the each basic block.
        /// </summary>
        /// <param name="blocks">The basic blocks of interest.</param>
        /// <returns>Returns true if the breakpoints were successfully set.</returns>
        public bool SetBreakpoints(IEnumerable<BasicBlock> blocks)
        {
            foreach (BasicBlock block in blocks)
            {
                if (!this.SetSoftBP(new IntPtr((long)block.Instructions[0].Address)))
                {
                    Console.WriteLine("Error setting breakpoint at 0x" + block.Instructions[0].Address.ToString("x"));
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Sets a soft breakpoint in the target starting at the supplied address for the specified number of
        /// instructions.
        /// </summary>
        /// <param name="startAddress">The first address at which a breakpoint will be set.</param>
        /// <param name="numInstructions">The number of instructions on which breakpoints will be set.</param>
        /// <returns>Returns true if the breakpoints were successfully set.</returns>
        public bool SetBreakpoints(IntPtr startAddress, uint numInstructions)
        {
            if (numInstructions == 0)
            {
                return true;
            }

            byte[] data = new byte[numInstructions*15];
            if (!this.Read(startAddress, data))
            {
                return false;
            }

            List<Instruction> insts = this.d.DisassembleInstructions(data, startAddress).ToList();
            if (insts.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < numInstructions && i < insts.Count; ++i)
            {
                if (!this.SetSoftBP(insts[i].Address))
                {
                    this.Status.Log(
                        "Error setting breakpoint at " +
                        this.IntPtrToFormattedAddress(new IntPtr((long)insts[i].Address)));
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Handles the EXCEPTION_BREAKPOINT debug exception.
        /// Causes the process to trigger a single step debug exception.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnBreakpointDebugException(ref WinApi.DEBUG_EVENT de)
        {
            if (this.InitialBreakpointHit)
            {
                this.Restore(de.Exception.ExceptionRecord.ExceptionAddress, false);
                //this.SetIP(de.Exception.ExceptionRecord.ExceptionAddress);
                //this.PrepareForSingleStep(de.Exception.ExceptionRecord.ExceptionAddress);
                this.BreakpointAddressJustHit = de.Exception.ExceptionRecord.ExceptionAddress;
                this.RestoreBreakpointOnExceptionSingleStep = true;
            }

            return base.OnBreakpointDebugException(ref de);
        }

        /// <summary>
        /// Handles the EXCEPTION_SINGLE_STEP debug exception.
        /// Logs information about the thread state when the exception is hit.
        /// </summary>
        /// <param name="de">The debug exception that was caught by the debugger.</param>
        /// <returns>Returns the continue debugging status code.</returns>
        protected override WinApi.DbgCode OnSingleStepDebugException(ref WinApi.DEBUG_EVENT de)
        {
            IntPtr threadHandle;
            WinApi.CONTEXT cx;
            this.BeginEditThread(de.dwThreadId, out threadHandle, out cx);
            if (this.LogRegistersOnBreakpoint)
            {
                this.LogRegisters(ref cx);
            }

            uint prevInstSize = this.GetPreviousInstructionSize(new IntPtr((long)cx._ip));
            IntPtr prevInstruction = new IntPtr((long)cx._ip - prevInstSize);
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

            return base.OnSingleStepDebugException(ref de);
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
            IntPtr beginBufAddress = new IntPtr(address.ToInt64() - 28);
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
            List<Instruction> insts = this.d.DisassembleInstructions(buffer, beginBufAddress).ToList();

            for (int i = 0; i < insts.Count; ++i)
            {
                if ((ulong)insts[i].Address.ToInt64() >= (ulong)address.ToInt64())
                {
                    return insts[i - 1].NumBytes;
                }
            }

            // Return nothing if a compatible instruction is not found.
            return 0;
        }

        #endregion
    }
}
