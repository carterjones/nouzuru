namespace Bunseki
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using BeaEngineCS;

    /// <summary>
    /// A generic disassembler that wraps around other disassembler libraries.
    /// </summary>
    public class Disassembler
    {
        #region Enumerations

        /// <summary>
        /// Various architectures.
        /// </summary>
        public enum Architecture
        {
            /// <summary>
            /// The x86-32 bit architecture.
            /// </summary>
            x86_32 = 1,

            /// <summary>
            /// The x86-64 bit architecture.
            /// </summary>
            x86_64,
        }

        /// <summary>
        /// Various internally used disassembler libraries.
        /// </summary>
        public enum InternalDisassembler
        {
            /// <summary>
            /// The BeaEngine library.
            /// </summary>
            BeaEngine = 1,
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the architecture targeted by the disassembler.
        /// </summary>
        public Architecture TargetArchitecture { get; set; }

        /// <summary>
        /// Gets or sets the engine used to perform disassembly.
        /// </summary>
        public InternalDisassembler Engine { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Disassembles the supplied array of bytes.
        /// </summary>
        /// <param name="data">the bytes to be disassembled</param>
        /// <returns>the list of instruction represented by the supplied byte array</returns>
        public IEnumerable<Instruction> DisassembleInstructions(byte[] data)
        {
            return this.DisassembleInstructions(data, IntPtr.Zero);
        }

        /// <summary>
        /// Disassembles the supplied byte array.
        /// </summary>
        /// <param name="data">the byte array to be disassembled</param>
        /// <param name="virtualAddress">the virtual address of the first byte to be disassembled</param>
        /// <returns>the disassembled instructions</returns>
        public IEnumerable<Instruction> DisassembleInstructions(byte[] data, IntPtr virtualAddress)
        {
            if (this.Engine == InternalDisassembler.BeaEngine)
            {
                BeaEngine.Architecture architecture;
                if (this.TargetArchitecture == Architecture.x86_32)
                {
                    architecture = BeaEngine.Architecture.x86_32;
                }
                else if (this.TargetArchitecture == Architecture.x86_64)
                {
                    architecture = BeaEngine.Architecture.x86_64;
                }
                else
                {
                    architecture = BeaEngine.Architecture.x86_32;
                }

                foreach (BeaEngine._Disasm inst in BeaEngine.Disassemble(data, virtualAddress, architecture))
                {
                    yield return new Instruction(inst);
                }
            }
            else
            {
                yield break;
            }
        }

        #endregion
    }
}
