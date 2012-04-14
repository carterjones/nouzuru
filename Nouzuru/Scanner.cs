namespace Nouzuru
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Text;
    using Logger;

    /// <summary>
    /// A memory scanner that can be used to search a process for data.
    /// </summary>
    public class Scanner : PInteractor, INotifyPropertyChanged
    {
        #region Fields

        /// <summary>
        /// If true, no scan has occured yet or no scan has been run since the last reset.
        /// </summary>
        private bool isFirstScan = true;

        /// <summary>
        /// Inticates (in percent) how complete the current scan is.
        /// </summary>
        private int progress;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the Scanner class.
        /// </summary>
        public Scanner()
        {
            SysInteractor.Init();
            this.Regions = new List<Region>();
            this.Matches = new List<IntPtr>();
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the list of pointers to the addresses that matched the search criteria from the most recent scan.
        /// </summary>
        public List<IntPtr> Matches { get; private set; }

        /// <summary>
        /// Gets the number of matches from the most recent scan.
        /// </summary>
        public int NumMatches
        {
            get { return this.Matches.Count; }
        }

        /// <summary>
        /// Gets a value (in percent) how complete the current scan is.
        /// </summary>
        public int Progress
        {
            get
            {
                return this.progress;
            }

            private set
            {
                this.progress = value;
                if (this.PropertyChanged != null)
                {
                    this.OnPropertyChanged("Progress");
                }
            }
        }

        /// <summary>
        /// Gets the list of regions that is scanned.
        /// </summary>
        public List<Region> Regions { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Searches through the target process's memory and identifies regions that are  of interest, when scanning.
        /// Creates a Region and saves it to the list of Regions for future scans.
        /// </summary>
        /// <returns>Returns true on successfully identifying regions in the target's memory.</returns>
        public bool IdentifyRegions()
        {
            return this.IdentifyRegions(0, (ulong)SysInteractor.MaxAddress);
        }

        /// <summary>
        /// Searches through the target process's memory and identifies regions that are  of interest, when scanning.
        /// Creates a Region and saves it to the list of Regions for future scans.
        /// </summary>
        /// <param name="minAddress">The minimum address that should be used, during this search.</param>
        /// <param name="maxAddress">The maximum address that should be used, during this search.</param>
        /// <returns>Returns true on successfully identifying regions in the target's memory.</returns>
        public bool IdentifyRegions(ulong minAddress = 0, ulong maxAddress = 0)
        {
            if (!this.IsOpen)
            {
                this.Status.Log(
                    "Unable to identify regions, because the target process has not been opened.",
                    Logger.Level.HIGH);
                return false;
            }

            if (maxAddress == 0)
            {
                maxAddress = (ulong)SysInteractor.MaxAddress;
            }

            WinApi.MEMORY_BASIC_INFORMATION mbi = new WinApi.MEMORY_BASIC_INFORMATION();
            ulong address = minAddress;
            bool result = true;
            this.Regions.Clear();

            while (address < maxAddress)
            {
                result = WinApi.VirtualQueryEx(Proc.Handle, (IntPtr)address, out mbi, (uint)Marshal.SizeOf(mbi));
                if (result && IsReadable(mbi))
                {
                    Region r = new Region((IntPtr)address, (uint)mbi.RegionSize, mbi.Protect, mbi.Type);
                    this.Regions.Add(r);
                }

                address += (ulong)mbi.RegionSize;
            }

            return this.Regions.Count > 0;
        }

        /// <summary>
        /// Marks all addresses in all regions of interest to the "unscanned" state.
        /// </summary>
        public void ResetResults()
        {
            this.isFirstScan = true;
            this.Progress = 0;
            for (int i = 0; i < this.Regions.Count; ++i)
            {
                this.Regions[i].ResetMatches();
            }
        }

        /// <summary>
        /// Reads the target process's memory, to update the cache, and then searches the cache for the specified
        /// value.
        /// </summary>
        /// <param name="value">The value of interest in this search.</param>
        /// <returns>Returns true if at least one match is found.</returns>
        public bool SearchLive(byte[] value)
        {
            bool result = true;
            result &= this.UpdateCache();
            result &= this.SearchCache(value);
            return result;
        }

        /// <summary>
        /// Reads the target process's memory, to update the cache, and then searches the cache for the specified
        /// string.
        /// </summary>
        /// <param name="value">The string of interest in this search.</param>
        /// <param name="e">The type of encoding that the string of interest uses.</param>
        /// <returns>Returns true if at least one match is found.</returns>
        public bool SearchLive(string value, Encoding e)
        {
            return this.SearchLive(e.GetBytes(value));
        }

        /// <summary>
        /// Searches the cache of Regions for the specified value.
        /// </summary>
        /// <param name="value">The value of interest in this search.</param>
        /// <returns>Returns true if at least one match is found.</returns>
        public bool SearchCache(byte[] value)
        {
            bool overallResult = false;
            bool singleResult = false;
            this.Matches.Clear();
            for (int i = 0; i < this.Regions.Count; ++i)
            {
                if (this.isFirstScan || this.Regions[i].MatchHasBeenFound)
                {
                    singleResult = this.Regions[i].CalcMatches(value);
                    if (singleResult)
                    {
                        this.Matches.AddRange(this.Regions[i].Matches);
                    }

                    overallResult |= singleResult;
                }

                this.Progress = (int)((i / (float)this.Regions.Count) * 100);
            }

            this.isFirstScan = false;
            this.Progress = 100;

            return overallResult;
        }

        /// <summary>
        /// Reads the regions of interest into the list of Regions, for future scanning.
        /// </summary>
        /// <returns>Returns true on successful update.</returns>
        public bool UpdateCache()
        {
            if (!this.IsOpen)
            {
                this.Status.Log(
                    "Unable to read memory, because the target process has not been opened.",
                    Logger.Level.HIGH);
                return false;
            }

            bool result = true;
            for (int i = 0; i < this.Regions.Count; ++i)
            {
                byte[] data = new byte[this.Regions[i].Size];
                result &= Read(this.Regions[i].BaseAddress.AsIntPtr, data);
                this.Regions[i].BaseAddress.CurValue = data;
            }

            return result;
        }

        /// <summary>
        /// Invoked when a property, which is tracked by another object that receives updates, is updated.
        /// </summary>
        /// <param name="name">The name of the property that is updated.</param>
        protected void OnPropertyChanged(string name)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        #endregion
    }
}
