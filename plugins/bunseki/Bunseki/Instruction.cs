namespace Bunseki
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Text;
    using BeaEngineCS;

    /// <summary>
    /// Represents a disassembled instruction.
    /// </summary>
    public class Instruction : INotifyPropertyChanged
    {
        #region Fields

        /// <summary>
        /// The string representation of the instruction.
        /// </summary>
        private string stringRepresentation = string.Empty;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the Instruction class, using a BeaEngine instruction.
        /// </summary>
        /// <param name="inst">the BeaEngine instruction</param>
        internal Instruction(BeaEngine._Disasm inst)
        {
            this.Address = (IntPtr)inst.VirtualAddr;
            this.Mnemonic = inst.Instruction.Mnemonic;
            this.stringRepresentation = inst.CompleteInstr;
            this.BranchTarget = (IntPtr)inst.Instruction.AddrValue;
            this.FlowType = Instruction.GetFlowControl(this.Mnemonic);
            this.NumBytes = (uint)inst.Length;
            this.Arg1 = new InstructionArgument(inst.Argument1);
            this.Arg2 = new InstructionArgument(inst.Argument2);
            this.Arg3 = new InstructionArgument(inst.Argument3);
        }

        /// <summary>
        /// Initializes a new instance of the Instruction class, using a Distorm instruction.
        /// </summary>
        /// <param name="inst">the Distorm instruction</param>
        internal Instruction(Distorm3cs.Distorm.DInst inst)
        {
            this.Address = (IntPtr)inst.addr;
            this.Mnemonic = inst.InstructionType.ToString().ToLower();

            throw new NotImplementedException();
        }

        /// <summary>
        /// Prevents a default instance of the Instruction class from being created.
        /// </summary>
        private Instruction()
        {
        }

        #endregion

        #region Events

        /// <summary>
        /// An event handler that handles when properties are changed.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Enumerations

        /// <summary>
        /// Flow control of execution.
        /// </summary>
        public enum ControlFlow : byte
        {
            /// <summary>
            /// Indicates the instruction is not a flow-control instruction.
            /// </summary>
            None = 0,

            /// <summary>
            /// Indicates the instruction is one of: CALL, CALL FAR.
            /// </summary>
            Call = 1,

            /// <summary>
            /// Indicates the instruction is one of: RET, IRET, RETF.
            /// </summary>
            Return,

            /// <summary>
            /// Indicates the instruction is one of: SYSCALL, SYSRET, SYSENTER, SYSEXIT.
            /// </summary>
            SysX,

            /// <summary>
            /// Indicates the instruction is one of: JMP, JMP FAR.
            /// </summary>
            UnconditionalBranch,

            /// <summary>
            /// Indicates the instruction is one of:
            /// JCXZ, JO, JNO, JB, JAE, JZ, JNZ, JBE, JA, JS, JNS, JP, JNP, JL, JGE, JLE, JG, LOOP, LOOPZ, LOOPNZ.
            /// </summary>
            ConditionalBranch,

            /// <summary>
            /// Indicates the instruction is one of: INT, INT1, INT 3, INTO, UD2.
            /// </summary>
            Interupt,

            /// <summary>
            /// Indicates the instruction is one of: CMOVxx.
            /// </summary>
            CMOVxx,
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the instruction of the instruction.
        /// </summary>
        public IntPtr Address { get; set; }

        /// <summary>
        /// Gets a string representation of the instruction.
        /// </summary>
        public string AddressAsString
        {
            get { return "0x" + this.Address.ToString("x").PadLeft(this.PointerWidth, '0'); }
        }

        /// <summary>
        /// Gets the mnemonic portion of the instruction.
        /// </summary>
        public string Mnemonic { get; private set; }

        /// <summary>
        /// Gets the target of the instruction if it branches elsewhere.
        /// </summary>
        public IntPtr BranchTarget { get; private set; }

        /// <summary>
        /// Gets the type of control flow for this instruction.
        /// </summary>
        public ControlFlow FlowType { get; private set; }

        /// <summary>
        /// Gets the number of bytes this instruction takes up.
        /// </summary>
        public uint NumBytes { get; private set; }

        /// <summary>
        /// Gets the first argument of the instruction.
        /// </summary>
        public InstructionArgument Arg1 { get; private set; }

        /// <summary>
        /// Gets the second argument of the instruction.
        /// </summary>
        public InstructionArgument Arg2 { get; private set; }

        /// <summary>
        /// Gets the third argument of the instruction.
        /// </summary>
        public InstructionArgument Arg3 { get; private set; }

        /// <summary>
        /// Gets a complete string representation of this instruction.
        /// </summary>
        public string CompleteInstruction
        {
            get
            {
                return this.ToString();
            }
        }

        /// <summary>
        /// Gets or sets the width of the pointer used by this instruction.
        /// </summary>
        public ushort PointerWidth { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Create an invalid instruction. Useful as a temporary instruction.
        /// </summary>
        /// <returns>an invalid instruction</returns>
        public static Instruction CreateInvalidInstruction()
        {
            Instruction inst = new Instruction();
            inst.Address = IntPtr.Subtract(IntPtr.Zero, 1);
            inst.Mnemonic = "invalid";
            inst.stringRepresentation = "invalid instruction";
            inst.BranchTarget = IntPtr.Zero;
            inst.FlowType = ControlFlow.None;
            inst.NumBytes = 0;
            inst.Arg1 = new InstructionArgument();
            inst.Arg2 = new InstructionArgument();
            inst.Arg3 = new InstructionArgument();
            return inst;
        }

        /// <summary>
        /// Create an invalid instruction at a specific address. Useful as a temporary instruction.
        /// </summary>
        /// <param name="address">the address of the invalid instruction</param>
        /// <returns>an invalid instruction</returns>
        public static Instruction CreateInvalidInstruction(IntPtr address)
        {
            Instruction inst = new Instruction();
            inst.Address = address;
            inst.Mnemonic = "invalid";
            inst.stringRepresentation = "invalid instruction";
            inst.BranchTarget = IntPtr.Zero;
            inst.FlowType = ControlFlow.None;
            inst.NumBytes = 0;
            inst.Arg1 = new InstructionArgument();
            inst.Arg2 = new InstructionArgument();
            inst.Arg3 = new InstructionArgument();
            return inst;
        }

        /// <summary>
        /// Convert the instruction to a string representation.
        /// </summary>
        /// <returns>a string representation of the instruction</returns>
        public override string ToString()
        {
            return this.stringRepresentation;
        }

        /// <summary>
        /// Handles the event when a property is changed.
        /// </summary>
        /// <param name="info">information about the property that was changed</param>
        protected void OnPropertyChanged(string info)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;

            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(info));
            }
        }

        /// <summary>
        /// Gets the flow control for a mnemonic.
        /// </summary>
        /// <param name="mnemonic">the mnemonic of interest</param>
        /// <returns>the control flow for the supplied mnemonic</returns>
        private static ControlFlow GetFlowControl(string mnemonic)
        {
            string mnemonicLowercase = mnemonic.ToLower();
            if (mnemonicLowercase.StartsWith("call"))
            {
                return ControlFlow.Call;
            }
            else if (mnemonicLowercase.StartsWith("jmp"))
            {
                return ControlFlow.UnconditionalBranch;
            }
            else if (mnemonicLowercase.StartsWith("j") || mnemonicLowercase.StartsWith("loop"))
            {
                return ControlFlow.ConditionalBranch;
            }
            else if (mnemonicLowercase.StartsWith("cmov"))
            {
                return ControlFlow.CMOVxx;
            }
            else if (mnemonicLowercase.StartsWith("sys"))
            {
                return ControlFlow.SysX;
            }
            else if (mnemonicLowercase.StartsWith("int") || mnemonicLowercase.Equals("ud2"))
            {
                return ControlFlow.Interupt;
            }
            else if (mnemonicLowercase.Contains("ret"))
            {
                // sysret will be handled by the "sys" check above.
                return ControlFlow.Return;
            }
            else
            {
                return ControlFlow.None;
            }
        }

        #endregion
    }
}
