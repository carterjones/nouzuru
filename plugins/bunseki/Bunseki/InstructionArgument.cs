namespace Bunseki
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using BeaEngineCS;

    /// <summary>
    /// Represents an argument of an instruction.
    /// </summary>
    public class InstructionArgument
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the InstructionArgument class.
        /// </summary>
        internal InstructionArgument()
        {
            this.Mnemonic = "invalid argument";
            this.AffectsMemory = false;
        }

        /// <summary>
        /// Initializes a new instance of the InstructionArgument class based on an argument.
        /// </summary>
        /// <param name="arg">the instruction argument</param>
        internal InstructionArgument(BeaEngine.ARGTYPE arg)
        {
            this.Mnemonic = arg.ArgMnemonic;
            this.AffectsMemory = arg.Details.HasFlag(BeaEngine.ArgumentDetails.MEMORY_TYPE);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the mnemonic of the instruction.
        /// </summary>
        public string Mnemonic { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the instruction argument affects memory.
        /// </summary>
        public bool AffectsMemory { get; private set; }

        #endregion
    }
}
