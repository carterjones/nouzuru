namespace Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime;
    using System.Runtime.InteropServices;
    using System.Text;
    using Distorm3cs;
    using System.IO;

    /// <summary>
    /// Tests various functionality of the distorm3cs interface.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// The code that is used for testing decomposition.
        /// </summary>
        /// <remarks>
        /// This byte sample is available/examined at: http://code.google.com/p/distorm/wiki/Showcases
        /// </remarks>
        private static byte[] code = new byte[] { 0x55, 0x8b, 0xec, 0x8b, 0x45, 0x08, 0x03, 0x45, 0x0c, 0xc9, 0xc3 };
        private static byte[] code2 = new byte[] {
            0x48, 0x89, 0x74, 0x24, 0x08, 0x48, 0x89, 0x7C, 0x24, 0x10, 0x41, 0x54, 0x48, 0x81, 0xEC, 0xB0, 0x00,
            0x00, 0x00, 0x83, 0x64, 0x24, 0x20, 0x00, 0x48, 0x8D, 0x4C, 0x24, 0x40, 0xFF, 0x15, 0x95, 0x68, 0x04,
            0x00, 0x48, 0x8D, 0x57, 0xE8 };

        /// <summary>
        /// Tests the decomposition of a resulting array that has parsed the test code in this class.
        /// </summary>
        /// <param name="result">The parsed results.</param>
        /// <returns>Returns true if the results have been parsed as expected.</returns>
        public static bool VerifyDecomposition(Distorm.DInst[] result)
        {
            if (result.Length < 6)
            {
                return false;
            }

            // Manually check each instruction.
            if (result[0].InstructionType != Distorm.InstructionType.PUSH ||
                result[0].ops[0].RegisterName != "ebp")
            {
                return false;
            }
            else if (result[1].InstructionType != Distorm.InstructionType.MOV ||
                result[1].ops[0].RegisterName != "ebp" ||
                result[1].ops[1].RegisterName != "esp")
            {
                return false;
            }
            else if (result[2].InstructionType != Distorm.InstructionType.MOV ||
                result[2].ops[0].RegisterName != "eax" ||
                result[2].ops[1].type != Distorm.OperandType.SMEM ||
                result[2].ops[1].RegisterName != "ebp" ||
                result[2].disp != 0x8)
            {
                return false;
            }
            else if (result[3].InstructionType != Distorm.InstructionType.ADD ||
                result[3].ops[0].RegisterName != "eax" ||
                result[3].ops[1].type != Distorm.OperandType.SMEM ||
                result[3].ops[1].RegisterName != "ebp" ||
                result[3].disp != 0xc)
            {
                return false;
            }
            else if (result[4].InstructionType != Distorm.InstructionType.LEAVE)
            {
                return false;
            }
            else if (result[5].InstructionType != Distorm.InstructionType.RET)
            {
                return false;
            }

            return true;
        }

        public static bool DecomposeTest3()
        {
            byte[] testCode = {
                0x8B, 0x89, 0xC8, 0x02, 0x00, 0x00, 0x8B, 0x51, 0x64, 0xF3, 0x0F, 0x10, 0x4A, 0x64, 0x0F, 0x2F, 0xC8,
                0x8B, 0x86, 0x44, 0x04, 0x00, 0x00, 0xF3, 0x0F, 0x10, 0x88, 0x34, 0x02, 0x00, 0x00, 0xF3, 0x0F, 0x5C,
                0xC1, 0xF3, 0x0F, 0x11, 0x44, 0x24, 0x18, 0x8B, 0x56, 0x60, 0x8B, 0x46, 0x64 };
            string expectedOutput =
                "mov ecx, [ecx+0x2c8]" +
                "mov edx, [ecx+0x64]" +
                "movss xmm1, [edx+0x64]" +
                "comiss xmm1, xmm0" +
                "mov eax, [esi+0x444]" +
                "movss xmm1, [eax+0x234]" +
                "subss xmm0, xmm1" +
                "movss [esp+0x18], xmm0" +
                "mov edx, [esi+0x60]" +
                "mov eax, [esi+0x64]";

            Distorm.DInst[] insts = Distorm.Decompose(testCode, 0, Distorm.DecodeType.Decode32Bits);
            string actualOutput = string.Join(string.Empty, insts.Select(x => x.ToString()));
            return expectedOutput.Equals(actualOutput);
        }

        public static bool VerifyDecomposition2(Distorm.DInst[] result)
        {
            string expectedOutput = "mov [rsp+0x8], rsi\n" +
                        "mov [rsp+0x10], rdi\n" +
                        "push r12\n" +
                        "sub rsp, 0xb0\n" +
                        "and dword [rsp+0x20], 0x0\n" +
                        "lea rcx, [rsp+0x40]\n" +
                        "call qword [0xffbf2288]\n" +
                        "lea rdx, [rdi-0x18]\n";

            if (result.Length < 7)
            {
                return false;
            }

            List<string> instructions = new List<string>();

            foreach (Distorm.DInst inst in result)
            {
                instructions.Add(inst.ToString());
            }

            //List<string> instructions2 = Distorm.Disassemble(Program.code2, 0xFFBAB9D0, Distorm.DecodeType.Decode64Bits);

            return expectedOutput.Equals(string.Join("\n", instructions) + "\n");
        }

        public static bool DecomposeFormatTest2()
        {
            string actualOutput = string.Empty;

            GCHandle gch = GCHandle.Alloc(Program.code2, GCHandleType.Pinned);

            // Prepare the _CodeInfo structure for decomposition.
            Distorm.CodeInfo ci = new Distorm.CodeInfo();
            ci.codeLen = Program.code2.Length;
            ci.code = gch.AddrOfPinnedObject();
            ci.codeOffset = 0xFFBAB9D0;
            ci.dt = Distorm.DecodeType.Decode64Bits;
            ci.features = Distorm.DecomposeFeatures.NONE;
            
            // Prepare the result instruction buffer to receive the decomposition.
            Distorm.DInst[] result = new Distorm.DInst[Program.code2.Length];
            uint usedInstructionsCount = 0;

            // Perform the decomposition.
            Distorm.DecodeResult r =
                Distorm.distorm_decompose(ref ci, result, (uint)result.Length, ref usedInstructionsCount);

            // Release the handle pinned to the code.
            gch.Free();

            // Return false if an error occured during decomposition.
            if (!r.Equals(Distorm.DecodeResult.SUCCESS))
            {
                return false;
            }

            Array.Resize(ref result, (int)usedInstructionsCount);

            return VerifyDecomposition2(result);
        }

        /// <summary>
        /// Tests both the distorm_decompose and distorm_format functions.
        /// </summary>
        /// <returns>Returns true if both tests passed.</returns>
        public static bool DecomposeFormatTest()
        {
            string expectedOutput = "push ebp\n" +
                                    "mov ebp, esp\n" +
                                    "mov eax, [ebp+0x8]\n" +
                                    "add eax, [ebp+0xc]\n" +
                                    "leave\n" +
                                    "ret\n";
            string actualOutput = string.Empty;

            GCHandle gch = GCHandle.Alloc(Program.code, GCHandleType.Pinned);

            // Prepare the _CodeInfo structure for decomposition.
            Distorm.CodeInfo ci = new Distorm.CodeInfo();
            ci.codeLen = Program.code.Length;
            ci.code = gch.AddrOfPinnedObject();
            ci.codeOffset = 0;
            ci.dt = Distorm.DecodeType.Decode32Bits;
            ci.features = Distorm.DecomposeFeatures.NONE;
            
            // Prepare the result instruction buffer to receive the decomposition.
            Distorm.DInst[] result = new Distorm.DInst[Program.code.Length];
            uint usedInstructionsCount = 0;

            // Perform the decomposition.
            Distorm.DecodeResult r =
                Distorm.distorm_decompose(ref ci, result, (uint)result.Length, ref usedInstructionsCount);

            // Release the handle pinned to the code.
            gch.Free();

            // Return false if an error occured during decomposition.
            if (!r.Equals(Distorm.DecodeResult.SUCCESS))
            {
                return false;
            }

            // Prepare a _DecodedInst structure for formatting the results.
            Distorm.DecodedInst inst = new Distorm.DecodedInst();

            for (uint i = 0; i < usedInstructionsCount; ++i)
            {
                // Format the results of the decomposition.
                Distorm.distorm_format(ref ci, ref result[i], ref inst);

                // Add it to the buffer to be verified.
                if (string.IsNullOrEmpty(inst.Operands))
                {
                    actualOutput += inst.Mnemonic + "\n";
                }
                else
                {
                    actualOutput += inst.Mnemonic + " " + inst.Operands + "\n";
                }
            }

            return expectedOutput.Equals(actualOutput);
        }

        /// <summary>
        /// Tests the DistormSimple.Disassemble function.
        /// </summary>
        /// <returns>Returns true if the test passed.</returns>
        public static bool DisassembleTest()
        {
            string expectedOutput = "push ebp\n" +
                                    "mov ebp, esp\n" +
                                    "mov eax, [ebp+0x8]\n" +
                                    "add eax, [ebp+0xc]\n" +
                                    "leave\n" +
                                    "ret\n";

            List<string> instructions = Distorm.Disassemble(Program.code);

            return expectedOutput.Equals(string.Join("\n", instructions) + "\n");
        }

        /// <summary>
        /// Tests the DistormSimple.distorm_decompose() function.
        /// </summary>
        /// <returns>Returns true if the test passed.</returns>
        public static bool DecomposeOnlyTest()
        {
            GCHandle gch = GCHandle.Alloc(Program.code, GCHandleType.Pinned);

            Distorm.CodeInfo ci = new Distorm.CodeInfo();
            ci.codeLen = Program.code.Length;
            ci.code = gch.AddrOfPinnedObject();
            ci.codeOffset = 0;
            ci.dt = Distorm.DecodeType.Decode32Bits;
            ci.features = Distorm.DecomposeFeatures.NONE;

            Distorm.DInst[] result = new Distorm.DInst[Program.code.Length];
            uint usedInstructionsCount = 0;

            Distorm.DecodeResult r =
                Distorm.distorm_decompose(ref ci, result, (uint)result.Length, ref usedInstructionsCount);

            // Release the handle pinned to the code.
            gch.Free();

            // Return false if an error occured during decomposition.
            if (!r.Equals(Distorm.DecodeResult.SUCCESS))
            {
                return false;
            }

            if (usedInstructionsCount < 6)
            {
                return false;
            }

            // Manually check each instruction.
            if (result[0].InstructionType != Distorm.InstructionType.PUSH ||
                result[0].ops[0].RegisterName != "ebp")
            {
                return false;
            }
            else if (result[1].InstructionType != Distorm.InstructionType.MOV ||
                result[1].ops[0].RegisterName != "ebp" ||
                result[1].ops[1].RegisterName != "esp")
            {
                return false;
            }
            else if (result[2].InstructionType != Distorm.InstructionType.MOV ||
                result[2].ops[0].RegisterName != "eax" ||
                result[2].ops[1].type != Distorm.OperandType.SMEM ||
                result[2].ops[1].RegisterName != "ebp" ||
                result[2].disp != 0x8)
            {
                return false;
            }
            else if (result[3].InstructionType != Distorm.InstructionType.ADD ||
                result[3].ops[0].RegisterName != "eax" ||
                result[3].ops[1].type != Distorm.OperandType.SMEM ||
                result[3].ops[1].RegisterName != "ebp" ||
                result[3].disp != 0xc)
            {
                return false;
            }
            else if (result[4].InstructionType != Distorm.InstructionType.LEAVE)
            {
                return false;
            }
            else if (result[5].InstructionType != Distorm.InstructionType.RET)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Tests the DistormSimple.Decompose() function.
        /// </summary>
        /// <returns>Returns true if the test passed.</returns>
        public static bool DecomposeWrapperTest()
        {
            Distorm.DInst[] result = Distorm.Decompose(Program.code);

            return Program.VerifyDecomposition(result);
        }

        /// <summary>
        /// Tests the DistormSimple.Decompose() function, but with an incomplete code buffer. This assumes that the
        /// DistormSimple.Decompose() function works properly with a properly made code buffer.
        /// </summary>
        /// <returns>Returns true if the test passed.</returns>
        public static bool DecomposeWrapperIncompleteCodeTest()
        {
            byte[] incompleteCode = new byte[Program.code.Length];
            Array.Copy(code, incompleteCode, incompleteCode.Length - 1);

            // Set the last byte to the first part of a "mov ebp, esp" instruction.
            incompleteCode[incompleteCode.Length - 1] = 0x8b;

            Distorm.DInst[] insts = Distorm.Decompose(incompleteCode);
            if (insts.Length < 6)
            {
                return false;
            }
            else if (insts[5].InstructionType != Distorm.InstructionType.UNDEFINED)
            {
                return false;
            }

            return true;
        }

        public static List<byte[]> ReadInputs()
        {
            List<byte[]> inputs = new List<byte[]>();
            string inputFile = "tests\\inputs.txt";
            string[] lines = File.ReadAllLines(inputFile);

            // Parse each line from the test inputs file.
            foreach (string line in lines)
            {
                // Extract the byte array.
                List<byte> bytes = new List<byte>();
                string[] byteParts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string bytePart in byteParts)
                {
                    string plainPart = bytePart.Replace("0x", string.Empty);
                    bytes.Add(byte.Parse(plainPart, System.Globalization.NumberStyles.HexNumber));
                }

                // Add the byte array as a single test input.
                inputs.Add(bytes.ToArray());
            }

            return inputs;
        }

        public static List<string> ReadExpectedOutputs()
        {
            string outputFile = "tests\\expected_outputs.txt";
            return File.ReadAllLines(outputFile).ToList();
        }

        public static bool TestInput(byte[] input, string expectedOutput)
        {
            Distorm.DInst[] insts = Distorm.Decompose(input);
            if (insts.Length != 1)
            {
                return false;
            }

            return insts[0].ToString().Equals(expectedOutput);
        }

        public static void RunTestsFromFile(bool showPasses)
        {
            List<byte[]> inputs = Program.ReadInputs();
            List<string> expectedOutputs = Program.ReadExpectedOutputs();
            if (inputs.Count != expectedOutputs.Count)
            {
                throw new Exception("Number of input test cases does not match number of expected outputs count.");
            }

            for (int i = 0; i < inputs.Count; ++i)
            {
                StringBuilder message = new StringBuilder();
                message.Append("Test " + i + ": ");
                if (Program.TestInput(inputs[i], expectedOutputs[i]))
                {
                    if (showPasses)
                    {
                        message.Append("Passed");
                        Console.WriteLine(message.ToString());
                    }
                }
                else
                {
                    message.Append("Failed");
                    Console.WriteLine(message.ToString());
                }
            }
        }

        /// <summary>
        /// Runs the collection of tests of the distorm3cs interface.
        /// </summary>
        /// <param name="args">Command line arguments passed to the program.</param>
        private static void Main(string[] args)
        {
            bool result = true;
            bool tmpResult = false;

            Program.RunTestsFromFile(false);

            result &= tmpResult = Program.DecomposeFormatTest();
            Console.WriteLine("DecomposeFormatTest():                " + (tmpResult ? "Passed" : "Failed"));
            result &= tmpResult = Program.DecomposeFormatTest2();
            Console.WriteLine("DecomposeFormatTest2():               " + (tmpResult ? "Passed" : "Failed"));
            result &= tmpResult = Program.DecomposeTest3();
            Console.WriteLine("DecomposeTest3():                     " + (tmpResult ? "Passed" : "Failed"));
            result &= tmpResult = Program.DisassembleTest();
            Console.WriteLine("DisassembleTest():                    " + (tmpResult ? "Passed" : "Failed"));
            result &= tmpResult = Program.DecomposeOnlyTest();
            Console.WriteLine("DecomposeOnlyTest():                  " + (tmpResult ? "Passed" : "Failed"));
            result &= tmpResult = Program.DecomposeWrapperTest();
            Console.WriteLine("DecomposeWrapperTest():               " + (tmpResult ? "Passed" : "Failed"));
            result &= tmpResult = Program.DecomposeWrapperIncompleteCodeTest();
            Console.WriteLine("DecomposeWrapperIncompleteCodeTest(): " + (tmpResult ? "Passed" : "Failed"));

            Console.WriteLine("--------------------------------------------");
            Console.WriteLine("End result:                           " + (result ? "All passed" : "Not all passed"));

            Console.WriteLine();
            Console.WriteLine("Press any key to continue.");
            Console.ReadKey();
        }
    }
}
