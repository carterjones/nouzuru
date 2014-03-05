namespace Nouzuru
{
    using System;
    using System.Collections.Generic;
    using Bunseki;
    using Distorm3cs;

    /// <summary>
    /// Represents a memory page.
    /// </summary>
    public class Page
    {
        /// <summary>
        /// Gets or sets the base address of the memory page.
        /// </summary>
        public IntPtr Address { get; set; }

        /// <summary>
        /// Gets or sets the data stored within the memory page.
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// Gets or sets the instructions found within the memory page.
        /// </summary>
        public List<Instruction> Instructions { get; set; }

        /// <summary>
        /// Gets or sets the disassembled instructions found within the memory page.
        /// </summary>
        public List<string> InstructionsDisassembled { get; set; }

        /// <summary>
        /// Gets or sets the decomposed instructions found within the memory page.
        /// </summary>
        public Distorm.DInst[] InstructionsDecomposed { get; set; }

        /// <summary>
        /// Gets the size of the memory page.
        /// </summary>
        public uint Size
        {
            get { return (uint)this.Data.Length; }
        }

        public bool Disassemble(Distorm.DecodeType decodeType)
        {
            if (this.Data.Length == 0)
            {
                return false;
            }

            if (this.InstructionsDisassembled.Count != 0)
            {
                this.InstructionsDisassembled = Distorm.Disassemble(this.Data, (ulong)Address.ToInt64(), decodeType);
                if (this.InstructionsDisassembled.Count == 0)
                {
                    return false;
                }
            }

            if (this.InstructionsDecomposed.Length != 0)
            {
                this.InstructionsDecomposed = Distorm.Decompose(this.Data, (ulong)Address.ToInt64(), decodeType);
                if (this.InstructionsDecomposed.Length == 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
