namespace Nouzuru
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents an in-memory region.
    /// </summary>
    public class Region
    {
        #region Fields

        /// <summary>
        /// An array of boolean values representing each address in this region. If an address at index (i) has been
        /// matched during a search, then match[i] will be set to true. Conversely, if an address at index (i) has
        /// been found to not meet the search criteria, then match[i] will be set to false.
        /// </summary>
        private bool[] matches;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the Region class.
        /// </summary>
        /// <param name="baseAddress">The base address of the region.</param>
        /// <param name="size">The size of the region.</param>
        /// <param name="protection">The protection settings on this region.</param>
        /// <param name="type">The type of the memory region.</param>
        public Region(IntPtr baseAddress, uint size, WinApi.MemoryProtect protection, WinApi.MemoryType type)
        {
            this.Size = size;
            this.BaseAddress = new Address(baseAddress, this.Size);
            this.matches = new bool[this.Size];
            this.Protect = protection;
            this.Type = type;

            // might want to import and use memset, if this for loop is really slow
            for (uint i = 0; i < this.Size; ++i)
            {
                this.matches[i] = true;
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the base address of this memory region.
        /// </summary>
        public Address BaseAddress { get; private set; }

        /// <summary>
        /// Gets a list of void pointers that point to addresses that matched the last scan criteria.
        /// </summary>
        /// <remarks>This value is re-calculated on every call, so cache its results for efficiency purposes.</remarks>
        public List<IntPtr> Matches
        {
            get
            {
                List<IntPtr> matches = new List<IntPtr>();
                for (uint i = 0; i < this.Size; ++i)
                {
                    if (this.matches[i] == true)
                    {
                        matches.Add((IntPtr)((int)this.BaseAddress.AsIntPtr + i));
                    }
                }

                return matches;
            }
        }

        /// <summary>
        /// Gets a value indicating whether a match has been found in a prior search.
        /// </summary>
        public bool MatchHasBeenFound { get; private set; }

        /// <summary>
        /// Gets the number of matches from the last scan.
        /// </summary>
        /// <remarks>This value is re-calculated on every call, so cache its results for efficiency purposes.</remarks>
        public uint NumMatches
        {
            get
            {
                uint numMatches = 0;
                for (uint i = 0; i < this.Size; ++i)
                {
                    if (this.matches[i] == true)
                    {
                        ++numMatches;
                    }
                }

                return numMatches;
            }
        }

        /// <summary>
        /// Gets the protection settings of this memory region.
        /// </summary>
        public WinApi.MemoryProtect Protect { get; private set; }

        /// <summary>
        /// Gets the size of this memory region.
        /// </summary>
        public uint Size { get; private set; }

        /// <summary>
        /// Gets the type of memory region.
        /// </summary>
        public WinApi.MemoryType Type { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Finds all locations in BaseAddress that match the specified value.
        /// </summary>
        /// <remarks>
        /// Uses algorithm similar to memcmp function, adapted from Microsoft's site:
        /// http://research.microsoft.com/en-us/um/redmond/projects/invisible/src/crt/memcmp.c.htm
        /// </remarks>
        /// <param name="value">The value that is the target of the search.</param>
        /// <returns>Returns true if a match is found.</returns>
        public bool CalcMatches(byte[] value)
        {
            uint lastIndex = this.Size - (uint)value.Length;

            this.MatchHasBeenFound = false;
            int count = value.Length;
            int v = 0, j = 0;

            // Set the tail end of the results to false.
            for (uint i = lastIndex; i < this.Size; ++i)
            {
                this.matches[i] = false;
            }

            // Calculate matches.
            for (uint i = 0; i < lastIndex; ++i)
            {
                if (this.matches[i])
                {
                    v = 0;
                    j = 0;
                    count = value.Length;
                    while (count-- > 0 && v == 0)
                    {
                        v = value[j] - this.BaseAddress.CurValue[i + j++];
                    }

                    // Value matches the scanned data, if v == 0.
                    this.matches[i] = v == 0;
                    this.MatchHasBeenFound |= v == 0;
                }
            }

            return this.MatchHasBeenFound;
        }

        /// <summary>
        /// Marks all locations in matches as unscanned.
        /// </summary>
        public void ResetMatches()
        {
            for (uint i = 0; i < this.Size; ++i)
            {
                this.matches[i] = true;
            }
        }

        #endregion
    }
}
