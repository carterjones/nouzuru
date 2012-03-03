namespace Nouzuru
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Distorm3cs;

    /// <summary>
    /// A basic block of instructions that contain, at most, one instruction that affects the flow of code.
    /// </summary>
    public class BasicBlock
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the BasicBlock class.
        /// </summary>
        public BasicBlock()
        {
            this.Instructions = new List<Distorm.DInst>();
            this.Previous = new List<BasicBlock>();
            this.Next = new List<BasicBlock>();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the list of instructions in this basic block.
        /// </summary>
        public List<Distorm.DInst> Instructions { get; set; }

        /// <summary>
        /// Gets or sets a list of basic blocks that connects to this basic block.
        /// </summary>
        public List<BasicBlock> Previous { get; set; }

        /// <summary>
        /// Gets or sets a list of basic blocks that this basic block connects to.
        /// </summary>
        public List<BasicBlock> Next { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Generates a new basic block that starts at the supplied base address.
        /// </summary>
        /// <param name="p">The process interactor used to read data from the target.</param>
        /// <param name="baseAddress">The base address of the basic block.</param>
        /// <param name="pages">A set of pages that have been read while analyzing basic blocks.</param>
        /// <returns>Returns a basic block that starts at the supplied address.</returns>
        /// <remarks>
        /// Unhandled ends to basic blocks (descriptions taken from wikipedia):
        /// - Instructions which may throw an exception
        /// - Function calls can be at the end of a basic block if they may not return, such as functions which throw
        ///   exceptions or special calls like C's longjmp and exit
        /// Unhandled beginnings to basic blocks (descriptions taken from wikipedia):
        /// - Instructions following ones that throw exceptions
        /// - Exception handlers.
        /// </remarks>
        public static BasicBlock GenerateBlock(PInteractor p, IntPtr baseAddress, ref List<Page> pages)
        {
            // TODO: Add some type of mechanism to track all basic blocks as a unique list/set, so that duplicates
            //       will not be added.

            // Prepare the base block.
            BasicBlock block = new BasicBlock();

            Page currentPage = null;
            IntPtr pageBase = new IntPtr((baseAddress.ToInt64() / SysInteractor.PageSize) * SysInteractor.PageSize);
            foreach (Page page in pages)
            {
                if (page.Address.Equals(pageBase))
                {
                    currentPage = page;
                    break;
                }
            }

            if (currentPage == null)
            {
                currentPage = new Page();
                currentPage.Address = pageBase;
                currentPage.Data = new byte[SysInteractor.PageSize];

                if (!p.Read(currentPage.Address, currentPage.Data))
                {
                    return new BasicBlock();
                }

                currentPage.Instructions = Distorm.Decompose(currentPage.Data, (ulong)currentPage.Address.ToInt64());
                if (currentPage.Instructions.Length == 0)
                {
                    return new BasicBlock();
                }
            }

            BasicBlock currentBlock = block;

            int i = 0;
            for (; i < currentPage.Instructions.Length; ++i)
            {
                if (currentPage.Instructions[i].addr == (ulong)baseAddress.ToInt64())
                {
                    // Add the instruction at the base address.
                    currentBlock.Instructions.Add(currentPage.Instructions[i++]);
                    break;
                }
            }

            for (; i < currentPage.Instructions.Length; ++i)
            {
                // Verify that no control flow instructions are added in the middle of a basic block.
                while (
                    currentPage.Instructions[i].FlowControlFlags != Distorm.FlowControl.CND_BRANCH &&
                    currentPage.Instructions[i].FlowControlFlags != Distorm.FlowControl.RET &&
                    currentPage.Instructions[i].FlowControlFlags != Distorm.FlowControl.UNC_BRANCH)
                {
                    // Add the non-control-modifying instructions.
                    currentBlock.Instructions.Add(currentPage.Instructions[i++]);
                }

                // Add the control-modifying instruction.
                currentBlock.Instructions.Add(currentPage.Instructions[i]);

                if (currentPage.Instructions[i].FlowControlFlags == Distorm.FlowControl.RET)
                {
                    break;
                }

                // Recursive step.
                BasicBlock forkedBlock =
                    GenerateBlock(p, new IntPtr((long)currentPage.Instructions[i].addr), ref pages);
                currentBlock.Next.Add(forkedBlock);
                forkedBlock.Previous.Add(currentBlock);

                // Add the fall-through block.
                BasicBlock nextBlock = new BasicBlock();
                currentBlock.Next.Add(nextBlock);
                nextBlock.Previous.Add(currentBlock);

                currentBlock = nextBlock;
            }

            // Return results.
            return block;
        }

        #endregion
    }
}
