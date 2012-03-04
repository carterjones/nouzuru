namespace Nouzuru
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using Distorm3cs;

    /// <summary>
    /// A basic block of instructions that contain, at most, one instruction that affects the flow of code.
    /// </summary>
    public class BasicBlock
    {
        #region Fields

        /// <summary>
        /// A counter that stores the number of basic blocks that have been made. This is used for generating a unique
        /// ID for each basic block.
        /// </summary>
        private static ulong numBlocksMade = 0;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the BasicBlock class.
        /// </summary>
        public BasicBlock()
        {
            this.InstructionsDecomposed = new List<Distorm.DInst>();
            this.InstructionsDisassembled = new List<string>();
            this.Previous = new List<BasicBlock>();
            this.Next = new List<BasicBlock>();
            this.ID = numBlocksMade++;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the list of decomposed instructions in this basic block.
        /// </summary>
        public List<Distorm.DInst> InstructionsDecomposed { get; set; }

        /// <summary>
        /// Gets or sets the list of disassembled instructions in this basic block.
        /// </summary>
        public List<string> InstructionsDisassembled { get; set; }

        /// <summary>
        /// Gets or sets a list of basic blocks that connects to this basic block.
        /// </summary>
        public List<BasicBlock> Previous { get; set; }

        /// <summary>
        /// Gets or sets a list of basic blocks that this basic block connects to.
        /// </summary>
        public List<BasicBlock> Next { get; set; }

        /// <summary>
        /// Gets the unique ID of this basic block.
        /// </summary>
        public ulong ID { get; private set; }

        #endregion

        #region Methods
        
        /// <summary>
        /// Generates a new basic block that starts at the supplied base address.
        /// </summary>
        /// <param name="p">The process interactor used to read data from the target.</param>
        /// <param name="baseAddress">The base address of the basic block.</param>
        /// <param name="blocks">
        /// A set of basic blocks that relate to the current block through either direct or indirect connections.
        /// </param>
        /// <param name="maxDepth">
        /// The maximum number of code flow indirections to be followed. If set to -1, no maximum indirection count is
        /// enforced.
        /// </param>
        /// <returns>Returns a basic block that starts at the supplied address.</returns>
        public static BasicBlock GenerateBlock(
            PInteractor p,
            IntPtr baseAddress,
            out HashSet<BasicBlock> blocks,
            long maxDepth = -1)
        {
            SysInteractor.Init();
            blocks = new HashSet<BasicBlock>();
            HashSet<Page> pages = new HashSet<Page>();

            BasicBlock block = BasicBlock.GenerateBlock(p, baseAddress, ref blocks, ref pages, maxDepth);
            BasicBlock.RemoveDuplicateInstructions(ref blocks);

            return block;
        }

        /// <summary>
        /// Generates a new basic block that starts at the supplied base address.
        /// </summary>
        /// <param name="p">The process interactor used to read data from the target.</param>
        /// <param name="baseAddress">The base address of the basic block.</param>
        /// <param name="blocks">
        /// A set of basic blocks that relate to the current block through either direct or indirect connections.
        /// </param>
        /// <param name="pages">A set of pages that have been read while analyzing basic blocks.</param>
        /// <param name="maxDepth">
        /// The maximum number of code flow indirections to be followed. If set to -1, no maximum indirection count is
        /// enforced.
        /// </param>
        /// <param name="currentDepth">The current number of indirections that have been followed.</param>
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
        public static BasicBlock GenerateBlock(
            PInteractor p,
            IntPtr baseAddress,
            ref HashSet<BasicBlock> blocks,
            ref HashSet<Page> pages,
            long maxDepth = -1,
            long currentDepth = 0)
        {
            // Prepare the base block.
            BasicBlock block = null;

            // If a block at this address already exists, return that block.
            foreach (BasicBlock b in blocks)
            {
                if (b.InstructionsDecomposed[0].addr == (ulong)baseAddress.ToInt64())
                {
                    return b;
                }
            }

            // If the block did not exist, then create a new block.
            if (block == null)
            {
                block = new BasicBlock();
                blocks.Add(block);
            }

            // Get the address of the page where the block exists.
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

            // Read the data from the page where the block exists.
            if (currentPage == null)
            {
                currentPage = new Page();
                currentPage.Address = pageBase;
                currentPage.Data = new byte[SysInteractor.PageSize];

                if (!p.Read(currentPage.Address, currentPage.Data))
                {
                    Console.WriteLine("Unable to read address 0x" + currentPage.Address.ToInt32().ToString("x").PadLeft(8, '0'));
                    Console.WriteLine("Error: " + Marshal.GetLastWin32Error());
                    return new BasicBlock();
                }

                currentPage.InstructionsDecomposed =
                    Distorm.Decompose(currentPage.Data, (ulong)currentPage.Address.ToInt64());
                if (currentPage.InstructionsDecomposed.Length == 0)
                {
                    return new BasicBlock();
                }

                currentPage.InstructionsDisassembled =
                    Distorm.Disassemble(currentPage.Data, (ulong)currentPage.Address.ToInt64());
                if (currentPage.InstructionsDisassembled.Count == 0)
                {
                    return new BasicBlock();
                }

                pages.Add(currentPage);
            }

            int i = 0;
            for (; i < currentPage.InstructionsDecomposed.Length; ++i)
            {
                if (currentPage.InstructionsDecomposed[i].addr == (ulong)baseAddress.ToInt64())
                {
                    // Break once the instruction has been found.
                    break;
                }
                else if (currentPage.InstructionsDecomposed[i].addr > (ulong)baseAddress.ToInt64())
                {
                    Console.WriteLine("Disassembly failed for address: 0x" + currentPage.InstructionsDecomposed[i].addr.ToString("x").PadLeft(8, '0'));
                    return new BasicBlock();
                }
            }

            if (i >= currentPage.InstructionsDecomposed.Length)
            {
                Console.WriteLine("Need to use scanner to identify and decompose regions, rather than single pages.");
                return new BasicBlock();
            }

            // Verify that no control flow instructions are added in the middle of a basic block.
            while (
                !(currentPage.InstructionsDecomposed[i].FlowControlFlags == Distorm.FlowControl.CALL &&
                  currentPage.InstructionsDecomposed[i].ops[0].type == Distorm.OperandType.PC) &&
                currentPage.InstructionsDecomposed[i].FlowControlFlags != Distorm.FlowControl.CND_BRANCH &&
                currentPage.InstructionsDecomposed[i].FlowControlFlags != Distorm.FlowControl.RET &&
                currentPage.InstructionsDecomposed[i].FlowControlFlags != Distorm.FlowControl.UNC_BRANCH)
            {
                // Add the non-control-modifying instructions.
                block.InstructionsDecomposed.Add(currentPage.InstructionsDecomposed[i]);
                block.InstructionsDisassembled.Add(currentPage.InstructionsDisassembled[i++]);
                if (i >= currentPage.InstructionsDecomposed.Length)
                {
                    Console.WriteLine("Need to use scanner to identify and decompose regions, rather than single pages.");
                    return new BasicBlock();
                }
            }

            // Add the control-modifying instruction.
            block.InstructionsDecomposed.Add(currentPage.InstructionsDecomposed[i]);
            block.InstructionsDisassembled.Add(currentPage.InstructionsDisassembled[i]);

            // End parsing if this is the end of the block.
            if (currentPage.InstructionsDecomposed[i].FlowControlFlags == Distorm.FlowControl.RET)
            {
                return block;
            }

            // Parse the branch block.
            BasicBlock branchBlock;
            if (maxDepth == -1 || currentDepth <= maxDepth)
            {
                // This handles CND_BRANCH, UNC_BRANCH, and CALL (only CALLs with PC operand types).
                if (currentPage.InstructionsDecomposed[i].FlowControlFlags == Distorm.FlowControl.CALL &&
                    currentPage.InstructionsDecomposed[i].ops[0].type == Distorm.OperandType.PC)
                {
                    branchBlock = blocks.FirstOrDefault(
                        x => x.InstructionsDecomposed.Count > 0 &&
                             x.InstructionsDecomposed[0].addr == currentPage.InstructionsDecomposed[i].BranchTarget);
                    if (branchBlock == null)
                    {
                        // Do not expand calls. Add a stub block, instead.
                        branchBlock = new BasicBlock();
                        Distorm.DInst inst = new Distorm.DInst();
                        inst.addr = currentPage.InstructionsDecomposed[i].BranchTarget;
                        branchBlock.InstructionsDecomposed.Add(inst);
                        branchBlock.InstructionsDisassembled.Add("...");
                        blocks.Add(branchBlock);
                    }
                }
                else
                {
                    branchBlock = GenerateBlock(
                        p, new IntPtr((long)currentPage.InstructionsDecomposed[i].BranchTarget), ref blocks, ref pages, maxDepth, currentDepth + 1);
                }

                block.Next.Add(branchBlock);
                branchBlock.Previous.Add(block);
            }

            if (currentPage.InstructionsDecomposed[i].FlowControlFlags == Distorm.FlowControl.CND_BRANCH ||
                currentPage.InstructionsDecomposed[i].FlowControlFlags == Distorm.FlowControl.CALL)
            {
                BasicBlock nextBlock = GenerateBlock(
                    p, new IntPtr((long)currentPage.InstructionsDecomposed[++i].addr), ref blocks, ref pages, maxDepth, currentDepth);
                block.Next.Add(nextBlock);
                nextBlock.Previous.Add(block);
            }

            // Return results.
            return block;
        }

        /// <summary>
        /// Generates a graphviz graph from the supplied basic block set.
        /// </summary>
        /// <param name="blocks">The set of basic blocks that will be converted to a graph.</param>
        /// <returns>Returns a string that holds the contents of a valid graphviz file.</returns>
        public static string GenerateGraph(HashSet<BasicBlock> blocks)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("digraph g{graph[rankdir=TB];node[fontname=\"Courier New\" fontsize=10];");
            foreach (BasicBlock block in blocks)
            {
                sb.Append(block.ToGraphVizEntry());
            }

            sb.Append("}");

            return sb.ToString();
        }

        /// <summary>
        /// Converts this basic block into a node within a graphviz file.
        /// </summary>
        /// <returns>Returns a string representation of this basic block as a graphviz node.</returns>
        public string ToGraphVizEntry()
        {
            StringBuilder sb = new StringBuilder();

            // Create node.
            sb.Append("node" + this.ID + "[label=<");
            sb.Append("<table border=\"0\" cellborder=\"0\" cellspacing=\"0\" cellpadding=\"0\"><tr><td>");
            if (this.InstructionsDecomposed.Count > 0)
            {
                sb.Append("0x" + this.InstructionsDecomposed[0].addr.ToString("x").PadLeft(8, '0') + ":");
                sb.Append("<br align=\"left\" />");
            }

            foreach (string inst in this.InstructionsDisassembled)
            {
                sb.Append(inst);
                sb.Append("<br align=\"left\" />");
            }

            sb.Append("</td></tr></table>>shape=\"box\"];");

            // Create edges.
            foreach (BasicBlock b in this.Next)
            {
                sb.Append("node" + this.ID + "->node" + b.ID + ";");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Searches for basic blocks that contain instructions that occur in other blocks. If any blocks contain
        /// duplicate portions of other blocks, the duplicate portions are removed. All previous and next connections
        /// are adjusted accordingly.
        /// </summary>
        /// <param name="blocks">The set of basic blocks that may contain duplicates.</param>
        private static void RemoveDuplicateInstructions(ref HashSet<BasicBlock> blocks)
        {
            IOrderedEnumerable<BasicBlock> orderedBlocks = blocks.OrderBy(x => x.ID);
            foreach (BasicBlock blockA in orderedBlocks)
            {
                // Only look at blocks where there are more than 1 instruction.
                if (blockA.InstructionsDecomposed.Count > 1)
                {
                    // Look at all instructions after the first instruction.
                    foreach (Distorm.DInst inst in blockA.InstructionsDecomposed.Skip(1))
                    {
                        // Look at the first instruction of all other blocks.
                        IEnumerable<BasicBlock> otherBlocks = orderedBlocks.Where(x => x.ID != blockA.ID);
                        BasicBlock blockFound =
                            otherBlocks.FirstOrDefault(x => x.InstructionsDecomposed[0].addr == inst.addr);
                        if (blockFound != null)
                        {
                            // Delete the current instruction and all after, within the current block.
                            blockA.InstructionsDecomposed.RemoveAll(
                                x => x.addr >= blockFound.InstructionsDecomposed[0].addr);
                            blockA.InstructionsDisassembled =
                                blockA.InstructionsDisassembled.Take(blockA.InstructionsDecomposed.Count).ToList();

                            // Set the fall-through block to the block that was found.
                            blockA.Next.Clear();
                            blockA.Next.Add(blockFound);
                            break;
                        }
                    }
                }
            }
        }

        #endregion
    }
}
