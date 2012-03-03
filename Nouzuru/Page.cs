namespace Nouzuru
{
    using System;
    using System.Collections.Generic;
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
        /// Gets or sets the decomposed instructions found within the memory page.
        /// </summary>
        public Distorm.DInst[] Instructions { get; set; }

        /// <summary>
        /// Gets the size of the memory page.
        /// </summary>
        public uint Size
        {
            get { return (uint)this.Data.Length; }
        }
    }
}
