namespace Distorm3cs
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.InteropServices;
    using System.Text;
    using Logger;

    /// <summary>
    /// A simple C# interface to the distorm library.
    /// </summary>
    public class Distorm
    {
        #region Constants

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
        /// The maximum size of the p value of a WString.
        /// </summary>
        public const int MAX_TEXT_SIZE = 48;

        /// <summary>
        /// The default value for the segment value of a Dinst structure.
        /// </summary>
        public const byte SEGMENT_DEFAULT = 0x80;

        /// <summary>
        /// No opcode ID is available.
        /// </summary>
        public const ushort OPCODE_ID_NONE = 0;

        #endregion

#if USE_32_BIT_DECODING
        /// <summary>
        /// A string representation of the target architecture.
        /// </summary>
        private const string ArchitectureString = "32";
#else
        /// <summary>
        /// A string representation of the target architecture.
        /// </summary>
        private const string ArchitectureString = "64";
#endif

        #endregion

        #region Enumerations

        /// <summary>
        /// Different types of operands.
        /// </summary>
        public enum OperandType : byte
        {
            /// <summary>
            /// No type is assigned.
            /// </summary>
            NONE,

            /// <summary>
            /// A register.
            /// </summary>
            REG,

            /// <summary>
            /// An immediate value.
            /// </summary>
            IMM,

            /// <summary>
            /// The first immediate value.
            /// </summary>
            IMM1,

            /// <summary>
            /// The second immediate value.
            /// </summary>
            IMM2,

            /// <summary>
            /// A displacement size.
            /// </summary>
            DISP,

            /// <summary>
            /// Simple memory.
            /// </summary>
            SMEM,

            /// <summary>
            /// Memory.
            /// </summary>
            MEM,

            /// <summary>
            /// Program counter.
            /// </summary>
            PC,

            /// <summary>
            /// A pointer.
            /// </summary>
            PTR
        }

        /// <summary>
        /// Various pieces of information about an instruction's capabilities.
        /// </summary>
        [Flags]
        public enum InstructionFlags : ushort
        {
            /// <summary>
            /// Instruction could not be disassembled.
            /// </summary>
            NOT_DECODABLE = unchecked((ushort)-1),

            /// <summary>
            /// The instruction locks memory access.
            /// </summary>
            LOCK = (1 << 0),

            /// <summary>
            /// The instruction is prefixed with a REPNZ.
            /// </summary>
            REPNZ = (1 << 1),

            /// <summary>
            /// The instruction is prefixed with a REP, this can be a REPZ, it depends on the specific instruction.
            /// </summary>
            REP = (1 << 2),

            /// <summary>
            /// Indicates there is a hint taken for Jcc instructions only.
            /// </summary>
            HINT_TAKEN = (1 << 3),

            /// <summary>
            /// Indicates there is a hint non-taken for Jcc instructions only.
            /// </summary>
            HINT_NOT_TAKEN = (1 << 4),

            /// <summary>
            /// The Imm value is signed extended.
            /// </summary>
            IMM_SIGNED = (1 << 5),

            /// <summary>
            /// The destination operand is writable.
            /// </summary>
            DST_WR = (1 << 6),

            /// <summary>
            /// The instruction uses RIP-relative indirection.
            /// </summary>
            RIP_RELATIVE = (1 << 7)
        }

        /// <summary>
        /// The size of the base register that is being analyzed.
        /// </summary>
        public enum RegisterBase : byte
        {
            /// <summary>
            /// 64-bit registers.
            /// </summary>
            REGS64 = 0,

            /// <summary>
            /// 32-bit registers.
            /// </summary>
            REGS32 = 16,

            /// <summary>
            /// 16 bit registers.
            /// </summary>
            REGS16 = 32,

            /// <summary>
            /// 8 bit registers.
            /// </summary>
            REGS8 = 48,

            /// <summary>
            /// 8 bit extended registers.
            /// </summary>
            REGS8_REX = 64,

            /// <summary>
            /// S registers.
            /// </summary>
            SREGS = 68,

            /// <summary>
            /// Floating point registers.
            /// </summary>
            FPUREGS = 75,

            /// <summary>
            /// MMX registers.
            /// </summary>
            MMXREGS = 83,

            /// <summary>
            /// Streaming SIMD Extensions registers.
            /// </summary>
            SSEREGS = 91,

            /// <summary>
            /// Advanced Vector Extensions registers.
            /// </summary>
            AVXREGS = 107,

            /// <summary>
            /// C registers.
            /// </summary>
            CREGS = 123,

            /// <summary>
            /// D registers.
            /// </summary>
            DREGS = 132,
        }

        /// <summary>
        /// Each mask indicates one of a register-class that is being used in some operand.
        /// </summary>
        [Flags]
        public enum RegisterMask : uint
        {
            /// <summary>
            /// AL, AH, AX, EAX, RAX
            /// </summary>
            AX = 1,

            /// <summary>
            /// CL, CH, CX, ECX, RCX
            /// </summary>
            CX = 2,

            /// <summary>
            /// DL, DH, DX, EDX, RDX
            /// </summary>
            DX = 4,

            /// <summary>
            /// BL, BH, BX, EBX, RBX
            /// </summary>
            BX = 8,

            /// <summary>
            /// SPL, SP, ESP, RSP
            /// </summary>
            SP = 0x10,

            /// <summary>
            /// BPL, BP, EBP, RBP
            /// </summary>
            BP = 0x20,

            /// <summary>
            /// SIL, SI, ESI, RSI
            /// </summary>
            SI = 0x40,

            /// <summary>
            /// DIL, DI, EDI, RDI
            /// </summary>
            DI = 0x80,

            /// <summary>
            /// ST(0) - ST(7)
            /// </summary>
            FPU = 0x100,

            /// <summary>
            /// MM0 - MM7
            /// </summary>
            MMX = 0x200,

            /// <summary>
            /// XMM0 - XMM15
            /// </summary>
            SSE = 0x400,

            /// <summary>
            /// YMM0 - YMM15
            /// </summary>
            AVX = 0x800,

            /// <summary>
            /// CR0, CR2, CR3, CR4, CR8
            /// </summary>
            CR = 0x1000,

            /// <summary>
            /// DR0, DR1, DR2, DR3, DR6, DR7
            /// </summary>
            DR = 0x2000
        }

        /// <summary>
        /// The class of an instruction.
        /// </summary>
        public enum InstructionSetClass : short
        {
            /// <summary>
            /// Integer instructions.
            /// </summary>
            INTEGER = 1,

            /// <summary>
            /// Floating point instructions.
            /// </summary>
            FPU = 2,

            /// <summary>
            /// P6 instructions.
            /// </summary>
            P6 = 3,

            /// <summary>
            /// MMX instructions.
            /// </summary>
            MMX = 4,

            /// <summary>
            /// SSE instructions.
            /// </summary>
            SSE = 5,

            /// <summary>
            /// SSE2 instructions.
            /// </summary>
            SSE2 = 6,

            /// <summary>
            /// SSE3 instructions.
            /// </summary>
            SSE3 = 7,

            /// <summary>
            /// SSSE3 instructions.
            /// </summary>
            SSSE3 = 8,

            /// <summary>
            /// SSE4_1 instructions.
            /// </summary>
            SSE4_1 = 9,

            /// <summary>
            /// SSE4_2 instructions.
            /// </summary>
            SSE4_2 = 10,

            /// <summary>
            /// SSE4_A instructions.
            /// </summary>
            SSE4_A = 11,

            /// <summary>
            /// 3DNow! instructions.
            /// </summary>
            _3DNOW = 12,    // Variables cannot start with a number, so an underscore preceeds it.

            /// <summary>
            /// Extended 3DNow! instructions.
            /// </summary>
            _3DNOWEXT = 13, // Variables cannot start with a number, so an underscore preceeds it.

            /// <summary>
            /// VMX instructions.
            /// </summary>
            VMX = 14,

            /// <summary>
            /// SVM instructions.
            /// </summary>
            SVM = 15,

            /// <summary>
            /// AVX instructions.
            /// </summary>
            AVX = 16,

            /// <summary>
            /// FMA instructions.
            /// </summary>
            FMA = 17,

            /// <summary>
            /// AES instructions.
            /// </summary>
            AES = 18,

            /// <summary>
            /// CMUL instructions.
            /// </summary>
            CLMUL = 19
        }

        /// <summary>
        /// Optional decomposition features.
        /// </summary>
        [Flags]
        public enum DecomposeFeatures : uint
        {
            /// <summary>
            /// No features should be used during decomposition.
            /// </summary>
            NONE = 0,

            /// <summary>
            /// The decoder will limit addresses to a maximum of 16 bits.
            /// </summary>
            MAXIMUM_ADDR16 = 1,

            /// <summary>
            /// The decoder will limit addresses to a maximum of 32 bits.
            /// </summary>
            MAXIMUM_ADDR32 = 2,

            /// <summary>
            /// The decoder will return only flow control instructions (and filter the others internally).
            /// </summary>
            RETURN_FC_ONLY = 4,

            /// <summary>
            /// The decoder will stop and return to the caller when the instruction 'CALL' (near and far) was decoded.
            /// </summary>
            STOP_ON_CALL = 8,

            /// <summary>
            /// The decoder will stop and return to the caller when the instruction 'RET' (near and far) was decoded.
            /// </summary>
            STOP_ON_RET = 0x10,

            /// <summary>
            /// The decoder will stop and return to the caller when the instruction system-call/ret was decoded.
            /// </summary>
            STOP_ON_SYS = 0x20,

            /// <summary>
            /// The decoder will stop and return to the caller when any of the branch 'JMP', (near and far) instructions
            /// were decoded.
            /// </summary>
            STOP_ON_UNC_BRANCH = 0x40,

            /// <summary>
            /// The decoder will stop and return to the caller when any of the conditional branch instruction were decoded.
            /// </summary>
            STOP_ON_CND_BRANCH = 0x80,

            /// <summary>
            /// The decoder will stop and return to the caller when the instruction 'INT' (INT, INT1, INTO, INT 3) was
            /// decoded.
            /// </summary>
            STOP_ON_INT = 0x100,

            /// <summary>
            /// The decoder will stop and return to the caller when any of the 'CMOVxx' instruction was decoded.
            /// </summary>
            STOP_ON_CMOV = 0x200,

            /// <summary>
            /// The decoder will stop and return to the caller when any flow control instruction was decoded.
            /// </summary>
            STOP_ON_FLOW_CONTROL = DecomposeFeatures.STOP_ON_CALL | DecomposeFeatures.STOP_ON_RET |
                                   DecomposeFeatures.STOP_ON_SYS | DecomposeFeatures.STOP_ON_UNC_BRANCH |
                                   DecomposeFeatures.STOP_ON_CND_BRANCH | DecomposeFeatures.STOP_ON_INT |
                                   DecomposeFeatures.STOP_ON_CMOV
        }

        /// <summary>
        /// Flow control of execution.
        /// </summary>
        public enum FlowControl : byte
        {
            /// <summary>
            /// Indicates the instruction is not a flow-control instruction.
            /// </summary>
            NONE = 0,

            /// <summary>
            /// Indicates the instruction is one of: CALL, CALL FAR.
            /// </summary>
            CALL = 1,

            /// <summary>
            /// Indicates the instruction is one of: RET, IRET, RETF.
            /// </summary>
            RET = 2,

            /// <summary>
            /// Indicates the instruction is one of: SYSCALL, SYSRET, SYSENTER, SYSEXIT.
            /// </summary>
            SYS = 3,

            /// <summary>
            /// Indicates the instruction is one of: JMP, JMP FAR.
            /// </summary>
            UNC_BRANCH = 4,

            /// <summary>
            /// Indicates the instruction is one of:
            /// JCXZ, JO, JNO, JB, JAE, JZ, JNZ, JBE, JA, JS, JNS, JP, JNP, JL, JGE, JLE, JG, LOOP, LOOPZ, LOOPNZ.
            /// </summary>
            CND_BRANCH = 5,

            /// <summary>
            /// Indicates the instruction is one of: INT, INT1, INT 3, INTO, UD2.
            /// </summary>
            INT = 6,

            /// <summary>
            /// Indicates the instruction is one of: CMOVxx.
            /// </summary>
            CMOV = 7
        }

        /// <summary>
        /// The three types of processor types that can be decoded.
        /// </summary>
        public enum DecodeType
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
        public enum DecodeResult
        {
            /// <summary>
            /// Nothing was decoded.
            /// </summary>
            NONE,

            /// <summary>
            /// The decoding was successful.
            /// </summary>
            SUCCESS,

            /// <summary>
            /// There are not enough entries to use in the result array.
            /// </summary>
            MEMORYERR,

            /// <summary>
            /// Input error (null code buffer, invalid decoding mode, etc...).
            /// </summary>
            INPUTERR,

            /// <summary>
            /// The decode result was filtered.
            /// </summary>
            FILTERED
        }

#pragma warning disable 1591
        /// <summary>
        /// Various types of instructions.
        /// </summary>
        [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1602:EnumerationItemsMustBeDocumented",
            Justification = "Self-explanatory variables.")]
        public enum InstructionType
        {
            UNDEFINED = 0, AAA = 66, AAD = 389, AAM = 384, AAS = 76, ADC = 31, ADD = 11, ADDPD = 3110,
            ADDPS = 3103, ADDSD = 3124, ADDSS = 3117, ADDSUBPD = 6394, ADDSUBPS = 6404,
            AESDEC = 9209, AESDECLAST = 9226, AESENC = 9167, AESENCLAST = 9184,
            AESIMC = 9150, AESKEYGENASSIST = 9795, AND = 41, ANDNPD = 3021, ANDNPS = 3013,
            ANDPD = 2990, ANDPS = 2983, ARPL = 111, BLENDPD = 9372, BLENDPS = 9353,
            BLENDVPD = 7619, BLENDVPS = 7609, BOUND = 104, BSF = 4346, BSR = 4358,
            BSWAP = 960, BT = 872, BTC = 934, BTR = 912, BTS = 887, CALL = 456,
            CALL_FAR = 260, CBW = 228, CDQ = 250, CDQE = 239, CLC = 492, CLD = 512,
            CLFLUSH = 4329, CLGI = 1833, CLI = 502, CLTS = 541, CMC = 487, CMOVA = 694,
            CMOVAE = 663, CMOVB = 656, CMOVBE = 686, CMOVG = 754, CMOVGE = 738,
            CMOVL = 731, CMOVLE = 746, CMOVNO = 648, CMOVNP = 723, CMOVNS = 708,
            CMOVNZ = 678, CMOVO = 641, CMOVP = 716, CMOVS = 701, CMOVZ = 671,
            CMP = 71, CMPEQPD = 4449, CMPEQPS = 4370, CMPEQSD = 4607, CMPEQSS = 4528,
            CMPLEPD = 4467, CMPLEPS = 4388, CMPLESD = 4625, CMPLESS = 4546, CMPLTPD = 4458,
            CMPLTPS = 4379, CMPLTSD = 4616, CMPLTSS = 4537, CMPNEQPD = 4488, CMPNEQPS = 4409,
            CMPNEQSD = 4646, CMPNEQSS = 4567, CMPNLEPD = 4508, CMPNLEPS = 4429,
            CMPNLESD = 4666, CMPNLESS = 4587, CMPNLTPD = 4498, CMPNLTPS = 4419,
            CMPNLTSD = 4656, CMPNLTSS = 4577, CMPORDPD = 4518, CMPORDPS = 4439,
            CMPORDSD = 4676, CMPORDSS = 4597, CMPS = 301, CMPUNORDPD = 4476, CMPUNORDPS = 4397,
            CMPUNORDSD = 4634, CMPUNORDSS = 4555, CMPXCHG = 898, CMPXCHG16B = 6373,
            CMPXCHG8B = 6362, COMISD = 2779, COMISS = 2771, CPUID = 865, CQO = 255,
            CRC32 = 9258, CVTDQ2PD = 6787, CVTDQ2PS = 3307, CVTPD2DQ = 6797, CVTPD2PI = 2681,
            CVTPD2PS = 3233, CVTPH2PS = 4161, CVTPI2PD = 2495, CVTPI2PS = 2485,
            CVTPS2DQ = 3317, CVTPS2PD = 3223, CVTPS2PH = 4171, CVTPS2PI = 2671,
            CVTSD2SI = 2701, CVTSD2SS = 3253, CVTSI2SD = 2515, CVTSI2SS = 2505,
            CVTSS2SD = 3243, CVTSS2SI = 2691, CVTTPD2DQ = 6776, CVTTPD2PI = 2614,
            CVTTPS2DQ = 3327, CVTTPS2PI = 2603, CVTTSD2SI = 2636, CVTTSS2SI = 2625,
            CWD = 245, CWDE = 233, DAA = 46, DAS = 56, DEC = 86, DIV = 1630,
            DIVPD = 3499, DIVPS = 3492, DIVSD = 3513, DIVSS = 3506, DPPD = 9615,
            DPPS = 9602, EMMS = 4100, ENTER = 340, EXTRACTPS = 9480, EXTRQ = 4136,
            F2XM1 = 1176, FABS = 1107, FADD = 1007, FADDP = 1533, FBLD = 1585,
            FBSTP = 1591, FCHS = 1101, FCLEX = 7289, FCMOVB = 1360, FCMOVBE = 1376,
            FCMOVE = 1368, FCMOVNB = 1429, FCMOVNBE = 1447, FCMOVNE = 1438, FCMOVNU = 1457,
            FCMOVU = 1385, FCOM = 1019, FCOMI = 1496, FCOMIP = 1607, FCOMP = 1025,
            FCOMPP = 1547, FCOS = 1295, FDECSTP = 1222, FDIV = 1045, FDIVP = 1578,
            FDIVR = 1051, FDIVRP = 1570, FEDISI = 1472, FEMMS = 574, FENI = 1466,
            FFREE = 1511, FIADD = 1301, FICOM = 1315, FICOMP = 1322, FIDIV = 1345,
            FIDIVR = 1352, FILD = 1402, FIMUL = 1308, FINCSTP = 1231, FINIT = 7304,
            FIST = 1416, FISTP = 1422, FISTTP = 1408, FISUB = 1330, FISUBR = 1337,
            FLD = 1058, FLD1 = 1125, FLDCW = 1082, FLDENV = 1074, FLDL2E = 1139,
            FLDL2T = 1131, FLDLG2 = 1154, FLDLN2 = 1162, FLDPI = 1147, FLDZ = 1170,
            FMUL = 1013, FMULP = 1540, FNCLEX = 7281, FNINIT = 7296, FNOP = 1095,
            FNSAVE = 7311, FNSTCW = 7266, FNSTENV = 7249, FNSTSW = 7326, FPATAN = 1197,
            FPREM = 1240, FPREM1 = 1214, FPTAN = 1190, FRNDINT = 1272, FRSTOR = 1503,
            FSAVE = 7319, FSCALE = 1281, FSETPM = 1480, FSIN = 1289, FSINCOS = 1263,
            FSQRT = 1256, FST = 1063, FSTCW = 7274, FSTENV = 7258, FSTP = 1068,
            FSTSW = 7334, FSUB = 1032, FSUBP = 1563, FSUBR = 1038, FSUBRP = 1555,
            FTST = 1113, FUCOM = 1518, FUCOMI = 1488, FUCOMIP = 1598, FUCOMP = 1525,
            FUCOMPP = 1393, FXAM = 1119, FXCH = 1089, FXRSTOR = 9892, FXRSTOR64 = 9901,
            FXSAVE = 9864, FXSAVE64 = 9872, FXTRACT = 1205, FYL2X = 1183, FYL2XP1 = 1247,
            GETSEC = 633, HADDPD = 4181, HADDPS = 4189, HLT = 482, HSUBPD = 4215,
            HSUBPS = 4223, IDIV = 1635, IMUL = 117, IN = 447, INC = 81, INS = 123,
            INSERTPS = 9547, INSERTQ = 4143, INT = 367, INT_3 = 360, INT1 = 476,
            INTO = 372, INVD = 555, INVEPT = 8284, INVLPG = 1711, INVLPGA = 1847,
            INVPCID = 8301, INVVPID = 8292, IRET = 378, JA = 166, JAE = 147,
            JB = 143, JBE = 161, JCXZ = 427, JECXZ = 433, JG = 202, JGE = 192,
            JL = 188, JLE = 197, JMP = 462, JMP_FAR = 467, JNO = 138, JNP = 183,
            JNS = 174, JNZ = 156, JO = 134, JP = 179, JRCXZ = 440, JS = 170,
            JZ = 152, LAHF = 289, LAR = 522, LDDQU = 6994, LDMXCSR = 9922, LDS = 335,
            LEA = 223, LEAVE = 347, LES = 330, LFENCE = 4265, LFS = 917, LGDT = 1687,
            LGS = 922, LIDT = 1693, LLDT = 1652, LMSW = 1705, LODS = 313, LOOP = 421,
            LOOPNZ = 406, LOOPZ = 414, LSL = 527, LSS = 907, LTR = 1658, LZCNT = 4363,
            MASKMOVDQU = 7119, MASKMOVQ = 7109, MAXPD = 3559, MAXPS = 3552, MAXSD = 3573,
            MAXSS = 3566, MFENCE = 4291, MINPD = 3439, MINPS = 3432, MINSD = 3453,
            MINSS = 3446, MONITOR = 1755, MOV = 218, MOVAPD = 2459, MOVAPS = 2451,
            MOVBE = 9251, MOVD = 3920, MOVDDUP = 2186, MOVDQ2Q = 6522, MOVDQA = 3946,
            MOVDQU = 3954, MOVHLPS = 2151, MOVHPD = 2345, MOVHPS = 2337, MOVLHPS = 2328,
            MOVLPD = 2168, MOVLPS = 2160, MOVMSKPD = 2815, MOVMSKPS = 2805, MOVNTDQ = 6849,
            MOVNTDQA = 7895, MOVNTI = 952, MOVNTPD = 2556, MOVNTPS = 2547, MOVNTQ = 6841,
            MOVNTSD = 2574, MOVNTSS = 2565, MOVQ = 3926, MOVQ2DQ = 6513, MOVS = 295,
            MOVSD = 2110, MOVSHDUP = 2353, MOVSLDUP = 2176, MOVSS = 2103, MOVSX = 939,
            MOVSXD = 10019, MOVUPD = 2095, MOVUPS = 2087, MOVZX = 927, MPSADBW = 9628,
            MUL = 1625, MULPD = 3170, MULPS = 3163, MULSD = 3184, MULSS = 3177,
            MWAIT = 1764, NEG = 1620, NOP = 581, NOT = 1615, OR = 27, ORPD = 3053,
            ORPS = 3047, OUT = 451, OUTS = 128, PABSB = 7688, PABSD = 7718, PABSW = 7703,
            PACKSSDW = 3849, PACKSSWB = 3681, PACKUSDW = 7916, PACKUSWB = 3759,
            PADDB = 7204, PADDD = 7234, PADDQ = 6481, PADDSB = 6930, PADDSW = 6947,
            PADDUSB = 6620, PADDUSW = 6639, PADDW = 7219, PALIGNR = 9410, PAND = 6607,
            PANDN = 6665, PAUSE = 10027, PAVGB = 6680, PAVGUSB = 2078, PAVGW = 6725,
            PBLENDVB = 7599, PBLENDW = 9391, PCLMULQDQ = 9647, PCMPEQB = 4043,
            PCMPEQD = 4081, PCMPEQQ = 7876, PCMPEQW = 4062, PCMPESTRI = 9726,
            PCMPESTRM = 9703, PCMPGTB = 3702, PCMPGTD = 3740, PCMPGTQ = 8087,
            PCMPGTW = 3721, PCMPISTRI = 9772, PCMPISTRM = 9749, PEXTRB = 9429,
            PEXTRD = 9446, PEXTRQ = 9454, PEXTRW = 6311, PF2ID = 1914, PF2IW = 1907,
            PFACC = 2028, PFADD = 1977, PFCMPEQ = 2035, PFCMPGE = 1938, PFCMPGT = 1984,
            PFMAX = 1993, PFMIN = 1947, PFMUL = 2044, PFNACC = 1921, PFPNACC = 1929,
            PFRCP = 1954, PFRCPIT1 = 2000, PFRCPIT2 = 2051, PFRSQIT1 = 2010, PFRSQRT = 1961,
            PFSUB = 1970, PFSUBR = 2020, PHADDD = 7375, PHADDSW = 7392, PHADDW = 7358,
            PHMINPOSUW = 8259, PHSUBD = 7451, PHSUBSW = 7468, PHSUBW = 7434, PI2FD = 1900,
            PI2FW = 1893, PINSRB = 9530, PINSRD = 9568, PINSRQ = 9576, PINSRW = 6294,
            PMADDUBSW = 7411, PMADDWD = 7073, PMAXSB = 8174, PMAXSD = 8191, PMAXSW = 6964,
            PMAXUB = 6648, PMAXUD = 8225, PMAXUW = 8208, PMINSB = 8106, PMINSD = 8123,
            PMINSW = 6902, PMINUB = 6590, PMINUD = 8157, PMINUW = 8140, PMOVMSKB = 6531,
            PMOVSXBD = 7754, PMOVSXBQ = 7775, PMOVSXBW = 7733, PMOVSXDQ = 7838,
            PMOVSXWD = 7796, PMOVSXWQ = 7817, PMOVZXBD = 7982, PMOVZXBQ = 8003,
            PMOVZXBW = 7961, PMOVZXDQ = 8066, PMOVZXWD = 8024, PMOVZXWQ = 8045,
            PMULDQ = 7859, PMULHRSW = 7538, PMULHRW = 2061, PMULHUW = 6740, PMULHW = 6759,
            PMULLD = 8242, PMULLW = 6496, PMULUDQ = 7054, POP = 22, POPA = 98,
            POPCNT = 4338, POPF = 277, POR = 6919, PREFETCH = 1872, PREFETCHNTA = 2402,
            PREFETCHT0 = 2415, PREFETCHT1 = 2427, PREFETCHT2 = 2439, PREFETCHW = 1882,
            PSADBW = 7092, PSHUFB = 7341, PSHUFD = 3988, PSHUFHW = 3996, PSHUFLW = 4005,
            PSHUFW = 3980, PSIGNB = 7487, PSIGND = 7521, PSIGNW = 7504, PSLLD = 7024,
            PSLLDQ = 9847, PSLLQ = 7039, PSLLW = 7009, PSRAD = 6710, PSRAW = 6695,
            PSRLD = 6451, PSRLDQ = 9830, PSRLQ = 6466, PSRLW = 6436, PSUBB = 7144,
            PSUBD = 7174, PSUBQ = 7189, PSUBSB = 6868, PSUBSW = 6885, PSUBUSB = 6552,
            PSUBUSW = 6571, PSUBW = 7159, PSWAPD = 2070, PTEST = 7629, PUNPCKHBW = 3780,
            PUNPCKHDQ = 3826, PUNPCKHQDQ = 3895, PUNPCKHWD = 3803, PUNPCKLBW = 3612,
            PUNPCKLDQ = 3658, PUNPCKLQDQ = 3870, PUNPCKLWD = 3635, PUSH = 16,
            PUSHA = 91, PUSHF = 270, PXOR = 6981, RCL = 977, RCPPS = 2953, RCPSS = 2960,
            RCR = 982, RDFSBASE = 9882, RDGSBASE = 9912, RDMSR = 600, RDPMC = 607,
            RDRAND = 9980, RDTSC = 593, RDTSCP = 1864, RET = 325, RETF = 354,
            ROL = 967, ROR = 972, ROUNDPD = 9296, ROUNDPS = 9277, ROUNDSD = 9334,
            ROUNDSS = 9315, RSM = 882, RSQRTPS = 2915, RSQRTSS = 2924, SAHF = 283,
            SAL = 997, SALC = 394, SAR = 1002, SBB = 36, SCAS = 319, SETA = 807,
            SETAE = 780, SETB = 774, SETBE = 800, SETG = 859, SETGE = 845, SETL = 839,
            SETLE = 852, SETNO = 767, SETNP = 832, SETNS = 819, SETNZ = 793,
            SETO = 761, SETP = 826, SETS = 813, SETZ = 787, SFENCE = 4321, SGDT = 1675,
            SHL = 987, SHLD = 876, SHR = 992, SHRD = 892, SHUFPD = 6336, SHUFPS = 6328,
            SIDT = 1681, SKINIT = 1839, SLDT = 1641, SMSW = 1699, SQRTPD = 2855,
            SQRTPS = 2847, SQRTSD = 2871, SQRTSS = 2863, STC = 497, STD = 517,
            STGI = 1827, STI = 507, STMXCSR = 9951, STOS = 307, STR = 1647, SUB = 51,
            SUBPD = 3379, SUBPS = 3372, SUBSD = 3393, SUBSS = 3386, SWAPGS = 1856,
            SYSCALL = 532, SYSENTER = 614, SYSEXIT = 624, SYSRET = 547, TEST = 206,
            TZCNT = 4351, UCOMISD = 2742, UCOMISS = 2733, UD2 = 569, UNPCKHPD = 2296,
            UNPCKHPS = 2286, UNPCKLPD = 2254, UNPCKLPS = 2244, VADDPD = 3139,
            VADDPS = 3131, VADDSD = 3155, VADDSS = 3147, VADDSUBPD = 6414, VADDSUBPS = 6425,
            VAESDEC = 9217, VAESDECLAST = 9238, VAESENC = 9175, VAESENCLAST = 9196,
            VAESIMC = 9158, VAESKEYGENASSIST = 9812, VANDNPD = 3038, VANDNPS = 3029,
            VANDPD = 3005, VANDPS = 2997, VBLENDPD = 9381, VBLENDPS = 9362, VBLENDVPD = 9681,
            VBLENDVPS = 9670, VBROADCASTF128 = 7672, VBROADCASTSD = 7658, VBROADCASTSS = 7644,
            VCMPEQPD = 5088, VCMPEQPS = 4686, VCMPEQSD = 5892, VCMPEQSS = 5490,
            VCMPEQ_OSPD = 5269, VCMPEQ_OSPS = 4867, VCMPEQ_OSSD = 6073, VCMPEQ_OSSS = 5671,
            VCMPEQ_UQPD = 5175, VCMPEQ_UQPS = 4773, VCMPEQ_UQSD = 5979, VCMPEQ_UQSS = 5577,
            VCMPEQ_USPD = 5378, VCMPEQ_USPS = 4976, VCMPEQ_USSD = 6182, VCMPEQ_USSS = 5780,
            VCMPFALSEPD = 5210, VCMPFALSEPS = 4808, VCMPFALSESD = 6014, VCMPFALSESS = 5612,
            VCMPFALSE_OSPD = 5419, VCMPFALSE_OSPS = 5017, VCMPFALSE_OSSD = 6223,
            VCMPFALSE_OSSS = 5821, VCMPGEPD = 5237, VCMPGEPS = 4835, VCMPGESD = 6041,
            VCMPGESS = 5639, VCMPGE_OQPD = 5449, VCMPGE_OQPS = 5047, VCMPGE_OQSD = 6253,
            VCMPGE_OQSS = 5851, VCMPGTPD = 5247, VCMPGTPS = 4845, VCMPGTSD = 6051,
            VCMPGTSS = 5649, VCMPGT_OQPD = 5462, VCMPGT_OQPS = 5060, VCMPGT_OQSD = 6266,
            VCMPGT_OQSS = 5864, VCMPLEPD = 5108, VCMPLEPS = 4706, VCMPLESD = 5912,
            VCMPLESS = 5510, VCMPLE_OQPD = 5295, VCMPLE_OQPS = 4893, VCMPLE_OQSD = 6099,
            VCMPLE_OQSS = 5697, VCMPLTPD = 5098, VCMPLTPS = 4696, VCMPLTSD = 5902,
            VCMPLTSS = 5500, VCMPLT_OQPD = 5282, VCMPLT_OQPS = 4880, VCMPLT_OQSD = 6086,
            VCMPLT_OQSS = 5684, VCMPNEQPD = 5131, VCMPNEQPS = 4729, VCMPNEQSD = 5935,
            VCMPNEQSS = 5533, VCMPNEQ_OQPD = 5223, VCMPNEQ_OQPS = 4821, VCMPNEQ_OQSD = 6027,
            VCMPNEQ_OQSS = 5625, VCMPNEQ_OSPD = 5435, VCMPNEQ_OSPS = 5033, VCMPNEQ_OSSD = 6239,
            VCMPNEQ_OSSS = 5837, VCMPNEQ_USPD = 5323, VCMPNEQ_USPS = 4921, VCMPNEQ_USSD = 6127,
            VCMPNEQ_USSS = 5725, VCMPNGEPD = 5188, VCMPNGEPS = 4786, VCMPNGESD = 5992,
            VCMPNGESS = 5590, VCMPNGE_UQPD = 5391, VCMPNGE_UQPS = 4989, VCMPNGE_UQSD = 6195,
            VCMPNGE_UQSS = 5793, VCMPNGTPD = 5199, VCMPNGTPS = 4797, VCMPNGTSD = 6003,
            VCMPNGTSS = 5601, VCMPNGT_UQPD = 5405, VCMPNGT_UQPS = 5003, VCMPNGT_UQSD = 6209,
            VCMPNGT_UQSS = 5807, VCMPNLEPD = 5153, VCMPNLEPS = 4751, VCMPNLESD = 5957,
            VCMPNLESS = 5555, VCMPNLE_UQPD = 5351, VCMPNLE_UQPS = 4949, VCMPNLE_UQSD = 6155,
            VCMPNLE_UQSS = 5753, VCMPNLTPD = 5142, VCMPNLTPS = 4740, VCMPNLTSD = 5946,
            VCMPNLTSS = 5544, VCMPNLT_UQPD = 5337, VCMPNLT_UQPS = 4935, VCMPNLT_UQSD = 6141,
            VCMPNLT_UQSS = 5739, VCMPORDPD = 5164, VCMPORDPS = 4762, VCMPORDSD = 5968,
            VCMPORDSS = 5566, VCMPORD_SPD = 5365, VCMPORD_SPS = 4963, VCMPORD_SSD = 6169,
            VCMPORD_SSS = 5767, VCMPTRUEPD = 5257, VCMPTRUEPS = 4855, VCMPTRUESD = 6061,
            VCMPTRUESS = 5659, VCMPTRUE_USPD = 5475, VCMPTRUE_USPS = 5073, VCMPTRUE_USSD = 6279,
            VCMPTRUE_USSS = 5877, VCMPUNORDPD = 5118, VCMPUNORDPS = 4716, VCMPUNORDSD = 5922,
            VCMPUNORDSS = 5520, VCMPUNORD_SPD = 5308, VCMPUNORD_SPS = 4906, VCMPUNORD_SSD = 6112,
            VCMPUNORD_SSS = 5710, VCOMISD = 2796, VCOMISS = 2787, VCVTDQ2PD = 6819,
            VCVTDQ2PS = 3338, VCVTPD2DQ = 6830, VCVTPD2PS = 3274, VCVTPS2DQ = 3349,
            VCVTPS2PD = 3263, VCVTSD2SI = 2722, VCVTSD2SS = 3296, VCVTSI2SD = 2536,
            VCVTSI2SS = 2525, VCVTSS2SD = 3285, VCVTSS2SI = 2711, VCVTTPD2DQ = 6807,
            VCVTTPS2DQ = 3360, VCVTTSD2SI = 2659, VCVTTSS2SI = 2647, VDIVPD = 3528,
            VDIVPS = 3520, VDIVSD = 3544, VDIVSS = 3536, VDPPD = 9621, VDPPS = 9608,
            VERR = 1663, VERW = 1669, VEXTRACTF128 = 9516, VEXTRACTPS = 9491,
            VFMADD132PD = 8387, VFMADD132PS = 8374, VFMADD132SD = 8413, VFMADD132SS = 8400,
            VFMADD213PD = 8667, VFMADD213PS = 8654, VFMADD213SD = 8693, VFMADD213SS = 8680,
            VFMADD231PD = 8947, VFMADD231PS = 8934, VFMADD231SD = 8973, VFMADD231SS = 8960,
            VFMADDSUB132PD = 8326, VFMADDSUB132PS = 8310, VFMADDSUB213PD = 8606,
            VFMADDSUB213PS = 8590, VFMADDSUB231PD = 8886, VFMADDSUB231PS = 8870,
            VFMSUB132PD = 8439, VFMSUB132PS = 8426, VFMSUB132SD = 8465, VFMSUB132SS = 8452,
            VFMSUB213PD = 8719, VFMSUB213PS = 8706, VFMSUB213SD = 8745, VFMSUB213SS = 8732,
            VFMSUB231PD = 8999, VFMSUB231PS = 8986, VFMSUB231SD = 9025, VFMSUB231SS = 9012,
            VFMSUBADD132PD = 8358, VFMSUBADD132PS = 8342, VFMSUBADD213PD = 8638,
            VFMSUBADD213PS = 8622, VFMSUBADD231PD = 8918, VFMSUBADD231PS = 8902,
            VFNMADD132PD = 8492, VFNMADD132PS = 8478, VFNMADD132SD = 8520, VFNMADD132SS = 8506,
            VFNMADD213PD = 8772, VFNMADD213PS = 8758, VFNMADD213SD = 8800, VFNMADD213SS = 8786,
            VFNMADD231PD = 9052, VFNMADD231PS = 9038, VFNMADD231SD = 9080, VFNMADD231SS = 9066,
            VFNMSUB132PD = 8548, VFNMSUB132PS = 8534, VFNMSUB132SD = 8576, VFNMSUB132SS = 8562,
            VFNMSUB213PD = 8828, VFNMSUB213PS = 8814, VFNMSUB213SD = 8856, VFNMSUB213SS = 8842,
            VFNMSUB231PD = 9108, VFNMSUB231PS = 9094, VFNMSUB231SD = 9136, VFNMSUB231SS = 9122,
            VHADDPD = 4197, VHADDPS = 4206, VHSUBPD = 4231, VHSUBPS = 4240, VINSERTF128 = 9503,
            VINSERTPS = 9557, VLDDQU = 7001, VLDMXCSR = 9941, VMASKMOVDQU = 7131,
            VMASKMOVPD = 7949, VMASKMOVPS = 7937, VMAXPD = 3588, VMAXPS = 3580,
            VMAXSD = 3604, VMAXSS = 3596, VMCALL = 1719, VMCLEAR = 9997, VMFUNC = 1787,
            VMINPD = 3468, VMINPS = 3460, VMINSD = 3484, VMINSS = 3476, VMLAUNCH = 1727,
            VMLOAD = 1811, VMMCALL = 1802, VMOVAPD = 2476, VMOVAPS = 2467, VMOVD = 3932,
            VMOVDDUP = 2234, VMOVDQA = 3962, VMOVDQU = 3971, VMOVHLPS = 2195,
            VMOVHPD = 2382, VMOVHPS = 2373, VMOVLHPS = 2363, VMOVLPD = 2214, VMOVLPS = 2205,
            VMOVMSKPD = 2836, VMOVMSKPS = 2825, VMOVNTDQ = 6858, VMOVNTDQA = 7905,
            VMOVNTPD = 2593, VMOVNTPS = 2583, VMOVQ = 3939, VMOVSD = 2143, VMOVSHDUP = 2391,
            VMOVSLDUP = 2223, VMOVSS = 2135, VMOVUPD = 2126, VMOVUPS = 2117, VMPSADBW = 9637,
            VMPTRLD = 9988, VMPTRST = 6385, VMREAD = 4128, VMRESUME = 1737, VMRUN = 1795,
            VMSAVE = 1819, VMULPD = 3199, VMULPS = 3191, VMULSD = 3215, VMULSS = 3207,
            VMWRITE = 4152, VMXOFF = 1747, VMXON = 10006, VORPD = 3066, VORPS = 3059,
            VPABSB = 7695, VPABSD = 7725, VPABSW = 7710, VPACKSSDW = 3859, VPACKSSWB = 3691,
            VPACKUSDW = 7926, VPACKUSWB = 3769, VPADDB = 7211, VPADDD = 7241,
            VPADDQ = 6488, VPADDSB = 6938, VPADDSW = 6955, VPADDUSW = 6629, VPADDW = 7226,
            VPALIGNR = 9419, VPAND = 6613, VPANDN = 6672, VPAVGB = 6687, VPAVGW = 6732,
            VPBLENDVB = 9692, VPBLENDW = 9400, VPCLMULQDQ = 9658, VPCMPEQB = 4052,
            VPCMPEQD = 4090, VPCMPEQQ = 7885, VPCMPEQW = 4071, VPCMPESTRI = 9737,
            VPCMPESTRM = 9714, VPCMPGTB = 3711, VPCMPGTD = 3749, VPCMPGTQ = 8096,
            VPCMPGTW = 3730, VPCMPISTRI = 9783, VPCMPISTRM = 9760, VPERM2F128 = 9265,
            VPERMILPD = 7570, VPERMILPS = 7559, VPEXTRB = 9437, VPEXTRD = 9462,
            VPEXTRQ = 9471, VPEXTRW = 6319, VPHADDD = 7383, VPHADDSW = 7401, VPHADDW = 7366,
            VPHMINPOSUW = 8271, VPHSUBD = 7459, VPHSUBSW = 7477, VPHSUBW = 7442,
            VPINSRB = 9538, VPINSRD = 9584, VPINSRQ = 9593, VPINSRW = 6302, VPMADDUBSW = 7422,
            VPMADDWD = 7082, VPMAXSB = 8182, VPMAXSD = 8199, VPMAXSW = 6972, VPMAXUB = 6656,
            VPMAXUD = 8233, VPMAXUW = 8216, VPMINSB = 8114, VPMINSD = 8131, VPMINSW = 6910,
            VPMINUB = 6598, VPMINUD = 8165, VPMINUW = 8148, VPMOVMSKB = 6541,
            VPMOVSXBD = 7764, VPMOVSXBQ = 7785, VPMOVSXBW = 7743, VPMOVSXDQ = 7848,
            VPMOVSXWD = 7806, VPMOVSXWQ = 7827, VPMOVZXBD = 7992, VPMOVZXBQ = 8013,
            VPMOVZXBW = 7971, VPMOVZXDQ = 8076, VPMOVZXWD = 8034, VPMOVZXWQ = 8055,
            VPMULDQ = 7867, VPMULHRSW = 7548, VPMULHUW = 6749, VPMULHW = 6767,
            VPMULLD = 8250, VPMULLW = 6504, VPMULUDQ = 7063, VPOR = 6924, VPSADBW = 7100,
            VPSHUFB = 7349, VPSHUFD = 4014, VPSHUFHW = 4023, VPSHUFLW = 4033,
            VPSIGNB = 7495, VPSIGND = 7529, VPSIGNW = 7512, VPSLLD = 7031, VPSLLDQ = 9855,
            VPSLLQ = 7046, VPSLLW = 7016, VPSRAD = 6717, VPSRAW = 6702, VPSRLD = 6458,
            VPSRLDQ = 9838, VPSRLQ = 6473, VPSRLW = 6443, VPSUBB = 7151, VPSUBD = 7181,
            VPSUBQ = 7196, VPSUBSB = 6876, VPSUBSW = 6893, VPSUBUSB = 6561, VPSUBUSW = 6580,
            VPSUBW = 7166, VPTEST = 7636, VPUNPCKHBW = 3791, VPUNPCKHDQ = 3837,
            VPUNPCKHQDQ = 3907, VPUNPCKHWD = 3814, VPUNPCKLBW = 3623, VPUNPCKLDQ = 3669,
            VPUNPCKLQDQ = 3882, VPUNPCKLWD = 3646, VPXOR = 6987, VRCPPS = 2967,
            VRCPSS = 2975, VROUNDPD = 9305, VROUNDPS = 9286, VROUNDSD = 9343,
            VROUNDSS = 9324, VRSQRTPS = 2933, VRSQRTSS = 2943, VSHUFPD = 6353,
            VSHUFPS = 6344, VSQRTPD = 2888, VSQRTPS = 2879, VSQRTSD = 2906, VSQRTSS = 2897,
            VSTMXCSR = 9970, VSUBPD = 3408, VSUBPS = 3400, VSUBSD = 3424, VSUBSS = 3416,
            VTESTPD = 7590, VTESTPS = 7581, VUCOMISD = 2761, VUCOMISS = 2751,
            VUNPCKHPD = 2317, VUNPCKHPS = 2306, VUNPCKLPD = 2275, VUNPCKLPS = 2264,
            VXORPD = 3095, VXORPS = 3087, VZEROALL = 4118, VZEROUPPER = 4106,
            WAIT = 10013, WBINVD = 561, WRFSBASE = 9931, WRGSBASE = 9960, WRMSR = 586,
            XADD = 946, XCHG = 212, XGETBV = 1771, XLAT = 400, XOR = 61, XORPD = 3080,
            XORPS = 3073, XRSTOR = 4273, XRSTOR64 = 4281, XSAVE = 4249, XSAVE64 = 4256,
            XSAVEOPT = 4299, XSAVEOPT64 = 4309, XSETBV = 1779
        }

        /// <summary>
        /// Various types of registers.
        /// </summary>
        [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1602:EnumerationItemsMustBeDocumented",
            Justification = "Self-explanatory variables.")]
        public enum RegisterType
        {
            RAX, RCX, RDX, RBX, RSP, RBP, RSI, RDI, R8, R9, R10, R11, R12, R13, R14, R15,
            EAX, ECX, EDX, EBX, ESP, EBP, ESI, EDI, R8D, R9D, R10D, R11D, R12D, R13D, R14D, R15D,
            AX, CX, DX, BX, SP, BP, SI, DI, R8W, R9W, R10W, R11W, R12W, R13W, R14W, R15W,
            AL, CL, DL, BL, AH, CH, DH, BH, R8B, R9B, R10B, R11B, R12B, R13B, R14B, R15B,
            SPL, BPL, SIL, DIL,
            ES, CS, SS, DS, FS, GS,
            RIP,
            ST0, ST1, ST2, ST3, ST4, ST5, ST6, ST7,
            MM0, MM1, MM2, MM3, MM4, MM5, MM6, MM7,
            XMM0, XMM1, XMM2, XMM3, XMM4, XMM5, XMM6, XMM7, XMM8, XMM9, XMM10, XMM11, XMM12, XMM13, XMM14, XMM15,
            YMM0, YMM1, YMM2, YMM3, YMM4, YMM5, YMM6, YMM7, YMM8, YMM9, YMM10, YMM11, YMM12, YMM13, YMM14, YMM15,
            CR0, UNUSED0, CR2, CR3, CR4, UNUSED1, UNUSED2, UNUSED3, CR8,
            DR0, DR1, DR2, DR3, UNUSED4, UNUSED5, DR6, DR7
        }

