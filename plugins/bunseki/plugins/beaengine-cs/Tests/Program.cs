namespace Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using BeaEngineCS;

    /// <summary>
    /// Tests various functionality of the distorm3cs interface.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// Runs the collection of tests of the BeaEngineCS interface.
        /// </summary>
        /// <param name="args">Command line arguments passed to the program.</param>
        public static void Main(string[] args)
        {
            bool result = true;
            bool tmpResult = false;

            result &= tmpResult = Program.VersionTest();
            Console.WriteLine("VersionTest():  " + (tmpResult ? "Passed" : "Failed"));
            result &= tmpResult = Program.RevisionTest();
            Console.WriteLine("RevisionTest(): " + (tmpResult ? "Passed" : "Failed"));
            result &= tmpResult = Program.DisasmTest();
            Console.WriteLine("DisasmTest():   " + (tmpResult ? "Passed" : "Failed"));

            Console.WriteLine("--------------------------------------------");
            Console.WriteLine("End result:     " + (result ? "All passed" : "Not all passed"));

            Console.WriteLine();
            Console.WriteLine("Press any key to continue.");
            Console.ReadKey();
        }

        /// <summary>
        /// Tests the BeaEngineCS.BeaEngineVersion() function.
        /// </summary>
        /// <returns>Returns true if the test passed.</returns>
        private static bool VersionTest()
        {
            return BeaEngine.Version.Equals("4.1");
        }

        /// <summary>
        /// Tests the BeaEngineCS.BeaEngineRevision() function.
        /// </summary>
        /// <returns>Returns true if the test passed.</returns>
        private static bool RevisionTest()
        {
            return BeaEngine.Revision.Equals("172");
        }

        /// <summary>
        /// Tests the BeaEngineCS.Disasm() function.
        /// </summary>
        /// <returns>Returns true if the test passed.</returns>
        private static bool DisasmTest()
        {
            int dataSize = 0x100;
            IntPtr data = Marshal.AllocHGlobal(dataSize);
            for (int i = 0; i < dataSize; ++i)
            {
                Marshal.WriteByte(IntPtr.Add(data, i), 0);
            }

            BeaEngine._Disasm inst = new BeaEngine._Disasm();
            inst.InstructionPointer = (UIntPtr)data.ToInt64();
            int len = BeaEngine.Disassemble(ref inst);
            if (len == BeaEngine.UnknownOpcode)
            {
                Console.Error.WriteLine("Unknown opcode.");
                return false;
            }
            else if (len == BeaEngine.OutOfBlock)
            {
                Console.Error.WriteLine("Out of block.");
                return false;
            }
            else
            {
                return inst.CompleteInstr.Equals("add byte ptr [eax], al");
            }
        }
    }
}
