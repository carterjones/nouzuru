﻿namespace Nouzuru
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Distorm3cs;

    /// <summary>
    /// A type of debugger that is used for monitoring the execution flow of a process.
    /// </summary>
    public class Profiler : Debugger
    {
        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether the initial breakpoint has been hit.
        /// </summary>
        public bool InitialBreakpointHit { get; protected set; }

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
                if (!this.SetSoftBP(new IntPtr((long)block.InstructionsDecomposed[0].addr)))
                {
                    Console.WriteLine("Error setting breakpoint at 0x" + block.InstructionsDecomposed[0].addr.ToString("x"));
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
            if (!this.InitialBreakpointHit)
            {
                this.InitialBreakpointHit = true;
            }
            else
            {
                this.Restore(de.Exception.ExceptionRecord.ExceptionAddress, false);
                this.SetIP(de.Exception.ExceptionRecord.ExceptionAddress);
                this.PrepareForSingleStep(de.Exception.ExceptionRecord.ExceptionAddress);
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
                this.Status.Log(
                    "dr0:" + cx.Dr0.ToString("X").PadLeft(16, '0') +
                    "dr1:" + cx.Dr1.ToString("X").PadLeft(16, '0') +
                    "dr2:" + cx.Dr2.ToString("X").PadLeft(16, '0') +
                    "dr3:" + cx.Dr3.ToString("X").PadLeft(16, '0') +
                    "dr6:" + cx.Dr6.ToString("X").PadLeft(16, '0') +
                    "dr7:" + cx.Dr7.ToString("X").PadLeft(16, '0'));
#else
                this.Status.Log(
                    "eax:" + cx.Eax.ToString("X").PadLeft(8, '0') +
                    "ebx:" + cx.Ebx.ToString("X").PadLeft(8, '0') +
                    "ecx:" + cx.Ecx.ToString("X").PadLeft(8, '0') +
                    "edx:" + cx.Edx.ToString("X").PadLeft(8, '0') +
                    "eip:" + cx.Eip.ToString("X").PadLeft(8, '0') +
                    "ebp:" + cx.Ebp.ToString("X").PadLeft(8, '0'));
                this.Status.Log(
                    "dr0:" + cx.Dr0.ToString("X").PadLeft(8, '0') +
                    "dr1:" + cx.Dr1.ToString("X").PadLeft(8, '0') +
                    "dr2:" + cx.Dr2.ToString("X").PadLeft(8, '0') +
                    "dr3:" + cx.Dr3.ToString("X").PadLeft(8, '0') +
                    "dr6:" + cx.Dr6.ToString("X").PadLeft(8, '0') +
                    "dr7:" + cx.Dr7.ToString("X").PadLeft(8, '0'));
#endif
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
