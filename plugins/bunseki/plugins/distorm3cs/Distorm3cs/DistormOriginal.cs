namespace Distorm3cs
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// The primary interface for calling distorm3 functions.
    /// </summary>
    public class DistormOriginal
    {
        #region Constants

        #region Instruction Flags

        /// <summary>
        /// Instruction could not be disassembled.
        /// </summary>
        public const ushort FLAG_NOT_DECODABLE = unchecked((ushort)-1);

        /// <summary>
        /// The instruction locks memory access.
        /// </summary>
        public const ushort FLAG_LOCK = (1 << 0);

        /// <summary>
        /// The instruction is prefixed with a REPNZ.
        /// </summary>
        public const ushort FLAG_REPNZ = (1 << 1);

        /// <summary>
        /// The instruction is prefixed with a REP, this can be a REPZ, it depends on the specific instruction.
        /// </summary>
        public const ushort FLAG_REP = (1 << 2);

        /// <summary>
        /// Indicates there is a hint taken for Jcc instructions only.
        /// </summary>
        public const ushort FLAG_HINT_TAKEN = (1 << 3);

        /// <summary>
        /// Indicates there is a hint non-taken for Jcc instructions only.
        /// </summary>
        public const ushort FLAG_HINT_NOT_TAKEN = (1 << 4);

        /// <summary>
        /// The Imm value is signed extended.
        /// </summary>
        public const ushort FLAG_IMM_SIGNED = (1 << 5);

        /// <summary>
        /// The destination operand is writable.
        /// </summary>
        public const ushort FLAG_DST_WR = (1 << 6);

        /// <summary>
        /// The instruction uses RIP-relative indirection.
        /// </summary>
        public const ushort FLAG_RIP_RELATIVE = (1 << 7);

        #endregion

        #region Register Bases

        public const byte REGS64 = 0;
        public const byte REGS32 = 16;
        public const byte REGS16 = 32;
        public const byte REGS8 = 48;
        public const byte REGS8_REX = 64;
        public const byte SREGS = 68;
        public const byte FPUREGS = 75;
        public const byte MMXREGS = 83;
        public const byte SSEREGS = 91;
        public const byte AVXREGS = 107;
        public const byte CREGS = 123;
        public const byte DREGS = 13;

        #endregion

        #region Register Masks

        /// <summary>
        /// AL, AH, AX, EAX, RAX
        /// </summary>
        public const uint RM_AX = 1;

        /// <summary>
        /// CL, CH, CX, ECX, RCX
        /// </summary>
        public const uint RM_CX = 2;

        /// <summary>
        /// DL, DH, DX, EDX, RDX
        /// </summary>
        public const uint RM_DX = 4;

        /// <summary>
        /// BL, BH, BX, EBX, RBX
        /// </summary>
        public const uint RM_BX = 8;

        /// <summary>
        /// SPL, SP, ESP, RSP
        /// </summary>
        public const uint RM_SP = 0x10;

        /// <summary>
        /// BPL, BP, EBP, RBP
        /// </summary>
        public const uint RM_BP = 0x20;

        /// <summary>
        /// SIL, SI, ESI, RSI
        /// </summary>
        public const uint RM_SI = 0x40;

        /// <summary>
        /// DIL, DI, EDI, RDI
        /// </summary>
        public const uint RM_DI = 0x80;

        /// <summary>
        /// ST(0) - ST(7)
        /// </summary>
        public const uint RM_FPU = 0x100;

        /// <summary>
        /// MM0 - MM7
        /// </summary>
        public const uint RM_MMX = 0x200;

        /// <summary>
        /// XMM0 - XMM15
        /// </summary>
        public const uint RM_SSE = 0x400;

        /// <summary>
        /// YMM0 - YMM15
        /// </summary>
        public const uint RM_AVX = 0x800;

        /// <summary>
        /// CR0, CR2, CR3, CR4, CR8
        /// </summary>
        public const uint RM_CR = 0x1000;

        /// <summary>
        /// DR0, DR1, DR2, DR3, DR6, DR7
        /// </summary>
        public const uint RM_DR = 0x2000;

        #endregion

        #region Instruction-Set-Class Types

        public const byte ISC_INTEGER = 1;
        public const byte ISC_FPU = 2;
        public const byte ISC_P6 = 3;
        public const byte ISC_MMX = 4;
        public const byte ISC_SSE = 5;
        public const byte ISC_SSE2 = 6;
        public const byte ISC_SSE3 = 7;
        public const byte ISC_SSSE3 = 8;
        public const byte ISC_SSE4_1 = 9;
        public const byte ISC_SSE4_2 = 10;
        public const byte ISC_SSE4_A = 11;
        public const byte ISC_3DNOW = 12;
        public const byte ISC_3DNOWEXT = 13;
        public const byte ISC_VMX = 14;
        public const byte ISC_SVM = 15;
        public const byte ISC_AVX = 16;
        public const byte ISC_FMA = 17;
        public const byte ISC_AES = 18;
        public const byte ISC_CLMUL = 19;

        #endregion

        #region Decompose Features

        /// <summary>
        /// No features should be used during decomposition.
        /// </summary>
        public const uint DF_NONE = 0;

        /// <summary>
        /// The decoder will limit addresses to a maximum of 16 bits.
        /// </summary>
        public const uint DF_MAXIMUM_ADDR16 = 1;

        /// <summary>
        /// The decoder will limit addresses to a maximum of 32 bits.
        /// </summary>
        public const uint DF_MAXIMUM_ADDR32 = 2;

        /// <summary>
        /// The decoder will return only flow control instructions (and filter the others internally).
        /// </summary>
        public const uint DF_RETURN_FC_ONLY = 4;

        /// <summary>
        /// The decoder will stop and return to the caller when the instruction 'CALL' (near and far) was decoded.
        /// </summary>
        public const uint DF_STOP_ON_CALL = 8;

        /// <summary>
        /// The decoder will stop and return to the caller when the instruction 'RET' (near and far) was decoded.
        /// </summary>
        public const uint DF_STOP_ON_RET = 0x10;

        /// <summary>
        /// The decoder will stop and return to the caller when the instruction system-call/ret was decoded.
        /// </summary>
        public const uint DF_STOP_ON_SYS = 0x20;

        /// <summary>
        /// The decoder will stop and return to the caller when any of the branch 'JMP', (near and far) instructions
        /// were decoded.
        /// </summary>
        public const uint DF_STOP_ON_UNC_BRANCH = 0x40;

        /// <summary>
        /// The decoder will stop and return to the caller when any of the conditional branch instruction were decoded.
        /// </summary>
        public const uint DF_STOP_ON_CND_BRANCH = 0x80;

        /// <summary>
        /// The decoder will stop and return to the caller when the instruction 'INT' (INT, INT1, INTO, INT 3) was
        /// decoded.
        /// </summary>
        public const uint DF_STOP_ON_INT = 0x100;

        /// <summary>
        /// The decoder will stop and return to the caller when any of the 'CMOVxx' instruction was decoded.
        /// </summary>
        public const uint DF_STOP_ON_CMOV = 0x200;

        /// <summary>
        /// The decoder will stop and return to the caller when any flow control instruction was decoded.
        /// </summary>
        public const uint DF_STOP_ON_FLOW_CONTROL = (DF_STOP_ON_CALL | DF_STOP_ON_RET | DF_STOP_ON_SYS |
                                                     DF_STOP_ON_UNC_BRANCH | DF_STOP_ON_CND_BRANCH | DF_STOP_ON_INT |
                                                     DF_STOP_ON_CMOV);

        #endregion

        #region Flow Control

        /// <summary>
        /// Indicates the instruction is not a flow-control instruction.
        /// </summary>
        public const byte FC_NONE = 0;

        /// <summary>
        /// Indicates the instruction is one of: CALL, CALL FAR.
        /// </summary>
        public const byte FC_CALL = 1;

        /// <summary>
        /// Indicates the instruction is one of: RET, IRET, RETF.
        /// </summary>
        public const byte FC_RET = 2;

        /// <summary>
        /// Indicates the instruction is one of: SYSCALL, SYSRET, SYSENTER, SYSEXIT.
        /// </summary>
        public const byte FC_SYS = 3;

        /// <summary>
        /// Indicates the instruction is one of: JMP, JMP FAR.
        /// </summary>
        public const byte FC_UNC_BRANCH = 4;

        /// <summary>
        /// Indicates the instruction is one of:
        /// JCXZ, JO, JNO, JB, JAE, JZ, JNZ, JBE, JA, JS, JNS, JP, JNP, JL, JGE, JLE, JG, LOOP, LOOPZ, LOOPNZ.
        /// </summary>
        public const byte FC_CND_BRANCH = 5;

        /// <summary>
        /// Indiciates the instruction is one of: INT, INT1, INT 3, INTO, UD2.
        /// </summary>
        public const byte FC_INT = 6;

        /// <summary>
        /// Indicates the instruction is one of: CMOVxx.
        /// </summary>
        public const byte FC_CMOV = 7;

        #endregion

        #region Miscellaneous constants

        /// <summary>
        /// No register was defined.
        /// </summary>
        public const byte R_NONE = unchecked((byte)-1);

        /// <summary>
        /// Up to four operands per instruction.
        /// </summary>
        public const byte OPERANDS_NO = 4;

        /// <summary>
        /// The maximum size of the p value of a _WString.
        /// </summary>
        public const int MAX_TEXT_SIZE = 48;

        /// <summary>
        /// The default value for the segment value of a _DInst structure.
        /// </summary>
        public const byte SEGMENT_DEFAULT = 0x80;

        /// <summary>
        /// No opcode ID is available.
        /// </summary>
        public const ushort OPCODE_ID_NONE = 0;

        #endregion

        #endregion

        #region Enumerations

        public enum _OperandType : byte
        {
            O_NONE,
            O_REG,
            O_IMM,
            O_IMM1,
            O_IMM2,
            O_DISP,
            O_SMEM,
            O_MEM,
            O_PC,
            O_PTR
        }

        public enum InstructionFlags : ushort
        {
            /// <summary>
            /// Instruction could not be disassembled.
            /// </summary>
            NOT_DECODABLE = DistormOriginal.FLAG_NOT_DECODABLE,

            /// <summary>
            /// The instruction locks memory access.
            /// </summary>
            LOCK = DistormOriginal.FLAG_LOCK,

            /// <summary>
            /// The instruction is prefixed with a REPNZ.
            /// </summary>
            REPNZ = DistormOriginal.FLAG_REPNZ,

            /// <summary>
            /// The instruction is prefixed with a REP, this can be a REPZ, it depends on the specific instruction.
            /// </summary>
            REP = DistormOriginal.FLAG_REP,

            /// <summary>
            /// Indicates there is a hint taken for Jcc instructions only.
            /// </summary>
            HINT_TAKEN = DistormOriginal.FLAG_HINT_TAKEN,

            /// <summary>
            /// Indicates there is a hint non-taken for Jcc instructions only.
            /// </summary>
            HINT_NOT_TAKEN = DistormOriginal.FLAG_HINT_NOT_TAKEN,

            /// <summary>
            /// The Imm value is signed extended.
            /// </summary>
            IMM_SIGNED = DistormOriginal.FLAG_IMM_SIGNED,

            /// <summary>
            /// The destination operand is writable.
            /// </summary>
            DST_WR = DistormOriginal.FLAG_DST_WR,

            /// <summary>
            /// The instruction uses RIP-relative indirection.
            /// </summary>
            RIP_RELATIVE = DistormOriginal.FLAG_RIP_RELATIVE
        }

        public enum RegisterBase : byte
        {
            REGS64 = DistormOriginal.REGS64,
            REGS32 = DistormOriginal.REGS32,
            REGS16 = DistormOriginal.REGS16,
            REGS8 = DistormOriginal.REGS8,
            REGS8_REX = DistormOriginal.REGS8_REX,
            SREGS = DistormOriginal.SREGS,
            FPUREGS = DistormOriginal.FPUREGS,
            MMXREGS = DistormOriginal.MMXREGS,
            SSEREGS = DistormOriginal.SSEREGS,
            AVXREGS = DistormOriginal.AVXREGS,
            CREGS = DistormOriginal.CREGS,
            DREGS = DistormOriginal.DREGS
        }

        /// <summary>
        /// Each mask indicates one of a register-class that is being used in some operand.
        /// </summary>
        public enum RegisterMask : uint
        {
            /// <summary>
            /// AL, AH, AX, EAX, RAX
            /// </summary>
            AX = DistormOriginal.RM_AX,

            /// <summary>
            /// CL, CH, CX, ECX, RCX
            /// </summary>
            CX = DistormOriginal.RM_CX,

            /// <summary>
            /// DL, DH, DX, EDX, RDX
            /// </summary>
            DX = DistormOriginal.RM_DX,

            /// <summary>
            /// BL, BH, BX, EBX, RBX
            /// </summary>
            BX = DistormOriginal.RM_BX,

            /// <summary>
            /// SPL, SP, ESP, RSP
            /// </summary>
            SP = DistormOriginal.RM_SP,

            /// <summary>
            /// BPL, BP, EBP, RBP
            /// </summary>
            BP = DistormOriginal.RM_BP,

            /// <summary>
            /// SIL, SI, ESI, RSI
            /// </summary>
            SI = DistormOriginal.RM_SI,

            /// <summary>
            /// DIL, DI, EDI, RDI
            /// </summary>
            DI = DistormOriginal.RM_DI,

            /// <summary>
            /// ST(0) - ST(7)
            /// </summary>
            FPU = DistormOriginal.RM_FPU,

            /// <summary>
            /// MM0 - MM7
            /// </summary>
            MMX = DistormOriginal.RM_MMX,

            /// <summary>
            /// XMM0 - XMM15
            /// </summary>
            SSE = DistormOriginal.RM_SSE,

            /// <summary>
            /// YMM0 - YMM15
            /// </summary>
            AVX = DistormOriginal.RM_AVX,

            /// <summary>
            /// CR0, CR2, CR3, CR4, CR8
            /// </summary>
            CR = DistormOriginal.RM_CR,

            /// <summary>
            /// DR0, DR1, DR2, DR3, DR6, DR7
            /// </summary>
            DR = DistormOriginal.RM_DR
        }

        public enum InstructionSetClass : byte
        {
            INTEGER = DistormOriginal.ISC_INTEGER,
            FPU = DistormOriginal.ISC_FPU,
            P6 = DistormOriginal.ISC_P6,
            MMX = DistormOriginal.ISC_MMX,
            SSE = DistormOriginal.ISC_SSE,
            SSE2 = DistormOriginal.ISC_SSE2,
            SSE3 = DistormOriginal.ISC_SSE3,
            SSSE3 = DistormOriginal.ISC_SSSE3,
            SSE4_1 = DistormOriginal.ISC_SSE4_1,
            SSE4_2 = DistormOriginal.ISC_SSE4_2,
            SSE4_A = DistormOriginal.ISC_SSE4_A,
            _3DNOW = DistormOriginal.ISC_3DNOW,       // Variables cannot start with a number, so an underscore preceeds it.
            _3DNOWEXT = DistormOriginal.ISC_3DNOWEXT, // Variables cannot start with a number, so an underscore preceeds it.
            VMX = DistormOriginal.ISC_VMX,
            SVM = DistormOriginal.ISC_SVM,
            AVX = DistormOriginal.ISC_AVX,
            FMA = DistormOriginal.ISC_FMA,
            AES = DistormOriginal.ISC_AES,
            CLMUL = DistormOriginal.ISC_CLMUL,
        }

        public enum DecomposeFeatures : uint
        {
            /// <summary>
            /// No features should be used during decomposition.
            /// </summary>
            NONE = DistormOriginal.DF_NONE,

            /// <summary>
            /// The decoder will limit addresses to a maximum of 16 bits.
            /// </summary>
            MAXIMUM_ADDR16 = DistormOriginal.DF_MAXIMUM_ADDR16,

            /// <summary>
            /// The decoder will limit addresses to a maximum of 32 bits.
            /// </summary>
            MAXIMUM_ADDR32 = DistormOriginal.DF_MAXIMUM_ADDR32,

            /// <summary>
            /// The decoder will return only flow control instructions (and filter the others internally).
            /// </summary>
            RETURN_FC_ONLY = DistormOriginal.DF_RETURN_FC_ONLY,

            /// <summary>
            /// The decoder will stop and return to the caller when the instruction 'CALL' (near and far) was decoded.
            /// </summary>
            STOP_ON_CALL = DistormOriginal.DF_STOP_ON_CALL,

            /// <summary>
            /// The decoder will stop and return to the caller when the instruction 'RET' (near and far) was decoded.
            /// </summary>
            STOP_ON_RET = DistormOriginal.DF_STOP_ON_RET,

            /// <summary>
            /// The decoder will stop and return to the caller when the instruction system-call/ret was decoded.
            /// </summary>
            STOP_ON_SYS = DistormOriginal.DF_STOP_ON_SYS,

            /// <summary>
            /// The decoder will stop and return to the caller when any of the branch 'JMP', (near and far) instructions
            /// were decoded.
            /// </summary>
            STOP_ON_UNC_BRANCH = DistormOriginal.DF_STOP_ON_UNC_BRANCH,

            /// <summary>
            /// The decoder will stop and return to the caller when any of the conditional branch instruction were decoded.
            /// </summary>
            STOP_ON_CND_BRANCH = DistormOriginal.DF_STOP_ON_CND_BRANCH,

            /// <summary>
            /// The decoder will stop and return to the caller when the instruction 'INT' (INT, INT1, INTO, INT 3) was
            /// decoded.
            /// </summary>
            STOP_ON_INT = DistormOriginal.DF_STOP_ON_INT,

            /// <summary>
            /// The decoder will stop and return to the caller when any of the 'CMOVxx' instruction was decoded.
            /// </summary>
            STOP_ON_CMOV = DistormOriginal.DF_STOP_ON_CMOV,

            /// <summary>
            /// The decoder will stop and return to the caller when any flow control instruction was decoded.
            /// </summary>
            STOP_ON_FLOW_CONTROL = DistormOriginal.DF_STOP_ON_FLOW_CONTROL
        }

        public enum FlowControl : byte
        {
            /// <summary>
            /// Indicates the instruction is not a flow-control instruction.
            /// </summary>
            NONE = DistormOriginal.FC_NONE,

            /// <summary>
            /// Indicates the instruction is one of: CALL, CALL FAR.
            /// </summary>
            CALL = DistormOriginal.FC_CALL,

            /// <summary>
            /// Indicates the instruction is one of: RET, IRET, RETF.
            /// </summary>
            RET = DistormOriginal.FC_RET,

            /// <summary>
            /// Indicates the instruction is one of: SYSCALL, SYSRET, SYSENTER, SYSEXIT.
            /// </summary>
            SYS = DistormOriginal.FC_SYS,

            /// <summary>
            /// Indicates the instruction is one of: JMP, JMP FAR.
            /// </summary>
            UNC_BRANCH = DistormOriginal.FC_UNC_BRANCH,

            /// <summary>
            /// Indicates the instruction is one of:
            /// JCXZ, JO, JNO, JB, JAE, JZ, JNZ, JBE, JA, JS, JNS, JP, JNP, JL, JGE, JLE, JG, LOOP, LOOPZ, LOOPNZ.
            /// </summary>
            CND_BRANCH = DistormOriginal.FC_CND_BRANCH,

            /// <summary>
            /// Indiciates the instruction is one of: INT, INT1, INT 3, INTO, UD2.
            /// </summary>
            INT = DistormOriginal.FC_INT,

            /// <summary>
            /// Indicates the instruction is one of: CMOVxx.
            /// </summary>
            CMOV = DistormOriginal.FC_CMOV
        }

        /// <summary>
        /// The three types of processor types that can be decoded.
        /// </summary>
        public enum _DecodeType
        {
            /// <summary>
            /// 16-bit decode type.
            /// </summary>
            Decode16Bits = 0,

            /// <summary>
            /// 32-bit decode type.
            /// </summary>
            Decode32Bits = 1,

            /// <summary>
            /// 64-bit decode type.
            /// </summary>
            Decode64Bits = 2
        }

        /// <summary>
        /// Return code of the decoding function.
        /// </summary>
        public enum _DecodeResult
        {
            /// <summary>
            /// Nothing was decoded.
            /// </summary>
            DECRES_NONE,

            /// <summary>
            /// The decoding was successful.
            /// </summary>
            DECRES_SUCCESS,

            /// <summary>
            /// There are not enough entries to use in the result array.
            /// </summary>
            DECRES_MEMORYERR,

            /// <summary>
            /// Input error (null code buffer, invalid decoding mode, etc...).
            /// </summary>
            DECRES_INPUTERR,

            /// <summary>
            /// The decode result was filtered.
            /// </summary>
            DECRES_FILTERED
        }

        #endregion

        #region Methods

        /// <summary>
        /// Get the Instruction-Set-Class type of the instruction.
        /// </summary>
        /// <param name="meta">The meta value from a _DInst structure.</param>
        /// <returns>
        /// Returns the Instruction-Set-Class type of the instruction.
        /// I.E: ISC_INTEGER, ISC_FPU, and many more.
        /// </returns>
        /// <remarks>This is the META_GET_ISC macro in distorm.h.</remarks>
        public static short MetaGetISC(byte meta)
        {
            return (short)((meta >> 3) & 0x1f);
        }

        /// <summary>
        /// Set the Instruction-Set-Class type of the instruction.
        /// </summary>
        /// <param name="di">The instruction that will have its meta value set.</param>
        /// <param name="isc">The Instruction-Set-Class type to set to the meta value.</param>
        public static void MetaSetISC(_DInst di, short isc)
        {
            di.meta |= (byte)(isc << 3);
        }

        /// <summary>
        /// Get the flow control flags of the instruction.
        /// </summary>
        /// <param name="meta">The meta flag of a _Dinst structure.</param>
        /// <returns>Returns the control flow flag value.</returns>
        public static byte MetaGetFC(byte meta)
        {
            return (byte)(meta & 0x7);
        }

        /// <summary>
        /// Get the target address of a branching instruction.
        /// </summary>
        /// <param name="di">A decomposed instruction, specifically some type of a branch instruction.</param>
        /// <returns>Returns the target address of the branch.</returns>
        /// <remarks>This is the INSTRUCTION_GET_TARGET macro in distorm.h</remarks>
        public static ulong InstructionGetTarget(_DInst di)
        {
            return di.addr + di.imm.addr + di.size;
        }

        /// <summary>
        /// Get the target address of a RIP-relative memory indirection.
        /// </summary>
        /// <param name="di">A decomposed instruction.</param>
        /// <returns>Returns the target address of a RIP-relative memory indirection.</returns>
        /// <remarks>This is the INSTRUCTION_GET_RIP_TARGET macro in distorm.h.</remarks>
        public static ulong InstructionGetRipTarget(_DInst di)
        {
            return di.addr + di.disp + di.size;
        }

        /// <summary>
        /// Sets the operand size in the flags value of an instruction.
        /// </summary>
        /// <param name="di">The instruction that will have its flags value modified.</param>
        /// <param name="size">The new size of the operand.</param>
        /// <remarks>This is the FLAG_SET_OPSIZE macro in distorm.h.</remarks>
        public static void FlagSetOpSize(_DInst di, byte size)
        {
            di.flags |= (ushort)((size & 3) << 8);
        }

        /// <summary>
        /// Sets the address size in the flags value of an instruction.
        /// </summary>
        /// <param name="di">The instruction that will have its flags value modified.</param>
        /// <param name="size">The new size of the address.</param>
        /// <remarks>This is the FLAG_SET_ADDRSIZE macro in distorm.h.</remarks>
        public static void FlagSetAddrSize(_DInst di, byte size)
        {
            di.flags |= (ushort)((size & 3) << 10);
        }

        /// <summary>
        /// Gets the operand size from the provided flags value.
        /// </summary>
        /// <param name="flags">The flags value that holds the operand size.</param>
        /// <returns>Returns the operand size: 0 - 16 bits / 1 - 32 bits / 2 - 64 bits / 3 reserved</returns>
        /// <remarks>This is the FLAG_GET_OPSIZE macro in distorm.h.</remarks>
        public static byte FlagGetOpSize(ushort flags)
        {
            return (byte)((flags >> 8) & 3);
        }

        /// <summary>
        /// Gets the address size from the provided flags value.
        /// </summary>
        /// <param name="flags">The flags value that holds the address size.</param>
        /// <returns>Returns the address size: 0 - 16 bits / 1 - 32 bits / 2 - 64 bits / 3 reserved</returns>
        /// <remarks>This is the FLAG_GET_ADDRSIZE macro in distorm.h.</remarks>
        public static byte FlagGetAddrSize(ushort flags)
        {
            return (byte)((flags >> 10) & 3);
        }

        /// <summary>
        /// Retrieves the prefix of an instruction, based on the provide flags value.
        /// </summary>
        /// <param name="flags">The flags value that holds the prefix of an instruction.</param>
        /// <returns>Returns the prefix of an instruction (FLAG_LOCK, FLAG_REPNZ, FLAG_REP).</returns>
        /// <remarks>This is the FLAG_GET_PREFIX macro in distorm.h.</remarks>
        public static byte FlagGetPrefix(ushort flags)
        {
            return (byte)(flags & 7);
        }

        /// <summary>
        /// Sets the segment value of an instruction.
        /// </summary>
        /// <param name="di">The instruction that will have its segment value set.</param>
        /// <param name="segment">The value to set which the instruction's segment value will be set.</param>
        /// <remarks>This is the SEGMENT_SET macro in distorm.h.</remarks>
        public static void SegmentSet(_DInst di, byte segment)
        {
            di.segment |= segment;
        }

        /// <summary>
        /// Gets the segment register index from a segment value.
        /// </summary>
        /// <param name="segment">A segment value, taken from a decomposed _DInst structure.</param>
        /// <returns>Returns segment register index.</returns>
        /// <remarks>This is the SEGMENT_GET macro in distorm.h.</remarks>
        public static byte SegmentGet(byte segment)
        {
            return segment == R_NONE ? R_NONE : (byte)(segment & 0x7f);
        }

        /// <summary>
        /// Determines if the segment value is set to the default segment value.
        /// </summary>
        /// <param name="segment">The segment value to test.</param>
        /// <returns>
        /// Returns true if the segment register is the default one for the operand. For instance:
        /// MOV [EBP], AL - the default segment register is SS. However,
        /// MOV [FS:EAX], AL - The default segment is DS, but we overrode it with FS,
        /// therefore the function will return FALSE.
        /// </returns>
        /// <remarks>This is the SEGMENT_IS_DEFAULT macro in distorm.h.</remarks>
        public static bool SegmentIsDefault(byte segment)
        {
            return (segment & SEGMENT_DEFAULT) == SEGMENT_DEFAULT;
        }

        /// <summary>
        /// Decomposes data into assembly format, using the native distorm_decompose function.
        /// </summary>
        /// <param name="ci">
        /// The _CodeInfo structure that holds the data that will be decomposed.
        /// </param>
        /// <param name="result">
        /// Array of type _DInst which will be used by this function in order to return the disassembled instructions.
        /// </param>
        /// <param name="maxInstructions">
        /// The maximum number of entries in the result array that you pass to this function, so it won't exceed its
        /// bound.
        /// </param>
        /// <param name="usedInstructionsCount">
        /// Number of the instruction that successfully were disassembled and written to the result array. Will hold
        /// the number of entries used in the result array and the result array itself will be filled with the
        /// disassembled instructions.
        /// </param>
        /// <returns>
        /// DECRES_SUCCESS on success (no more to disassemble), DECRES_INPUTERR on input error (null code buffer,
        /// invalid decoding mode, etc...), DECRES_MEMORYERR when there are not enough entries to use in the result
        /// array, BUT YOU STILL have to check for usedInstructionsCount!
        /// </returns>
        /// <remarks>
        /// Side-Effects: Even if the return code is DECRES_MEMORYERR, there might STILL be data in the array you
        ///               passed, this function will try to use as much entries as possible!
        /// Notes: 1) The minimal size of maxInstructions is 15.
        ///        2) You will have to synchronize the offset,code and length by yourself if you pass code fragments
        ///           and not a complete code block!
        /// </remarks>
        [DllImport("distorm3.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl,
#if USE_32_BIT_DECODING
            EntryPoint = "distorm_decompose32")]
#else
            EntryPoint = "distorm_decompose64")]
