namespace Nouzuru
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using Bunseki;

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

        private static Disassembler d = new Disassembler();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the BasicBlock class.
        /// </summary>
        public BasicBlock()
        {
            this.Instructions = new List<Instruction>();
            this.Previous = new List<BasicBlock>();
            this.Next = new List<BasicBlock>();
            this.ID = numBlocksMade++;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the list of instructions in this basic block.
        /// </summary>
        public List<Instruction> Instructions { get; set; }

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
                if (b.Instructions[0].Address == baseAddress)
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

                currentPage.Instructions =
                    BasicBlock.d.DisassembleInstructions(currentPage.Data, currentPage.Address).ToList();
                if (currentPage.Instructions.Count == 0)
                {
                    return new BasicBlock();
                }

                pages.Add(currentPage);
            }

            int i = 0;
            for (; i < currentPage.Instructions.Count; ++i)
            {
                if (currentPage.Instructions[i].Address == baseAddress)
                {
                    // Break once the instruction has been found.
                    break;
                }
                else if ((ulong)currentPage.Instructions[i].Address > (ulong)baseAddress)
                {
                    Console.WriteLine("Disassembly failed for address: 0x" + currentPage.Instructions[i].Address.ToString("x").PadLeft(8, '0'));
                    return new BasicBlock();
                }
            }

            if (i >= currentPage.Instructions.Count)
            {
                Console.WriteLine("Need to use scanner to identify and decompose regions, rather than single pages.");
                return new BasicBlock();
            }

            // Verify that no control flow instructions are added in the middle of a basic block.
            while (
                // TODO: verify this still works after adjusting the call verification logic.
                currentPage.Instructions[i].FlowType != Instruction.ControlFlow.Call &&
                currentPage.Instructions[i].FlowType != Instruction.ControlFlow.ConditionalBranch &&
                currentPage.Instructions[i].FlowType != Instruction.ControlFlow.Return &&
                currentPage.Instructions[i].FlowType != Instruction.ControlFlow.UnconditionalBranch)
            {
                // Add the non-control-modifying instructions.
                block.Instructions.Add(currentPage.Instructions[i++]);
                if (i >= currentPage.Instructions.Count)
                {
                    Console.WriteLine("Need to use scanner to identify and decompose regions, rather than single pages.");
                    return new BasicBlock();
                }
            }

            // Add the control-modifying instruction.
            block.Instructions.Add(currentPage.Instructions[i]);

            // End parsing if this is the end of the block.
            if (currentPage.Instructions[i].FlowType == Instruction.ControlFlow.Return)
            {
                return block;
            }

            // Parse the branch block.
            BasicBlock branchBlock;
            if (maxDepth == -1 || currentDepth <= maxDepth)
            {
                // TODO: verify this still works after adjusting the call verification logic.
                // This handles CND_BRANCH, UNC_BRANCH, and CALL.
                if (currentPage.Instructions[i].FlowType == Instruction.ControlFlow.Call)
                {
                    branchBlock = blocks.FirstOrDefault(
                        x => x.Instructions.Count > 0 &&
                             x.Instructions[0].Address == currentPage.Instructions[i].BranchTarget);
                    if (branchBlock == null)
                    {
                        // Do not expand calls. Add a stub block, instead.
                        branchBlock = new BasicBlock();

                        // TODO: verify that creating an invalid instruction works.
                        branchBlock.Instructions.Add(
                            Instruction.CreateInvalidInstruction(currentPage.Instructions[i].BranchTarget));
                        blocks.Add(branchBlock);
                    }
                }
                else
                {
                    branchBlock = GenerateBlock(
                        p, new IntPtr((long)currentPage.Instructions[i].BranchTarget), ref blocks, ref pages, maxDepth, currentDepth + 1);
                }

                block.Next.Add(branchBlock);
                branchBlock.Previous.Add(block);
            }

            if (currentPage.Instructions[i].FlowType == Instruction.ControlFlow.ConditionalBranch ||
                currentPage.Instructions[i].FlowType == Instruction.ControlFlow.Call)
            {
                BasicBlock nextBlock = GenerateBlock(
                    p, new IntPtr((long)currentPage.Instructions[++i].Address), ref blocks, ref pages, maxDepth, currentDepth);
                block.Next.Add(nextBlock);
                nextBlock.Previous.Add(block);
            }

            // Return results.
            return block;
        }

        /// <summary>
        /// Combines multiple sets of blocks into a single set of blocks. Removes any duplicate blocks and adjusts
        /// next and previous connections accordingly.
        /// </summary>
        /// <param name="blockSets">The collection of basic block sets.</param>
        /// <returns>Returns the unified set of blocks.</returns>
        public static HashSet<BasicBlock> MergeBlockSets(params HashSet<BasicBlock>[] blockSets)
        {
            HashSet<BasicBlock> blocksCombo = new HashSet<BasicBlock>();
            foreach (HashSet<BasicBlock> blockSet in blockSets)
            {
                blocksCombo.UnionWith(blockSet);
            }

            HashSet<BasicBlock> blocksForRemoval = new HashSet<BasicBlock>();
            foreach (BasicBlock block in blocksCombo)
            {
                IEnumerable<BasicBlock> connections =
                    blocksCombo.Where(x => x.Instructions[0].Address == block.Instructions[0].Address);
                int numConnections = connections.Count();
                if (numConnections > 1)
                {
                    // TODO: fix this check:
                    IEnumerable<BasicBlock> expanded =
                        connections.Where(x => !x.Instructions[0].Equals("..."));
                    int numExpanded = expanded.Count();
                    BasicBlock chosenBlock;
                    if (numExpanded == 0)
                    {
                        // Both are stubs, so just pick the first one.
                        chosenBlock = connections.ElementAt(0);
                    }
                    else
                    {
                        // Assume there are no discrepencies and choose the first element.
                        chosenBlock = expanded.ElementAt(0);
                    }

                    // Adjust the blocks adjacent to the non-chosen blocks to now use the chosen block.
                    IEnumerable<BasicBlock> nonChosenBlocks = connections.Where(x => x.ID != chosenBlock.ID);
                    int numNonChosenBlocks = nonChosenBlocks.Count();
                    foreach (BasicBlock nonChosenBlock in nonChosenBlocks)
                    {
                        // Re-direct the previous blocks to the new block.
                        foreach (BasicBlock prev in nonChosenBlock.Previous)
                        {
                            prev.Next.Remove(nonChosenBlock);

                            // Add the link to the new block, if it has not been added in a prior iteration.
                            if (!prev.Next.Exists(x => x.ID == chosenBlock.ID))
                            {
                                prev.Next.Add(chosenBlock);
                            }
                        }

                        // Mark the block to be removed.
                        blocksForRemoval.Add(nonChosenBlock);
                    }
                }
            }

            foreach (BasicBlock block in blocksForRemoval)
            {
                blocksCombo.Remove(block);
            }

            return blocksCombo;
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
            if (this.Instructions.Count > 0)
            {
                sb.Append("0x" + this.Instructions[0].Address.ToString("x").PadLeft(8, '0') + ":");
                sb.Append("<br align=\"left\" />");
            }

            foreach (Instruction inst in this.Instructions)
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
                if (blockA.Instructions.Count > 1)
                {
                    // Look at all instructions after the first instruction.
                    foreach (Instruction inst in blockA.Instructions.Skip(1))
                    {
                        // Look at the first instruction of all other blocks.
                        IEnumerable<BasicBlock> otherBlocks = orderedBlocks.Where(x => x.ID != blockA.ID);
                        BasicBlock blockFound =
                            otherBlocks.FirstOrDefault(x => x.Instructions[0].Address == inst.Address);
                        if (blockFound != null)
                        {
                            // Delete the current instruction and all after, within the current block.
                            blockA.Instructions.RemoveAll(
                                x => (ulong)x.Address >= (ulong)blockFound.Instructions[0].Address);
                            blockA.Instructions = blockA.Instructions.Take(blockA.Instructions.Count).ToList();

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