#pragma warning restore 1591

        /// <summary>
        /// Various operand sizes.
        /// </summary>
        private enum OperandSizes
        {
            /// <summary>
            /// A single byte operand.
            /// </summary>
            Byte = 1,

            /// <summary>
            /// An two byte operand.
            /// </summary>
            Word = 2,

            /// <summary>
            /// A four byte operand.
            /// </summary>
            Dword = 4,

            /// <summary>
            /// An eight byte operand.
            /// </summary>
            Qword = 8
        }

        #endregion

        #region Properties

        #region Mnemonics

        /// <summary>
        /// Gets the set of mnemonics, represented as a single character array.
        /// </summary>
        public static char[] MNEMONICS
        {
            get
            {
                return (
                    "\x09" + "UNDEFINED\0" + "\x03" + "ADD\0" + "\x04" + "PUSH\0" + "\x03" + "POP\0" + "\x02" + "OR\0" +
                    "\x03" + "ADC\0" + "\x03" + "SBB\0" + "\x03" + "AND\0" + "\x03" + "DAA\0" + "\x03" + "SUB\0" +
                    "\x03" + "DAS\0" + "\x03" + "XOR\0" + "\x03" + "AAA\0" + "\x03" + "CMP\0" + "\x03" + "AAS\0" +
                    "\x03" + "INC\0" + "\x03" + "DEC\0" + "\x05" + "PUSHA\0" + "\x04" + "POPA\0" + "\x05" + "BOUND\0" +
                    "\x04" + "ARPL\0" + "\x04" + "IMUL\0" + "\x03" + "INS\0" + "\x04" + "OUTS\0" + "\x02" + "JO\0" +
                    "\x03" + "JNO\0" + "\x02" + "JB\0" + "\x03" + "JAE\0" + "\x02" + "JZ\0" + "\x03" + "JNZ\0" + "\x03" + "JBE\0" +
                    "\x02" + "JA\0" + "\x02" + "JS\0" + "\x03" + "JNS\0" + "\x02" + "JP\0" + "\x03" + "JNP\0" + "\x02" + "JL\0" +
                    "\x03" + "JGE\0" + "\x03" + "JLE\0" + "\x02" + "JG\0" + "\x04" + "TEST\0" + "\x04" + "XCHG\0" +
                    "\x03" + "MOV\0" + "\x03" + "LEA\0" + "\x03" + "CBW\0" + "\x04" + "CWDE\0" + "\x04" + "CDQE\0" +
                    "\x03" + "CWD\0" + "\x03" + "CDQ\0" + "\x03" + "CQO\0" + "\x08" + "CALL FAR\0" + "\x05" + "PUSHF\0" +
                    "\x04" + "POPF\0" + "\x04" + "SAHF\0" + "\x04" + "LAHF\0" + "\x04" + "MOVS\0" + "\x04" + "CMPS\0" +
                    "\x04" + "STOS\0" + "\x04" + "LODS\0" + "\x04" + "SCAS\0" + "\x03" + "RET\0" + "\x03" + "LES\0" +
                    "\x03" + "LDS\0" + "\x05" + "ENTER\0" + "\x05" + "LEAVE\0" + "\x04" + "RETF\0" + "\x05" + "INT 3\0" +
                    "\x03" + "INT\0" + "\x04" + "INTO\0" + "\x04" + "IRET\0" + "\x03" + "AAM\0" + "\x03" + "AAD\0" +
                    "\x04" + "SALC\0" + "\x04" + "XLAT\0" + "\x06" + "LOOPNZ\0" + "\x05" + "LOOPZ\0" + "\x04" + "LOOP\0" +
                    "\x04" + "JCXZ\0" + "\x05" + "JECXZ\0" + "\x05" + "JRCXZ\0" + "\x02" + "IN\0" + "\x03" + "OUT\0" +
                    "\x04" + "CALL\0" + "\x03" + "JMP\0" + "\x07" + "JMP FAR\0" + "\x04" + "INT1\0" + "\x03" + "HLT\0" +
                    "\x03" + "CMC\0" + "\x03" + "CLC\0" + "\x03" + "STC\0" + "\x03" + "CLI\0" + "\x03" + "STI\0" +
                    "\x03" + "CLD\0" + "\x03" + "STD\0" + "\x03" + "LAR\0" + "\x03" + "LSL\0" + "\x07" + "SYSCALL\0" +
                    "\x04" + "CLTS\0" + "\x06" + "SYSRET\0" + "\x04" + "INVD\0" + "\x06" + "WBINVD\0" + "\x03" + "UD2\0" +
                    "\x05" + "FEMMS\0" + "\x03" + "NOP\0" + "\x05" + "WRMSR\0" + "\x05" + "RDTSC\0" + "\x05" + "RDMSR\0" +
                    "\x05" + "RDPMC\0" + "\x08" + "SYSENTER\0" + "\x07" + "SYSEXIT\0" + "\x06" + "GETSEC\0" + "\x05" + "CMOVO\0" +
                    "\x06" + "CMOVNO\0" + "\x05" + "CMOVB\0" + "\x06" + "CMOVAE\0" + "\x05" + "CMOVZ\0" + "\x06" + "CMOVNZ\0" +
                    "\x06" + "CMOVBE\0" + "\x05" + "CMOVA\0" + "\x05" + "CMOVS\0" + "\x06" + "CMOVNS\0" + "\x05" + "CMOVP\0" +
                    "\x06" + "CMOVNP\0" + "\x05" + "CMOVL\0" + "\x06" + "CMOVGE\0" + "\x06" + "CMOVLE\0" + "\x05" + "CMOVG\0" +
                    "\x04" + "SETO\0" + "\x05" + "SETNO\0" + "\x04" + "SETB\0" + "\x05" + "SETAE\0" + "\x04" + "SETZ\0" +
                    "\x05" + "SETNZ\0" + "\x05" + "SETBE\0" + "\x04" + "SETA\0" + "\x04" + "SETS\0" + "\x05" + "SETNS\0" +
                    "\x04" + "SETP\0" + "\x05" + "SETNP\0" + "\x04" + "SETL\0" + "\x05" + "SETGE\0" + "\x05" + "SETLE\0" +
                    "\x04" + "SETG\0" + "\x05" + "CPUID\0" + "\x02" + "BT\0" + "\x04" + "SHLD\0" + "\x03" + "RSM\0" +
                    "\x03" + "BTS\0" + "\x04" + "SHRD\0" + "\x07" + "CMPXCHG\0" + "\x03" + "LSS\0" + "\x03" + "BTR\0" +
                    "\x03" + "LFS\0" + "\x03" + "LGS\0" + "\x05" + "MOVZX\0" + "\x03" + "BTC\0" + "\x03" + "BSF\0" +
                    "\x05" + "MOVSX\0" + "\x04" + "XADD\0" + "\x06" + "MOVNTI\0" + "\x05" + "BSWAP\0" + "\x03" + "ROL\0" +
                    "\x03" + "ROR\0" + "\x03" + "RCL\0" + "\x03" + "RCR\0" + "\x03" + "SHL\0" + "\x03" + "SHR\0" +
                    "\x03" + "SAL\0" + "\x03" + "SAR\0" + "\x04" + "FADD\0" + "\x04" + "FMUL\0" + "\x04" + "FCOM\0" +
                    "\x05" + "FCOMP\0" + "\x04" + "FSUB\0" + "\x05" + "FSUBR\0" + "\x04" + "FDIV\0" + "\x05" + "FDIVR\0" +
                    "\x03" + "FLD\0" + "\x03" + "FST\0" + "\x04" + "FSTP\0" + "\x06" + "FLDENV\0" + "\x05" + "FLDCW\0" +
                    "\x04" + "FXCH\0" + "\x04" + "FNOP\0" + "\x04" + "FCHS\0" + "\x04" + "FABS\0" + "\x04" + "FTST\0" +
                    "\x04" + "FXAM\0" + "\x04" + "FLD1\0" + "\x06" + "FLDL2T\0" + "\x06" + "FLDL2E\0" + "\x05" + "FLDPI\0" +
                    "\x06" + "FLDLG2\0" + "\x06" + "FLDLN2\0" + "\x04" + "FLDZ\0" + "\x05" + "F2XM1\0" + "\x05" + "FYL2X\0" +
                    "\x05" + "FPTAN\0" + "\x06" + "FPATAN\0" + "\x07" + "FXTRACT\0" + "\x06" + "FPREM1\0" + "\x07" + "FDECSTP\0" +
                    "\x07" + "FINCSTP\0" + "\x05" + "FPREM\0" + "\x07" + "FYL2XP1\0" + "\x05" + "FSQRT\0" + "\x07" + "FSINCOS\0" +
                    "\x07" + "FRNDINT\0" + "\x06" + "FSCALE\0" + "\x04" + "FSIN\0" + "\x04" + "FCOS\0" + "\x05" + "FIADD\0" +
                    "\x05" + "FIMUL\0" + "\x05" + "FICOM\0" + "\x06" + "FICOMP\0" + "\x05" + "FISUB\0" + "\x06" + "FISUBR\0" +
                    "\x05" + "FIDIV\0" + "\x06" + "FIDIVR\0" + "\x06" + "FCMOVB\0" + "\x06" + "FCMOVE\0" + "\x07" + "FCMOVBE\0" +
                    "\x06" + "FCMOVU\0" + "\x07" + "FUCOMPP\0" + "\x04" + "FILD\0" + "\x06" + "FISTTP\0" + "\x04" + "FIST\0" +
                    "\x05" + "FISTP\0" + "\x07" + "FCMOVNB\0" + "\x07" + "FCMOVNE\0" + "\x08" + "FCMOVNBE\0" +
                    "\x07" + "FCMOVNU\0" + "\x04" + "FENI\0" + "\x06" + "FEDISI\0" + "\x06" + "FSETPM\0" + "\x06" + "FUCOMI\0" +
                    "\x05" + "FCOMI\0" + "\x06" + "FRSTOR\0" + "\x05" + "FFREE\0" + "\x05" + "FUCOM\0" + "\x06" + "FUCOMP\0" +
                    "\x05" + "FADDP\0" + "\x05" + "FMULP\0" + "\x06" + "FCOMPP\0" + "\x06" + "FSUBRP\0" + "\x05" + "FSUBP\0" +
                    "\x06" + "FDIVRP\0" + "\x05" + "FDIVP\0" + "\x04" + "FBLD\0" + "\x05" + "FBSTP\0" + "\x07" + "FUCOMIP\0" +
                    "\x06" + "FCOMIP\0" + "\x03" + "NOT\0" + "\x03" + "NEG\0" + "\x03" + "MUL\0" + "\x03" + "DIV\0" +
                    "\x04" + "IDIV\0" + "\x04" + "SLDT\0" + "\x03" + "STR\0" + "\x04" + "LLDT\0" + "\x03" + "LTR\0" +
                    "\x04" + "VERR\0" + "\x04" + "VERW\0" + "\x04" + "SGDT\0" + "\x04" + "SIDT\0" + "\x04" + "LGDT\0" +
                    "\x04" + "LIDT\0" + "\x04" + "SMSW\0" + "\x04" + "LMSW\0" + "\x06" + "INVLPG\0" + "\x06" + "VMCALL\0" +
                    "\x08" + "VMLAUNCH\0" + "\x08" + "VMRESUME\0" + "\x06" + "VMXOFF\0" + "\x07" + "MONITOR\0" +
                    "\x05" + "MWAIT\0" + "\x06" + "XGETBV\0" + "\x06" + "XSETBV\0" + "\x05" + "VMRUN\0" + "\x07" + "VMMCALL\0" +
                    "\x06" + "VMLOAD\0" + "\x06" + "VMSAVE\0" + "\x04" + "STGI\0" + "\x04" + "CLGI\0" + "\x06" + "SKINIT\0" +
                    "\x07" + "INVLPGA\0" + "\x06" + "SWAPGS\0" + "\x06" + "RDTSCP\0" + "\x08" + "PREFETCH\0" +
                    "\x09" + "PREFETCHW\0" + "\x05" + "PI2FW\0" + "\x05" + "PI2FD\0" + "\x05" + "PF2IW\0" + "\x05" + "PF2ID\0" +
                    "\x06" + "PFNACC\0" + "\x07" + "PFPNACC\0" + "\x07" + "PFCMPGE\0" + "\x05" + "PFMIN\0" + "\x05" + "PFRCP\0" +
                    "\x07" + "PFRSQRT\0" + "\x05" + "PFSUB\0" + "\x05" + "PFADD\0" + "\x07" + "PFCMPGT\0" + "\x05" + "PFMAX\0" +
                    "\x08" + "PFRCPIT1\0" + "\x08" + "PFRSQIT1\0" + "\x06" + "PFSUBR\0" + "\x05" + "PFACC\0" +
                    "\x07" + "PFCMPEQ\0" + "\x05" + "PFMUL\0" + "\x08" + "PFRCPIT2\0" + "\x07" + "PMULHRW\0" +
                    "\x06" + "PSWAPD\0" + "\x07" + "PAVGUSB\0" + "\x06" + "MOVUPS\0" + "\x06" + "MOVUPD\0" + "\x05" + "MOVSS\0" +
                    "\x05" + "MOVSD\0" + "\x06" + "VMOVSS\0" + "\x06" + "VMOVSD\0" + "\x07" + "VMOVUPS\0" + "\x07" + "VMOVUPD\0" +
                    "\x07" + "MOVHLPS\0" + "\x06" + "MOVLPS\0" + "\x06" + "MOVLPD\0" + "\x08" + "MOVSLDUP\0" +
                    "\x07" + "MOVDDUP\0" + "\x08" + "VMOVHLPS\0" + "\x07" + "VMOVLPS\0" + "\x07" + "VMOVLPD\0" +
                    "\x09" + "VMOVSLDUP\0" + "\x08" + "VMOVDDUP\0" + "\x08" + "UNPCKLPS\0" + "\x08" + "UNPCKLPD\0" +
                    "\x09" + "VUNPCKLPS\0" + "\x09" + "VUNPCKLPD\0" + "\x08" + "UNPCKHPS\0" + "\x08" + "UNPCKHPD\0" +
                    "\x09" + "VUNPCKHPS\0" + "\x09" + "VUNPCKHPD\0" + "\x07" + "MOVLHPS\0" + "\x06" + "MOVHPS\0" +
                    "\x06" + "MOVHPD\0" + "\x08" + "MOVSHDUP\0" + "\x08" + "VMOVLHPS\0" + "\x07" + "VMOVHPS\0" +
                    "\x07" + "VMOVHPD\0" + "\x09" + "VMOVSHDUP\0" + "\x0b" + "PREFETCHNTA\0" + "\x0a" + "PREFETCHT0\0" +
                    "\x0a" + "PREFETCHT1\0" + "\x0a" + "PREFETCHT2\0" + "\x06" + "MOVAPS\0" + "\x06" + "MOVAPD\0" +
                    "\x07" + "VMOVAPS\0" + "\x07" + "VMOVAPD\0" + "\x08" + "CVTPI2PS\0" + "\x08" + "CVTPI2PD\0" +
                    "\x08" + "CVTSI2SS\0" + "\x08" + "CVTSI2SD\0" + "\x09" + "VCVTSI2SS\0" + "\x09" + "VCVTSI2SD\0" +
                    "\x07" + "MOVNTPS\0" + "\x07" + "MOVNTPD\0" + "\x07" + "MOVNTSS\0" + "\x07" + "MOVNTSD\0" +
                    "\x08" + "VMOVNTPS\0" + "\x08" + "VMOVNTPD\0" + "\x09" + "CVTTPS2PI\0" + "\x09" + "CVTTPD2PI\0" +
                    "\x09" + "CVTTSS2SI\0" + "\x09" + "CVTTSD2SI\0" + "\x0a" + "VCVTTSS2SI\0" + "\x0a" + "VCVTTSD2SI\0" +
                    "\x08" + "CVTPS2PI\0" + "\x08" + "CVTPD2PI\0" + "\x08" + "CVTSS2SI\0" + "\x08" + "CVTSD2SI\0" +
                    "\x09" + "VCVTSS2SI\0" + "\x09" + "VCVTSD2SI\0" + "\x07" + "UCOMISS\0" + "\x07" + "UCOMISD\0" +
                    "\x08" + "VUCOMISS\0" + "\x08" + "VUCOMISD\0" + "\x06" + "COMISS\0" + "\x06" + "COMISD\0" +
                    "\x07" + "VCOMISS\0" + "\x07" + "VCOMISD\0" + "\x08" + "MOVMSKPS\0" + "\x08" + "MOVMSKPD\0" +
                    "\x09" + "VMOVMSKPS\0" + "\x09" + "VMOVMSKPD\0" + "\x06" + "SQRTPS\0" + "\x06" + "SQRTPD\0" +
                    "\x06" + "SQRTSS\0" + "\x06" + "SQRTSD\0" + "\x07" + "VSQRTSS\0" + "\x07" + "VSQRTSD\0" + "\x07" + "VSQRTPS\0" +
                    "\x07" + "VSQRTPD\0" + "\x07" + "RSQRTPS\0" + "\x07" + "RSQRTSS\0" + "\x08" + "VRSQRTSS\0" +
                    "\x08" + "VRSQRTPS\0" + "\x05" + "RCPPS\0" + "\x05" + "RCPSS\0" + "\x06" + "VRCPSS\0" + "\x06" + "VRCPPS\0" +
                    "\x05" + "ANDPS\0" + "\x05" + "ANDPD\0" + "\x06" + "VANDPS\0" + "\x06" + "VANDPD\0" + "\x06" + "ANDNPS\0" +
                    "\x06" + "ANDNPD\0" + "\x07" + "VANDNPS\0" + "\x07" + "VANDNPD\0" + "\x04" + "ORPS\0" + "\x04" + "ORPD\0" +
                    "\x05" + "VORPS\0" + "\x05" + "VORPD\0" + "\x05" + "XORPS\0" + "\x05" + "XORPD\0" + "\x06" + "VXORPS\0" +
                    "\x06" + "VXORPD\0" + "\x05" + "ADDPS\0" + "\x05" + "ADDPD\0" + "\x05" + "ADDSS\0" + "\x05" + "ADDSD\0" +
                    "\x06" + "VADDPS\0" + "\x06" + "VADDPD\0" + "\x06" + "VADDSS\0" + "\x06" + "VADDSD\0" + "\x05" + "MULPS\0" +
                    "\x05" + "MULPD\0" + "\x05" + "MULSS\0" + "\x05" + "MULSD\0" + "\x06" + "VMULPS\0" + "\x06" + "VMULPD\0" +
                    "\x06" + "VMULSS\0" + "\x06" + "VMULSD\0" + "\x08" + "CVTPS2PD\0" + "\x08" + "CVTPD2PS\0" +
                    "\x08" + "CVTSS2SD\0" + "\x08" + "CVTSD2SS\0" + "\x09" + "VCVTSS2SD\0" + "\x09" + "VCVTSD2SS\0" +
                    "\x09" + "VCVTPS2PD\0" + "\x09" + "VCVTPD2PS\0" + "\x08" + "CVTDQ2PS\0" + "\x08" + "CVTPS2DQ\0" +
                    "\x09" + "CVTTPS2DQ\0" + "\x09" + "VCVTDQ2PS\0" + "\x09" + "VCVTPS2DQ\0" + "\x0a" + "VCVTTPS2DQ\0" +
                    "\x05" + "SUBPS\0" + "\x05" + "SUBPD\0" + "\x05" + "SUBSS\0" + "\x05" + "SUBSD\0" + "\x06" + "VSUBPS\0" +
                    "\x06" + "VSUBPD\0" + "\x06" + "VSUBSS\0" + "\x06" + "VSUBSD\0" + "\x05" + "MINPS\0" + "\x05" + "MINPD\0" +
                    "\x05" + "MINSS\0" + "\x05" + "MINSD\0" + "\x06" + "VMINPS\0" + "\x06" + "VMINPD\0" + "\x06" + "VMINSS\0" +
                    "\x06" + "VMINSD\0" + "\x05" + "DIVPS\0" + "\x05" + "DIVPD\0" + "\x05" + "DIVSS\0" + "\x05" + "DIVSD\0" +
                    "\x06" + "VDIVPS\0" + "\x06" + "VDIVPD\0" + "\x06" + "VDIVSS\0" + "\x06" + "VDIVSD\0" + "\x05" + "MAXPS\0" +
                    "\x05" + "MAXPD\0" + "\x05" + "MAXSS\0" + "\x05" + "MAXSD\0" + "\x06" + "VMAXPS\0" + "\x06" + "VMAXPD\0" +
                    "\x06" + "VMAXSS\0" + "\x06" + "VMAXSD\0" + "\x09" + "PUNPCKLBW\0" + "\x0a" + "VPUNPCKLBW\0" +
                    "\x09" + "PUNPCKLWD\0" + "\x0a" + "VPUNPCKLWD\0" + "\x09" + "PUNPCKLDQ\0" + "\x0a" + "VPUNPCKLDQ\0" +
                    "\x08" + "PACKSSWB\0" + "\x09" + "VPACKSSWB\0" + "\x07" + "PCMPGTB\0" + "\x08" + "VPCMPGTB\0" +
                    "\x07" + "PCMPGTW\0" + "\x08" + "VPCMPGTW\0" + "\x07" + "PCMPGTD\0" + "\x08" + "VPCMPGTD\0" +
                    "\x08" + "PACKUSWB\0" + "\x09" + "VPACKUSWB\0" + "\x09" + "PUNPCKHBW\0" + "\x0a" + "VPUNPCKHBW\0" +
                    "\x09" + "PUNPCKHWD\0" + "\x0a" + "VPUNPCKHWD\0" + "\x09" + "PUNPCKHDQ\0" + "\x0a" + "VPUNPCKHDQ\0" +
                    "\x08" + "PACKSSDW\0" + "\x09" + "VPACKSSDW\0" + "\x0a" + "PUNPCKLQDQ\0" + "\x0b" + "VPUNPCKLQDQ\0" +
                    "\x0a" + "PUNPCKHQDQ\0" + "\x0b" + "VPUNPCKHQDQ\0" + "\x04" + "MOVD\0" + "\x04" + "MOVQ\0" +
                    "\x05" + "VMOVD\0" + "\x05" + "VMOVQ\0" + "\x06" + "MOVDQA\0" + "\x06" + "MOVDQU\0" + "\x07" + "VMOVDQA\0" +
                    "\x07" + "VMOVDQU\0" + "\x06" + "PSHUFW\0" + "\x06" + "PSHUFD\0" + "\x07" + "PSHUFHW\0" + "\x07" + "PSHUFLW\0" +
                    "\x07" + "VPSHUFD\0" + "\x08" + "VPSHUFHW\0" + "\x08" + "VPSHUFLW\0" + "\x07" + "PCMPEQB\0" +
                    "\x08" + "VPCMPEQB\0" + "\x07" + "PCMPEQW\0" + "\x08" + "VPCMPEQW\0" + "\x07" + "PCMPEQD\0" +
                    "\x08" + "VPCMPEQD\0" + "\x04" + "EMMS\0" + "\x0a" + "VZEROUPPER\0" + "\x08" + "VZEROALL\0" +
                    "\x06" + "VMREAD\0" + "\x05" + "EXTRQ\0" + "\x07" + "INSERTQ\0" + "\x07" + "VMWRITE\0" + "\x06" + "HADDPD\0" +
                    "\x06" + "HADDPS\0" + "\x07" + "VHADDPD\0" + "\x07" + "VHADDPS\0" + "\x06" + "HSUBPD\0" + "\x06" + "HSUBPS\0" +
                    "\x07" + "VHSUBPD\0" + "\x07" + "VHSUBPS\0" + "\x06" + "FXSAVE\0" + "\x07" + "FXRSTOR\0" +
                    "\x04" + "XAVE\0" + "\x06" + "LFENCE\0" + "\x06" + "XRSTOR\0" + "\x06" + "MFENCE\0" + "\x06" + "SFENCE\0" +
                    "\x07" + "CLFLUSH\0" + "\x06" + "POPCNT\0" + "\x03" + "BSR\0" + "\x05" + "LZCNT\0" + "\x07" + "CMPEQPS\0" +
                    "\x07" + "CMPLTPS\0" + "\x07" + "CMPLEPS\0" + "\x0a" + "CMPUNORDPS\0" + "\x08" + "CMPNEQPS\0" +
                    "\x08" + "CMPNLTPS\0" + "\x08" + "CMPNLEPS\0" + "\x08" + "CMPORDPS\0" + "\x07" + "CMPEQPD\0" +
                    "\x07" + "CMPLTPD\0" + "\x07" + "CMPLEPD\0" + "\x0a" + "CMPUNORDPD\0" + "\x08" + "CMPNEQPD\0" +
                    "\x08" + "CMPNLTPD\0" + "\x08" + "CMPNLEPD\0" + "\x08" + "CMPORDPD\0" + "\x07" + "CMPEQSS\0" +
                    "\x07" + "CMPLTSS\0" + "\x07" + "CMPLESS\0" + "\x0a" + "CMPUNORDSS\0" + "\x08" + "CMPNEQSS\0" +
                    "\x08" + "CMPNLTSS\0" + "\x08" + "CMPNLESS\0" + "\x08" + "CMPORDSS\0" + "\x07" + "CMPEQSD\0" +
                    "\x07" + "CMPLTSD\0" + "\x07" + "CMPLESD\0" + "\x0a" + "CMPUNORDSD\0" + "\x08" + "CMPNEQSD\0" +
                    "\x08" + "CMPNLTSD\0" + "\x08" + "CMPNLESD\0" + "\x08" + "CMPORDSD\0" + "\x08" + "VCMPEQPS\0" +
                    "\x08" + "VCMPLTPS\0" + "\x08" + "VCMPLEPS\0" + "\x0b" + "VCMPUNORDPS\0" + "\x09" + "VCMPNEQPS\0" +
                    "\x09" + "VCMPNLTPS\0" + "\x09" + "VCMPNLEPS\0" + "\x09" + "VCMPORDPS\0" + "\x08" + "VCMPEQPD\0" +
                    "\x08" + "VCMPLTPD\0" + "\x08" + "VCMPLEPD\0" + "\x0b" + "VCMPUNORDPD\0" + "\x09" + "VCMPNEQPD\0" +
                    "\x09" + "VCMPNLTPD\0" + "\x09" + "VCMPNLEPD\0" + "\x09" + "VCMPORDPD\0" + "\x08" + "VCMPEQSS\0" +
                    "\x08" + "VCMPLTSS\0" + "\x08" + "VCMPLESS\0" + "\x0b" + "VCMPUNORDSS\0" + "\x09" + "VCMPNEQSS\0" +
                    "\x09" + "VCMPNLTSS\0" + "\x09" + "VCMPNLESS\0" + "\x09" + "VCMPORDSS\0" + "\x08" + "VCMPEQSD\0" +
                    "\x08" + "VCMPLTSD\0" + "\x08" + "VCMPLESD\0" + "\x0b" + "VCMPUNORDSD\0" + "\x09" + "VCMPNEQSD\0" +
                    "\x09" + "VCMPNLTSD\0" + "\x09" + "VCMPNLESD\0" + "\x09" + "VCMPORDSD\0" + "\x06" + "PINSRW\0" +
                    "\x07" + "VPINSRW\0" + "\x06" + "PEXTRW\0" + "\x07" + "VPEXTRW\0" + "\x06" + "SHUFPS\0" + "\x06" + "SHUFPD\0" +
                    "\x07" + "VSHUFPS\0" + "\x07" + "VSHUFPD\0" + "\x09" + "CMPXCHG8B\0" + "\x0a" + "CMPXCHG16B\0" +
                    "\x07" + "VMPTRST\0" + "\x08" + "ADDSUBPD\0" + "\x08" + "ADDSUBPS\0" + "\x09" + "VADDSUBPD\0" +
                    "\x09" + "VADDSUBPS\0" + "\x05" + "PSRLW\0" + "\x06" + "VPSRLW\0" + "\x05" + "PSRLD\0" + "\x06" + "VPSRLD\0" +
                    "\x05" + "PSRLQ\0" + "\x06" + "VPSRLQ\0" + "\x05" + "PADDQ\0" + "\x06" + "VPADDQ\0" + "\x06" + "PMULLW\0" +
                    "\x07" + "VPMULLW\0" + "\x07" + "MOVQ2DQ\0" + "\x07" + "MOVDQ2Q\0" + "\x08" + "PMOVMSKB\0" +
                    "\x09" + "VPMOVMSKB\0" + "\x07" + "PSUBUSB\0" + "\x08" + "VPSUBUSB\0" + "\x07" + "PSUBUSW\0" +
                    "\x08" + "VPSUBUSW\0" + "\x06" + "PMINUB\0" + "\x07" + "VPMINUB\0" + "\x04" + "PAND\0" + "\x05" + "VPAND\0" +
                    "\x07" + "PADDUSB\0" + "\x08" + "VPADDUSW\0" + "\x07" + "PADDUSW\0" + "\x06" + "PMAXUB\0" +
                    "\x07" + "VPMAXUB\0" + "\x05" + "PANDN\0" + "\x06" + "VPANDN\0" + "\x05" + "PAVGB\0" + "\x06" + "VPAVGB\0" +
                    "\x05" + "PSRAW\0" + "\x06" + "VPSRAW\0" + "\x05" + "PSRAD\0" + "\x06" + "VPSRAD\0" + "\x05" + "PAVGW\0" +
                    "\x06" + "VPAVGW\0" + "\x07" + "PMULHUW\0" + "\x08" + "VPMULHUW\0" + "\x06" + "PMULHW\0" +
                    "\x07" + "VPMULHW\0" + "\x09" + "CVTTPD2DQ\0" + "\x08" + "CVTDQ2PD\0" + "\x08" + "CVTPD2DQ\0" +
                    "\x0a" + "VCVTTPD2DQ\0" + "\x09" + "VCVTDQ2PD\0" + "\x09" + "VCVTPD2DQ\0" + "\x06" + "MOVNTQ\0" +
                    "\x07" + "MOVNTDQ\0" + "\x08" + "VMOVNTDQ\0" + "\x06" + "PSUBSB\0" + "\x07" + "VPSUBSB\0" +
                    "\x06" + "PSUBSW\0" + "\x07" + "VPSUBSW\0" + "\x06" + "PMINSW\0" + "\x07" + "VPMINSW\0" + "\x03" + "POR\0" +
                    "\x04" + "VPOR\0" + "\x06" + "PADDSB\0" + "\x07" + "VPADDSB\0" + "\x06" + "PADDSW\0" + "\x07" + "VPADDSW\0" +
                    "\x06" + "PMAXSW\0" + "\x07" + "VPMAXSW\0" + "\x04" + "PXOR\0" + "\x05" + "VPXOR\0" + "\x05" + "LDDQU\0" +
                    "\x06" + "VLDDQU\0" + "\x05" + "PSLLW\0" + "\x06" + "VPSLLW\0" + "\x05" + "PSLLD\0" + "\x06" + "VPSLLD\0" +
                    "\x05" + "PSLLQ\0" + "\x06" + "VPSLLQ\0" + "\x07" + "PMULUDQ\0" + "\x08" + "VPMULUDQ\0" + "\x07" + "PMADDWD\0" +
                    "\x08" + "VPMADDWD\0" + "\x06" + "PSADBW\0" + "\x07" + "VPSADBW\0" + "\x08" + "MASKMOVQ\0" +
                    "\x0a" + "MASKMOVDQU\0" + "\x0b" + "VMASKMOVDQU\0" + "\x05" + "PSUBB\0" + "\x06" + "VPSUBB\0" +
                    "\x05" + "PSUBW\0" + "\x06" + "VPSUBW\0" + "\x05" + "PSUBD\0" + "\x06" + "VPSUBD\0" + "\x05" + "PSUBQ\0" +
                    "\x06" + "VPSUBQ\0" + "\x05" + "PADDB\0" + "\x06" + "VPADDB\0" + "\x05" + "PADDW\0" + "\x06" + "VPADDW\0" +
                    "\x05" + "PADDD\0" + "\x06" + "VPADDD\0" + "\x07" + "FNSTENV\0" + "\x06" + "FSTENV\0" + "\x06" + "FNSTCW\0" +
                    "\x05" + "FSTCW\0" + "\x06" + "FNCLEX\0" + "\x05" + "FCLEX\0" + "\x06" + "FNINIT\0" + "\x05" + "FINIT\0" +
                    "\x06" + "FNSAVE\0" + "\x05" + "FSAVE\0" + "\x06" + "FNSTSW\0" + "\x05" + "FSTSW\0" + "\x06" + "PSHUFB\0" +
                    "\x07" + "VPSHUFB\0" + "\x06" + "PHADDW\0" + "\x07" + "VPHADDW\0" + "\x06" + "PHADDD\0" + "\x07" + "VPHADDD\0" +
                    "\x07" + "PHADDSW\0" + "\x08" + "VPHADDSW\0" + "\x09" + "PMADDUBSW\0" + "\x0a" + "VPMADDUBSW\0" +
                    "\x06" + "PHSUBW\0" + "\x07" + "VPHSUBW\0" + "\x06" + "PHSUBD\0" + "\x07" + "VPHSUBD\0" + "\x07" + "PHSUBSW\0" +
                    "\x08" + "VPHSUBSW\0" + "\x06" + "PSIGNB\0" + "\x07" + "VPSIGNB\0" + "\x06" + "PSIGNW\0" +
                    "\x07" + "VPSIGNW\0" + "\x06" + "PSIGND\0" + "\x07" + "VPSIGND\0" + "\x08" + "PMULHRSW\0" +
                    "\x09" + "VPMULHRSW\0" + "\x09" + "VPERMILPS\0" + "\x09" + "VPERMILPD\0" + "\x08" + "VPTESTPS\0" +
                    "\x08" + "VPTESTPD\0" + "\x08" + "PBLENDVB\0" + "\x08" + "BLENDVPS\0" + "\x08" + "BLENDVPD\0" +
                    "\x05" + "PTEST\0" + "\x06" + "VPTEST\0" + "\x0c" + "VBROADCASTSS\0" + "\x0c" + "VBROADCASTSD\0" +
                    "\x0e" + "VBROADCASTF128\0" + "\x05" + "PABSB\0" + "\x06" + "VPABSB\0" + "\x05" + "PABSW\0" +
                    "\x06" + "VPABSW\0" + "\x05" + "PABSD\0" + "\x06" + "VPABSD\0" + "\x08" + "PMOVSXBW\0" + "\x09" + "VPMOVSXBW\0" +
                    "\x08" + "PMOVSXBD\0" + "\x09" + "VPMOVSXBD\0" + "\x08" + "PMOVSXBQ\0" + "\x09" + "VPMOVSXBQ\0" +
                    "\x08" + "PMOVSXWD\0" + "\x09" + "VPMOVSXWD\0" + "\x08" + "PMOVSXWQ\0" + "\x09" + "VPMOVSXWQ\0" +
                    "\x08" + "PMOVSXDQ\0" + "\x09" + "VPMOVSXDQ\0" + "\x06" + "PMULDQ\0" + "\x07" + "VPMULDQ\0" +
                    "\x07" + "PCMPEQQ\0" + "\x08" + "VPCMPEQQ\0" + "\x08" + "MOVNTDQA\0" + "\x09" + "VMOVNTDQA\0" +
                    "\x08" + "PACKUSDW\0" + "\x09" + "VPACKUSDW\0" + "\x0a" + "VMASKMOVPS\0" + "\x0a" + "VMASKMOVPD\0" +
                    "\x08" + "PMOVZXBW\0" + "\x09" + "VPMOVZXBW\0" + "\x08" + "PMOVZXBD\0" + "\x09" + "VPMOVZXBD\0" +
                    "\x08" + "PMOVZXBQ\0" + "\x09" + "VPMOVZXBQ\0" + "\x08" + "PMOVZXWD\0" + "\x09" + "VPMOVZXWD\0" +
                    "\x08" + "PMOVZXWQ\0" + "\x09" + "VPMOVZXWQ\0" + "\x08" + "PMOVZXDQ\0" + "\x09" + "VPMOVZXDQ\0" +
                    "\x07" + "PCMPGTQ\0" + "\x08" + "VPCMPGTQ\0" + "\x06" + "PMINSB\0" + "\x07" + "VPMINSB\0" +
                    "\x06" + "PMINSD\0" + "\x07" + "VPMINSD\0" + "\x06" + "PMINUW\0" + "\x07" + "VPMINUW\0" + "\x06" + "PMINUD\0" +
                    "\x07" + "VPMINUD\0" + "\x06" + "PMAXSB\0" + "\x07" + "VPMAXSB\0" + "\x06" + "PMAXSD\0" + "\x07" + "VPMAXSD\0" +
                    "\x06" + "PMAXUW\0" + "\x07" + "VPMAXUW\0" + "\x06" + "PMAXUD\0" + "\x07" + "VPMAXUD\0" + "\x06" + "PMULLD\0" +
                    "\x07" + "VPMULLD\0" + "\x0a" + "PHMINPOSUW\0" + "\x0b" + "VPHMINPOSUW\0" + "\x06" + "INVEPT\0" +
                    "\x07" + "INVVPID\0" + "\x0e" + "VFMADDSUB132PS\0" + "\x0e" + "VFMADDSUB132PD\0" + "\x0e" + "VFMSUBADD132PS\0" +
                    "\x0e" + "VFMSUBADD132PD\0" + "\x0b" + "VFMADD132PS\0" + "\x0b" + "VFMADD132PD\0" + "\x0b" + "VFMADD132SS\0" +
                    "\x0b" + "VFMADD132SD\0" + "\x0b" + "VFMSUB132PS\0" + "\x0b" + "VFMSUB132PD\0" + "\x0b" + "VFMSUB132SS\0" +
                    "\x0b" + "VFMSUB132SD\0" + "\x0c" + "VFNMADD132PS\0" + "\x0c" + "VFNMADD132PD\0" + "\x0c" + "VFNMADD132SS\0" +
                    "\x0c" + "VFNMADD132SD\0" + "\x0c" + "VFNMSUB132PS\0" + "\x0c" + "VFNMSUB132PD\0" + "\x0c" + "VFNMSUB132SS\0" +
                    "\x0c" + "VFNMSUB132SD\0" + "\x0e" + "VFMADDSUB213PS\0" + "\x0e" + "VFMADDSUB213PD\0" +
                    "\x0e" + "VFMSUBADD213PS\0" + "\x0e" + "VFMSUBADD213PD\0" + "\x0b" + "VFMADD213PS\0" +
                    "\x0b" + "VFMADD213PD\0" + "\x0b" + "VFMADD213SS\0" + "\x0b" + "VFMADD213SD\0" + "\x0b" + "VFMSUB213PS\0" +
                    "\x0b" + "VFMSUB213PD\0" + "\x0b" + "VFMSUB213SS\0" + "\x0b" + "VFMSUB213SD\0" + "\x0c" + "VFNMADD213PS\0" +
                    "\x0c" + "VFNMADD213PD\0" + "\x0c" + "VFNMADD213SS\0" + "\x0c" + "VFNMADD213SD\0" + "\x0c" + "VFNMSUB213PS\0" +
                    "\x0c" + "VFNMSUB213PD\0" + "\x0c" + "VFNMSUB213SS\0" + "\x0c" + "VFNMSUB213SD\0" + "\x0e" + "VFMADDSUB231PS\0" +
                    "\x0e" + "VFMADDSUB231PD\0" + "\x0e" + "VFMSUBADD231PS\0" + "\x0e" + "VFMSUBADD231PD\0" +
                    "\x0b" + "VFMADD231PS\0" + "\x0b" + "VFMADD231PD\0" + "\x0b" + "VFMADD231SS\0" + "\x0b" + "VFMADD231SD\0" +
                    "\x0b" + "VFMSUB231PS\0" + "\x0b" + "VFMSUB231PD\0" + "\x0b" + "VFMSUB231SS\0" + "\x0b" + "VFMSUB231SD\0" +
                    "\x0c" + "VFNMADD231PS\0" + "\x0c" + "VFNMADD231PD\0" + "\x0c" + "VFNMADD231SS\0" + "\x0c" + "VFNMADD231SD\0" +
                    "\x0c" + "VFNMSUB231PS\0" + "\x0c" + "VFNMSUB231PD\0" + "\x0c" + "VFNMSUB231SS\0" + "\x0c" + "VFNMSUB231SD\0" +
                    "\x06" + "AESIMC\0" + "\x07" + "VAESIMC\0" + "\x06" + "AESENC\0" + "\x07" + "VAESENC\0" + "\x0a" + "AESENCLAST\0" +
                    "\x0b" + "VAESENCLAST\0" + "\x06" + "AESDEC\0" + "\x07" + "VAESDEC\0" + "\x0a" + "AESDECLAST\0" +
                    "\x0b" + "VAESDECLAST\0" + "\x05" + "MOVBE\0" + "\x05" + "CRC32\0" + "\x0a" + "VPERM2F128\0" +
                    "\x07" + "ROUNDPS\0" + "\x08" + "VROUNDPS\0" + "\x07" + "ROUNDPD\0" + "\x08" + "VROUNDPD\0" +
                    "\x07" + "ROUNDSS\0" + "\x08" + "VROUNDSS\0" + "\x07" + "ROUNDSD\0" + "\x08" + "VROUNDSD\0" +
                    "\x07" + "BLENDPS\0" + "\x08" + "VBLENDPS\0" + "\x07" + "BLENDPD\0" + "\x08" + "VBLENDPD\0" +
                    "\x07" + "PBLENDW\0" + "\x09" + "VPBLENDVW\0" + "\x07" + "PALIGNR\0" + "\x08" + "VPALIGNR\0" +
                    "\x06" + "PEXTRB\0" + "\x07" + "VPEXTRB\0" + "\x06" + "PEXTRD\0" + "\x06" + "PEXTRQ\0" + "\x07" + "VPEXTRD\0" +
                    "\x09" + "EXTRACTPS\0" + "\x0a" + "VEXTRACTPS\0" + "\x0b" + "VINSERTF128\0" + "\x0c" + "VEXTRACTF128\0" +
                    "\x06" + "PINSRB\0" + "\x07" + "VPINSRB\0" + "\x08" + "INSERTPS\0" + "\x09" + "VINSERTPS\0" +
                    "\x06" + "PINSRD\0" + "\x06" + "PINSRQ\0" + "\x07" + "VPINSRD\0" + "\x07" + "VPINSRQ\0" + "\x04" + "DPPS\0" +
                    "\x05" + "VDPPS\0" + "\x04" + "DPPD\0" + "\x05" + "VDPPD\0" + "\x07" + "MPSADBW\0" + "\x08" + "VMPSADBW\0" +
                    "\x09" + "PCLMULQDQ\0" + "\x0a" + "VPCLMULQDQ\0" + "\x09" + "VBLENDVPS\0" + "\x09" + "VBLENDVPD\0" +
                    "\x09" + "VPBLENDVB\0" + "\x09" + "PCMPESTRM\0" + "\x0a" + "VPCMPESTRM\0" + "\x09" + "PCMPESTRI\0" +
                    "\x09" + "VCMPESTRI\0" + "\x09" + "PCMPISTRM\0" + "\x0a" + "VPCMPISTRM\0" + "\x09" + "PCMPISTRI\0" +
                    "\x0a" + "VPCMPISTRI\0" + "\x0f" + "AESKEYGENASSIST\0" + "\x10" + "VAESKEYGENASSIST\0" +
                    "\x06" + "PSRLDQ\0" + "\x07" + "VPSRLDQ\0" + "\x06" + "PSLLDQ\0" + "\x07" + "VPSLLDQ\0" + "\x07" + "LDMXCSR\0" +
                    "\x08" + "VLDMXCSR\0" + "\x07" + "STMXCSR\0" + "\x08" + "VSTMXCSR\0" + "\x07" + "VMPTRLD\0" +
                    "\x07" + "VMCLEAR\0" + "\x05" + "VMXON\0" + "\x04" + "WAIT\0" + "\x06" + "MOVSXD\0" + "\x05" + "PAUSE\0").ToCharArray();
            }
        }

        /// <summary>
        /// Gets the set of register types.
        /// </summary>
        public static WRegister[] REGISTERS
        {
            get
            {
                return new WRegister[]
                {
                    new WRegister(3, "RAX"), new WRegister(3, "RCX"), new WRegister(3, "RDX"), new WRegister(3, "RBX"), new WRegister(3, "RSP"), new WRegister(3, "RBP"), new WRegister(3, "RSI"), new WRegister(3, "RDI"), new WRegister(2, "R8"), new WRegister(2, "R9"), new WRegister(3, "R10"), new WRegister(3, "R11"), new WRegister(3, "R12"), new WRegister(3, "R13"), new WRegister(3, "R14"), new WRegister(3, "R15"),
                    new WRegister(3, "EAX"), new WRegister(3, "ECX"), new WRegister(3, "EDX"), new WRegister(3, "EBX"), new WRegister(3, "ESP"), new WRegister(3, "EBP"), new WRegister(3, "ESI"), new WRegister(3, "EDI"), new WRegister(3, "R8D"), new WRegister(3, "R9D"), new WRegister(4, "R10D"), new WRegister(4, "R11D"), new WRegister(4, "R12D"), new WRegister(4, "R13D"), new WRegister(4, "R14D"), new WRegister(4, "R15D"),
                    new WRegister(2, "AX"), new WRegister(2, "CX"), new WRegister(2, "DX"), new WRegister(2, "BX"), new WRegister(2, "SP"), new WRegister(2, "BP"), new WRegister(2, "SI"), new WRegister(2, "DI"), new WRegister(3, "R8W"), new WRegister(3, "R9W"), new WRegister(4, "R10W"), new WRegister(4, "R11W"), new WRegister(4, "R12W"), new WRegister(4, "R13W"), new WRegister(4, "R14W"), new WRegister(4, "R15W"),
                    new WRegister(2, "AL"), new WRegister(2, "CL"), new WRegister(2, "DL"), new WRegister(2, "BL"), new WRegister(2, "AH"), new WRegister(2, "CH"), new WRegister(2, "DH"), new WRegister(2, "BH"), new WRegister(3, "R8B"), new WRegister(3, "R9B"), new WRegister(4, "R10B"), new WRegister(4, "R11B"), new WRegister(4, "R12B"), new WRegister(4, "R13B"), new WRegister(4, "R14B"), new WRegister(4, "R15B"),
                    new WRegister(3, "SPL"), new WRegister(3, "BPL"), new WRegister(3, "SIL"), new WRegister(3, "DIL"),
                    new WRegister(2, "ES"), new WRegister(2, "CS"), new WRegister(2, "SS"), new WRegister(2, "DS"), new WRegister(2, "FS"), new WRegister(2, "GS"),
                    new WRegister(3, "RIP"),
                    new WRegister(3, "ST0"), new WRegister(3, "ST1"), new WRegister(3, "ST2"), new WRegister(3, "ST3"), new WRegister(3, "ST4"), new WRegister(3, "ST5"), new WRegister(3, "ST6"), new WRegister(3, "ST7"),
                    new WRegister(3, "MM0"), new WRegister(3, "MM1"), new WRegister(3, "MM2"), new WRegister(3, "MM3"), new WRegister(3, "MM4"), new WRegister(3, "MM5"), new WRegister(3, "MM6"), new WRegister(3, "MM7"),
                    new WRegister(4, "XMM0"), new WRegister(4, "XMM1"), new WRegister(4, "XMM2"), new WRegister(4, "XMM3"), new WRegister(4, "XMM4"), new WRegister(4, "XMM5"), new WRegister(4, "XMM6"), new WRegister(4, "XMM7"), new WRegister(4, "XMM8"), new WRegister(4, "XMM9"), new WRegister(5, "XMM10"), new WRegister(5, "XMM11"), new WRegister(5, "XMM12"), new WRegister(5, "XMM13"), new WRegister(5, "XMM14"), new WRegister(5, "XMM15"),
                    new WRegister(4, "YMM0"), new WRegister(4, "YMM1"), new WRegister(4, "YMM2"), new WRegister(4, "YMM3"), new WRegister(4, "YMM4"), new WRegister(4, "YMM5"), new WRegister(4, "YMM6"), new WRegister(4, "YMM7"), new WRegister(4, "YMM8"), new WRegister(4, "YMM9"), new WRegister(5, "YMM10"), new WRegister(5, "YMM11"), new WRegister(5, "YMM12"), new WRegister(5, "YMM13"), new WRegister(5, "YMM14"), new WRegister(5, "YMM15"),
                    new WRegister(3, "CR0"), new WRegister(0, string.Empty), new WRegister(3, "CR2"), new WRegister(3, "CR3"), new WRegister(3, "CR4"), new WRegister(0, string.Empty), new WRegister(0, string.Empty), new WRegister(0, string.Empty), new WRegister(3, "CR8"),
                    new WRegister(3, "DR0"), new WRegister(3, "DR1"), new WRegister(3, "DR2"), new WRegister(3, "DR3"), new WRegister(0, string.Empty), new WRegister(0, string.Empty), new WRegister(3, "DR6"), new WRegister(3, "DR7")
                };
            }
        }

        #endregion

        #endregion

        #region Methods

        /// <summary>
        /// A wrapper for distorm_decompose(), which only takes in the code to be decomposed.
        /// </summary>
        /// <param name="code">The code to be decomposed.</param>
        /// <param name="offset">The offset at which the code starts in the image being disassembled.</param>
        /// <param name="bitDepth">The target architecture type of the code being disassembled.</param>
        /// <param name="logFilename">
        /// The name of the file to use to log important updates about the decomposition process.
        /// </param>
        /// <returns>Returns the code to be decomposed on success or an empty array upon failure.</returns>
        /// <remarks>
        /// Usage of brainpower is required to recognize that decomposing a code array of size 0 will also result in
        /// an empty array.
        /// </remarks>
        public static DInst[] Decompose(
            byte[] code,
            ulong offset = 0,
            DecodeType bitDepth = DecodeType.Decode32Bits,
            string logFilename = "Distorm3cs.log")
        {
            GCHandle gch = GCHandle.Alloc(code, GCHandleType.Pinned);

            Distorm.CodeInfo ci = new Distorm.CodeInfo();
            ci.codeLen = code.Length;
            ci.code = gch.AddrOfPinnedObject();
            ci.codeOffset = offset;
            ci.dt = bitDepth;
            ci.features = Distorm.DecomposeFeatures.NONE;

            // Most likely a gross over-estimation of how large to make the array, but it should never fail.
            Distorm.DInst[] result = new Distorm.DInst[code.Length];
            uint usedInstructionsCount = 0;

            // Decompose the data.
            Distorm.DecodeResult r =
                Distorm.distorm_decompose(ref ci, result, (uint)result.Length, ref usedInstructionsCount);

            // Release the handle pinned to the code.
            gch.Free();

            // Return false if an error occured during decomposition.
            if (!r.Equals(Distorm.DecodeResult.SUCCESS))
            {
                Logger.Log(
                    "Error decomposing data. Result was: " + r.ToString(),
                    logFilename,
                    Logger.Type.CONSOLE | Logger.Type.FILE);
                return new Distorm.DInst[0];
            }

            // Resize the array to match the actual number of instructions decoded.
            Array.Resize(ref result, (int)usedInstructionsCount);

            // Return the result.
            return result;
        }

        /// <summary>
        /// Translates opcodes into a list of strings, which each represent an instruction.
        /// </summary>
        /// <param name="code">The code to be disassembled.</param>
        /// <param name="offset">The offset at which the code starts in the image being disassembled.</param>
        /// <param name="bitDepth">The target architecture type of the code being disassembled.</param>
        /// <returns>Returns the disassembled instructions.</returns>
        public static List<string> Disassemble(
            byte[] code,
            ulong offset = 0,
            DecodeType bitDepth = DecodeType.Decode32Bits)
        {
            List<string> instructions = new List<string>();

            GCHandle gch = GCHandle.Alloc(code, GCHandleType.Pinned);

            // Prepare the CodeInfo structure for decomposition.
            Distorm.CodeInfo ci = new Distorm.CodeInfo();
            ci.codeLen = code.Length;
            ci.code = gch.AddrOfPinnedObject();
            ci.codeOffset = offset;
            ci.dt = bitDepth;
            ci.features = Distorm.DecomposeFeatures.NONE;

            // Prepare the result instruction buffer to receive the decomposition.
            Distorm.DInst[] result = new Distorm.DInst[code.Length];
            uint usedInstructionsCount = 0;

            // Perform the decomposition.
            Distorm.DecodeResult r =
                Distorm.distorm_decompose(ref ci, result, (uint)result.Length, ref usedInstructionsCount);

            // Release the handle pinned to the code.
            gch.Free();

            // Return an empty list if an error occured during decomposition.
            if (!r.Equals(Distorm.DecodeResult.SUCCESS))
            {
                return new List<string>();
            }

            // Prepare a DecodedInst structure for formatting the results.
            Distorm.DecodedInst inst = new Distorm.DecodedInst();

            for (uint i = 0; i < usedInstructionsCount; ++i)
            {
                // Format the results of the decomposition.
                Distorm.distorm_format(ref ci, ref result[i], ref inst);

                // Add it to the buffer to be verified.
                if (string.IsNullOrEmpty(inst.Operands))
                {
                    instructions.Add(inst.Mnemonic);
                }
                else
                {
                    instructions.Add(inst.Mnemonic + " " + inst.Operands);
                }
            }

            return instructions;
        }

        /// <summary>
        /// Decomposes data into assembly format, using the native distorm_decompose function.
        /// </summary>
        /// <param name="ci">
        /// The CodeInfo structure that holds the data that will be decomposed.
        /// </param>
        /// <param name="result">
        /// Array of type Dinst which will be used by this function in order to return the disassembled instructions.
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
        /// DECRES_SUCCESS on success (no more to disassemble), INPUTERR on input error (null code buffer, invalid
        /// decoding mode, etc...), MEMORYERR when there are not enough entries to use in the result array, BUT YOU
        /// STILL have to check for usedInstructionsCount!
        /// </returns>
        /// <remarks>
        /// Side-Effects: Even if the return code is MEMORYERR, there might STILL be data in the array you passed,
        ///               this function will try to use as much entries as possible!
        /// Notes: 1) The minimal size of maxInstructions is 15.
        ///        2) You will have to synchronize the offset,code and length by yourself if you pass code fragments
        ///           and not a complete code block!
        /// </remarks>
        [DllImport("distorm3.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "distorm_decompose" + ArchitectureString)]
        public static extern DecodeResult distorm_decompose(
            ref CodeInfo ci, [In, Out] DInst[] result, uint maxInstructions, ref uint usedInstructionsCount);

        /// <summary>
        /// Convert a Dinst structure, which was produced from the distorm_decompose function, into text.
        /// </summary>
        /// <param name="ci">The CodeInfo structure that holds the data that was decomposed.</param>
        /// <param name="di">The decoded instruction.</param>
        /// <param name="result">The variable to which the formatted instruction will be returned.</param>
        [DllImport("distorm3.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "distorm_format" + ArchitectureString)]
        public static extern void distorm_format(ref CodeInfo ci, ref DInst di, ref DecodedInst result);

        /// <summary>
        /// Gets the string for a given register index.
        /// </summary>
        /// <param name="r">
        /// The value of the index of an Operand structure, within the Dinst structure, if that operand represents a
        /// register.
        /// </param>
        /// <returns>Returns the string for a given register index.</returns>
        /// <remarks>This is the GET_REGISTER_NAME macro in mnemonics.h.</remarks>
        public static WRegister GetRegisterName(uint r)
        {
            return Distorm.REGISTERS[r];
        }

        /// <summary>
        /// Get the textual representation for an instruction.
        /// </summary>
        /// <param name="m">The opcode value of a Dinst structure.</param>
        /// <returns>Returns the textual representation for an instruction.</returns>
        /// <remarks>This is the GET_MNEMONIC_NAME macro in mnemonics.h.</remarks>
        public static WMnemonic GetMnemonicName(uint m)
        {
            WMnemonic wm = new WMnemonic();
            wm.length = MNEMONICS[m];
            wm.p = new char[wm.length];
            for (uint i = 0; i < wm.length; ++i)
            {
                wm.p[i] = Distorm.MNEMONICS[m + 1 + i];
            }

            return wm;
        }

        #endregion

        #region Structures

        /// <summary>
        /// A string representation used when returning a decoded instruction.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct WString
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
        public struct DecodedInst
        {
            /// <summary>
            /// Size of decoded instruction.
            /// </summary>
            public uint size;

            /// <summary>
            /// Start offset of the decoded instruction.
            /// </summary>
            public ulong offset;

            /// <summary>
            /// Mnemonic of decoded instruction, prefixed if required by REP, LOCK etc.
            /// </summary>
            private WString mnemonic;

            /// <summary>
            /// Operands of the decoded instruction, up to 3 operands, comma-separated.
            /// </summary>
            private WString operands;

            /// <summary>
            /// Hex dump - little endian, including prefixes.
            /// </summary>
            private WString instructionHex;

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
        public struct Operand
        {
            /// <summary>
            /// Type of operand:
            /// NONE: operand is to be ignored.
            /// REG: index holds global register index.
            /// IMM: instruction.imm.
            /// IMM1: instruction.imm.ex.i1.
            /// IMM2: instruction.imm.ex.i2.
            /// DISP: memory dereference with displacement only, instruction.disp.
            /// SMEM: simple memory dereference with optional displacement (a single register memory dereference).
            /// MEM: complex memory dereference (optional fields: s/i/b/disp).
            /// PC: the relative address of a branch instruction (instruction.imm.addr).
            /// PTR: the absolute target address of a far branch instruction (instruction.imm.ptr.seg/off).
            /// </summary>
            public OperandType type;

            /// <summary>
            /// Index of:
            /// REG: holds global register index
            /// SMEM: holds the 'base' register. E.G: [ECX], [EBX+0x1234] are both in operand.index.
            /// MEM: holds the 'index' register. E.G: [EAX*4] is in operand.index.
            /// </summary>
            public byte index;

            /// <summary>
            /// Size of:
            /// REG: register
            /// IMM: instruction.imm
            /// IMM1: instruction.imm.ex.i1
            /// IMM2: instruction.imm.ex.i2
            /// DISP: instruction.disp
            /// SMEM: size of indirection.
            /// MEM: size of indirection.
            /// PC: size of the relative offset
            /// PTR: size of instruction.imm.ptr.off (16 or 32)
            /// </summary>
            public ushort size;

            /// <summary>
            /// Gets the name of the register associated with this operand in lowercase.
            /// </summary>
            public string RegisterName
            {
                get
                {
                    if (this.Register.HasValue)
                    {
                        return this.Register.Value.ToString().ToLower();
                    }
                    else
                    {
                        return string.Empty;
                    }
                }
            }

            /// <summary>
            /// Gets the type of register associated with this operand if the operand is of type REG or SMEM.
            /// </summary>
            public RegisterType? Register
            {
                get
                {
                    if (this.type == OperandType.REG || this.type == OperandType.SMEM)
                    {
                        return (RegisterType)this.index;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        /// <summary>
        /// Used by PTR.
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
        /// Used by IMM1 (i1) and IMM2 (i2). ENTER instruction only.
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
            /// The value, as an address. Used by PC: (Use GET_TARGET_ADDR).
            /// </summary>
            [FieldOffset(0)]
            public ulong addr;

            /// <summary>
            /// The value, as a pointer. Used by PTR.
            /// </summary>
            [FieldOffset(0)]
            public _Value_ptr ptr;

            /// <summary>
            /// Used by IMM1 (i1) and IMM2 (i2). ENTER instruction only.
            /// </summary>
            [FieldOffset(0)]
            public _Value_ex ex;
        }

        /// <summary>
        /// Represents the new decoded instruction, used by the decompose interface.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct DInst
        {
            /// <summary>
            /// The immediate value of the instruction.
            /// Used by ops[n].type == IMM/IMM1&amp;IMM2/PTR/PC. Its size is ops[n].size.
            /// </summary>
            public _Value imm;

            /// <summary>
            /// Used by ops[n].type == SMEM/MEM/DISP. Its size is dispSize.
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
            /// Up to four operands per instruction, ignored if ops[n].type == NONE.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.I4, SizeConst = OPERANDS_NO)]
            public Operand[] ops;

            /// <summary>
            /// Size of the whole instruction.
            /// </summary>
            public byte size;

            /// <summary>
            /// Segment information of memory indirection, default segment, or overridden one, can be -1. Use SEGMENT
            /// macros.
            /// </summary>
            public byte segment;

            /// <summary>
            /// Used by ops[n].type == MEM. Base global register index (might be R_NONE), scale size (2/4/8), ignored
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

            /// <summary>
            /// Gets the instruction type, using this instruction's opcode value.
            /// </summary>
            public InstructionType InstructionType
            {
                get
                {
                    return (InstructionType)this.opcode;
                }
            }

            /// <summary>
            /// Gets or sets the Instruction-Set-Class type of the instruction. (INTEGER, FPU, and many more.)
            /// </summary>
            /// <remarks>This combines the META_GET_ISC/META_SET_ISC macros in distorm.h.</remarks>
            public InstructionSetClass ISC
            {
                get
                {
                    return (InstructionSetClass)((this.meta >> 3) & 0x1f);
                }

                set
                {
                    this.meta |= (byte)((short)value << 3);
                }
            }

            /// <summary>
            /// Gets the flow control flags of the instruction.
            /// </summary>
            /// <returns>Returns the control flow flag value.</returns>
            public FlowControl FlowControlFlags
            {
                get
                {
                    return (FlowControl)(this.meta & 0x7);
                }
            }

            /// <summary>
            /// Gets the target address of a branching instruction.
            /// </summary>
            /// <remarks>This is the INSTRUCTION_GET_TARGET macro in distorm.h</remarks>
            public ulong BranchTarget
            {
                get
                {
                    return this.addr + this.imm.addr + this.size;
                }
            }

            /// <summary>
            /// Gets the target address of a RIP-relative memory indirection.
            /// </summary>
            /// <remarks>This is the INSTRUCTION_GET_RIP_TARGET macro in distorm.h.</remarks>
            public ulong RipTarget
            {
                get
                {
                    return this.addr + this.disp + this.size;
                }
            }

            /// <summary>
            /// Gets or sets the operand size, which uses this instruction's 'flags' value.
            /// Returns the operand size: 0 - 16 bits / 1 - 32 bits / 2 - 64 bits / 3 reserved
            /// </summary>
            /// <remarks>This is the FLAG_GET_OPSIZE macro in distorm.h.</remarks>
            public byte OperandSize
            {
                get
                {
                    return (byte)((this.flags >> 8) & 3);
                }

                set
                {
                    this.flags |= (ushort)((value & 3) << 8);
                }
            }

            /// <summary>
            /// Gets or sets the address size, which uses this instruction's 'flags' value.
            /// Returns the address size: 0 - 16 bits / 1 - 32 bits / 2 - 64 bits / 3 reserved
            /// </summary>
            /// <remarks>This is the FLAG_GET_ADDRSIZE macro in distorm.h.</remarks>
            public byte AddressSize
            {
                get
                {
                    return (byte)((this.flags >> 10) & 3);
                }

                set
                {
                    this.flags |= (ushort)((value & 3) << 10);
                }
            }

            /// <summary>
            /// Gets the prefix of an instruction, which uses this instruction's 'flags' value.
            /// Returns the prefix of an instruction (FLAG_LOCK, FLAG_REPNZ, FLAG_REP).
            /// </summary>
            /// <remarks>This is the FLAG_GET_PREFIX macro in distorm.h.</remarks>
            public InstructionFlags Prefix
            {
                get
                {
                    return (InstructionFlags)(this.flags & 7);
                }
            }

            /// <summary>
            /// Gets or sets the segment value of an instruction.
            /// </summary>
            /// <remarks>This combines the SEGMENT_GET/SEGMENT_SET macros in distorm.h.</remarks>
            public byte Segment
            {
                get
                {
                    return this.segment == R_NONE ? R_NONE : (byte)(this.segment & 0x7f);
                }

                set
                {
                    this.segment |= value;
                }
            }

            /// <summary>
            /// Gets a value indicating whether the segment value is set to the default segment value.
            /// Returns true if the segment register is the default one for the operand. For instance:
            /// MOV [EBP], AL - the default segment register is SS. However,
            /// MOV [FS:EAX], AL - The default segment is DS, but we overrode it with FS,
            /// therefore the function will return FALSE.
            /// </summary>
            /// <remarks>This is the SEGMENT_IS_DEFAULT macro in distorm.h.</remarks>
            public bool IsSegmentDefault
            {
                get
                {
                    return (this.segment & SEGMENT_DEFAULT) == SEGMENT_DEFAULT;
                }
            }

            /// <summary>
            /// Gets a value indicating whether if the destination and source sizes in this instruction differ.
            /// </summary>
            private bool DstSrcSizesDiffer
            {
                get
                {
                    if (this.ops[0].type != OperandType.NONE && this.ops[1].type != OperandType.NONE)
                    {
                        if (this.ops[0].size == this.ops[1].size)
                        {
                            return false;
                        }
                        else if (this.ops[0].type == OperandType.REG)
                        {
                            return false;
                        }

                        return true;
                    }
                    else if (this.ops[0].type == OperandType.SMEM || this.ops[0].type == OperandType.MEM)
                    {
                        return true;
                    }

                    return false;
                }
            }

            /// <summary>
            /// Gets the smallest operand size used by this instruction.
            /// </summary>
            private ushort SmallestOperandSize
            {
                get
                {
                    ushort shortestOperandSize;
                    unchecked
                    {
                        shortestOperandSize = (ushort)-1;
                    }

                    foreach (Operand o in this.ops)
                    {
                        if (!o.Register.HasValue)
                        {
                            break;
                        }

                        shortestOperandSize = o.size < shortestOperandSize ? o.size : shortestOperandSize;
                    }

                    return shortestOperandSize;
                }
            }

            /// <summary>
            /// Converts this instruction to a string.
            /// </summary>
            /// <returns>a string representation of this instruction</returns>
            public override string ToString()
            {
                StringBuilder front = new StringBuilder();
                front.Append(this.InstructionType.ToString().ToLower());
                if (this.DstSrcSizesDiffer)
                {
                    front.Append(" " + ((OperandSizes)(this.SmallestOperandSize / 8)).ToString().ToLower());
                }

                StringBuilder back = new StringBuilder();

                for (int i = 0; i < this.ops.Length; ++i)
                {
                    if (this.ops[i].type == OperandType.NONE)
                    {
                        break;
                    }
                    else
                    {
                        if (i == 0)
                        {
                            back.Append(" ");
                        }
                        else
                        {
                            back.Append(", ");
                        }

                        if (this.ops[i].type == OperandType.REG)
                        {
                            back.Append(this.ops[i].RegisterName);
                        }
                        else if (this.ops[i].type == OperandType.IMM)
                        {
                            back.Append("0x" + this.imm.qword.ToString("x"));
                        }
                        else if (this.ops[i].type == OperandType.SMEM)
                        {
                            if (this.ops[i].Register.HasValue && this.ops[i].Register.Value == RegisterType.RIP)
                            {
                                back.Append("[" + "0x" + this.RipTarget.ToString("x") + "]");
                            }
                            else
                            {
                                long signedDisp = (long)this.disp;
                                back.Append("[" + this.ops[i].RegisterName);
                                if (signedDisp < 0)
                                {
                                    signedDisp = Math.Abs(signedDisp);
                                    back.Append("-0x" + signedDisp.ToString("x") + "]");
                                }
                                else
                                {
                                    back.Append("+0x" + this.disp.ToString("x") + "]");
                                }
                            }
                        }
                        else if (this.ops[i].type == OperandType.IMM1)
                        {
                            back.Append("IMM1");
                        }
                        else if (this.ops[i].type == OperandType.IMM2)
                        {
                            back.Append("IMM2");
                        }
                        else if (this.ops[i].type == OperandType.MEM)
                        {
                            back.Append("MEM");
                        }
                        else if (this.ops[i].type == OperandType.PC)
                        {
                            back.Append("PC");
                        }
                        else if (this.ops[i].type == OperandType.PTR)
                        {
                            back.Append("PTR");
                        }
                        else if (this.ops[i].type == OperandType.DISP)
                        {
                            back.Append("DISP");
                        }
                    }
                }

                return front.ToString() + back.ToString();
            }
        }

        /// <summary>
        /// Holds various pieces of information that are required by the distorm_decompose function.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct CodeInfo
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
            public DecodeType dt;

            /// <summary>
            /// Features that should be enabled during decomposition.
            /// </summary>
            public DecomposeFeatures features;
        }

        /// <summary>
        /// A mnemonic string representation.
        /// </summary>
        public struct WMnemonic
        {
            /// <summary>
            /// The length of the mnemonic string.
            /// </summary>
            public char length;

            /// <summary>
            /// A null terminated string, which contains 'length' characters.
            /// </summary>
            public char[] p; // len = 1
        }

        /// <summary>
        /// A register string representation.
        /// </summary>
        public struct WRegister
        {
            /// <summary>
            /// The length of the register string.
            /// </summary>
            public uint length;

            /// <summary>
            /// A null terminated string.
            /// </summary>
            public char[] p; // len = 6

            /// <summary>
            /// Initializes a new instance of the WRegister struct.
            /// </summary>
            /// <param name="length">The length of the register string to be created.</param>
            /// <param name="p">The array of characters that holds the register name.</param>
            public WRegister(uint length, string p)
            {
                this.length = length;
                this.p = p.ToCharArray();
            }
        }

        #endregion
    }
}