#endif
        public static extern _DecodeResult distorm_decompose(
            ref _CodeInfo ci, [In, Out] _DInst[] result, uint maxInstructions, ref uint usedInstructionsCount);

        /// <summary>
        /// Convert a _DInst structure, which was produced from the distorm_decompose function, into text.
        /// </summary>
        /// <param name="ci">The _CodeInfo structure that holds the data that was decomposed.</param>
        /// <param name="di">The decoded instruction.</param>
        /// <param name="result">The variable to which the formatted instruction will be returned.</param>
        [DllImport("distorm3.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl,
#if USE_32_BIT_DECODING
            EntryPoint = "distorm_format32")]
#else
            EntryPoint = "distorm_format64")]
#endif
        public static extern void distorm_format(ref _CodeInfo ci, ref _DInst di, ref _DecodedInst result);

        #endregion

        #region Structures

        /// <summary>
        /// A string representation used when returning a decoded instruction.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct _WString
        {
            /// <summary>
            /// The length of p.
            /// </summary>
            public uint length;

            /// <summary>
            /// A null terminated string.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_TEXT_SIZE)]
            public byte[] p;
        }

        /// <summary>
        /// Old decoded instruction structure in text format.
        /// Used only for backward compatibility with diStorm64.
        /// This structure holds all information the disassembler generates per instruction.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct _DecodedInst
        {
            /// <summary>
            /// Mnemonic of decoded instruction, prefixed if required by REP, LOCK etc.
            /// </summary>
            private _WString mnemonic;

            /// <summary>
            /// Operands of the decoded instruction, up to 3 operands, comma-seperated.
            /// </summary>
            private _WString operands;

            /// <summary>
            /// Hex dump - little endian, including prefixes.
            /// </summary>
            private _WString instructionHex;

            /// <summary>
            /// Size of decoded instruction.
            /// </summary>
            public uint size;

            /// <summary>
            /// Start offset of the decoded instruction.
            /// </summary>
            public ulong offset;

            /// <summary>
            /// Gets the mnemonic as a C# string.
            /// </summary>
            public string Mnemonic
            {
                get
                {
                    string longMnemonic = Encoding.UTF8.GetString(this.mnemonic.p);
                    return longMnemonic.Substring(0, (int)this.mnemonic.length).ToLower();
                }
            }

            /// <summary>
            /// Gets the operands as a C# string.
            /// </summary>
            public string Operands
            {
                get
                {
                    string longOperands = Encoding.UTF8.GetString(this.operands.p);
                    return longOperands.Substring(0, (int)this.operands.length).ToLower();
                }
            }

            /// <summary>
            /// Gets the instruction hex as a C# string.
            /// </summary>
            public string InstructionHex
            {
                get
                {
                    string longInstructionHex = Encoding.UTF8.GetString(this.instructionHex.p);
                    return longInstructionHex.Substring(0, (int)this.instructionHex.length);
                }
            }

            /// <summary>
            /// Returns this instruction in a simple format.
            /// </summary>
            /// <returns>Returns this instruction in the following format: "address: mnemonic operands"</returns>
            public override string ToString()
            {
                return this.offset.ToString("X").PadLeft(8, '0') + ": " + this.Mnemonic +
                       (this.Operands.Length > 0 ? " " + this.Operands : string.Empty);
            }
        }

        /// <summary>
        /// Represents an operand in an ASM instruction.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct _Operand
        {
            /// <summary>
            /// Type of operand:
            /// O_NONE: operand is to be ignored.
            /// O_REG: index holds global register index.
            /// O_IMM: instruction.imm.
            /// O_IMM1: instruction.imm.ex.i1.
            /// O_IMM2: instruction.imm.ex.i2.
            /// O_DISP: memory dereference with displacement only, instruction.disp.
            /// O_SMEM: simple memory dereference with optional displacement (a single register memory dereference).
            /// O_MEM: complex memory dereference (optional fields: s/i/b/disp).
            /// O_PC: the relative address of a branch instruction (instruction.imm.addr).
            /// O_PTR: the absolute target address of a far branch instruction (instruction.imm.ptr.seg/off).
            /// </summary>
            public _OperandType type;

            /// <summary>
            /// Index of:
            /// O_REG: holds global register index
            /// O_SMEM: holds the 'base' register. E.G: [ECX], [EBX+0x1234] are both in operand.index.
            /// O_MEM: holds the 'index' register. E.G: [EAX*4] is in operand.index.
            /// </summary>
            public byte index;

            /// <summary>
            /// Size of:
            /// O_REG: register
            /// O_IMM: instruction.imm
            /// O_IMM1: instruction.imm.ex.i1
            /// O_IMM2: instruction.imm.ex.i2
            /// O_DISP: instruction.disp
            /// O_SMEM: size of indirection.
            /// O_MEM: size of indirection.
            /// O_PC: size of the relative offset
            /// O_PTR: size of instruction.imm.ptr.off (16 or 32)
            /// </summary>
            public ushort size;
        }

        /// <summary>
        /// Used by O_PTR.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct _Value_ptr
        {
            /// <summary>
            /// The segment in which the value resides.
            /// </summary>
            public ushort seg;

            /// <summary>
            /// The offset from the segment in which the value resides. Can be 16 or 32 bits, size is in ops[n].size.
            /// </summary>
            public uint off;
        }

        /// <summary>
        /// Used by O_IMM1 (i1) and O_IMM2 (i2). ENTER instruction only.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct _Value_ex
        {
            /// <summary>
            /// The first immediate value.
            /// </summary>
            public uint i1;

            /// <summary>
            /// The second immediate value.
            /// </summary>
            public uint i2;
        }

        /// <summary>
        /// Represents a value within an instruction.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        public struct _Value
        {
            /// <summary>
            /// The value, as a signed 1-byte number.
            /// </summary>
            [FieldOffset(0)]
            public sbyte sbyte_;

            /// <summary>
            /// The value, as an unsigned 1-byte number.
            /// </summary>
            [FieldOffset(0)]
            public byte byte_;

            /// <summary>
            /// The value, as a signed 2-byte number.
            /// </summary>
            [FieldOffset(0)]
            public short sword;

            /// <summary>
            /// The value, as an unsigned 2-byte number.
            /// </summary>
            [FieldOffset(0)]
            public ushort word;

            /// <summary>
            /// The value, as a signed 4-byte number.
            /// </summary>
            [FieldOffset(0)]
            public int sdword;

            /// <summary>
            /// The value, as an unsigned 4-byte number.
            /// </summary>
            [FieldOffset(0)]
            public uint dword;

            /// <summary>
            /// The value, as a signed 8-byte number. All immediates are SIGN-EXTENDED to 64 bits!
            /// </summary>
            [FieldOffset(0)]
            public long sqword;

            /// <summary>
            /// The value, as an unsigned 8-byte number.
            /// </summary>
            [FieldOffset(0)]
            public ulong qword;

            /// <summary>
            /// The value, as an address. Used by O_PC: (Use GET_TARGET_ADDR).
            /// </summary>
            [FieldOffset(0)]
            public ulong addr;

            /// <summary>
            /// The value, as a pointer. Used by O_PTR.
            /// </summary>
            [FieldOffset(0)]
            public _Value_ptr ptr;

            /// <summary>
            /// Used by O_IMM1 (i1) and O_IMM2 (i2). ENTER instruction only.
            /// </summary>
            [FieldOffset(0)]
            public _Value_ex ex;
        }

        /// <summary>
        /// Represents the new decoded instruction, used by the decompose interface.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct _DInst
        {
            /// <summary>
            /// The immediate value of the instruction.
            /// Used by ops[n].type == O_IMM/O_IMM1&O_IMM2/O_PTR/O_PC. Its size is ops[n].size.
            /// </summary>
            public _Value imm;

            /// <summary>
            /// Used by ops[n].type == O_SMEM/O_MEM/O_DISP. Its size is dispSize.
            /// </summary>
            public ulong disp;

            /// <summary>
            /// Virtual address of first byte of instruction.
            /// </summary>
            public ulong addr;

            /// <summary>
            /// General flags of instruction, holds prefixes and more, if FLAG_NOT_DECODABLE, instruction is invalid.
            /// </summary>
            public ushort flags;

            /// <summary>
            /// Unused prefixes mask, for each bit that is set that prefix is not used (LSB is byte [addr + 0]).
            /// </summary>
            public ushort unusedPrefixesMask;

            /// <summary>
            /// Mask of registers that were used in the operands, only used for quick look up, in order to know *some*
            /// operand uses that register class.
            /// </summary>
            public ushort usedRegistersMask;

            /// <summary>
            /// ID of opcode in the global opcode table. Use for mnemonic look up.
            /// </summary>
            public ushort opcode;

            /// <summary>
            /// Up to four operands per instruction, ignored if ops[n].type == O_NONE.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.I4, SizeConst = OPERANDS_NO)]
            public _Operand[] ops;

            /// <summary>
            /// Size of the whole instruction.
            /// </summary>
            public byte size;

            /// <summary>
            /// Segment information of memory indirection, default segment, or overriden one, can be -1. Use SEGMENT
            /// macros.
            /// </summary>
            public byte segment;

            /// <summary>
            /// Used by ops[n].type == O_MEM. Base global register index (might be R_NONE), scale size (2/4/8), ignored
            /// for 0 or 1.
            /// </summary>
            public byte base_, scale;

            /// <summary>
            /// The size of the 'disp' field in bytes.
            /// </summary>
            public byte dispSize;

            /// <summary>
            /// Meta defines the instruction set class, and the flow control flags. Use META macros.
            /// </summary>
            public byte meta;
        }

        /// <summary>
        /// Holds various pieces of information that are required by the distorm_decompose function.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct _CodeInfo
        {
            /// <summary>
            /// The offset of the code.
            /// </summary>
            public ulong codeOffset;

            /// <summary>
            /// The next offset to be analyzed. nextOffset is OUT only.
            /// </summary>
            public ulong nextOffset;

            /// <summary>
            /// A pointer to unmanaged code that will be decomposed/disassembled.
            /// </summary>
            public IntPtr code;

            /// <summary>
            /// The length of the code that will be decomposed/disassembled.
            /// </summary>
            public int codeLen;

            /// <summary>
            /// The way this code should be decomposed/disassembled.
            /// </summary>
            public _DecodeType dt;

            /// <summary>
            /// Features that should be enabled during decomposition. Relevant flags begin with DF_.
            /// </summary>
            public uint features;
        }

        #endregion
    }
}
