namespace Nouzuru
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Provides access to system-wide settings, useful for memory manipulations.
    /// </summary>
    public static class SysInteractor
    {
        #region Properties

        /// <summary>
        /// Gets the maximum readable address of a process.
        /// </summary>
        public static IntPtr MaxAddress { get; private set; }

        /// <summary>
        /// Gets the page size of a memory page on this system.
        /// </summary>
        public static long PageSize { get; private set; }

        /// <summary>
        /// Gets the number of processors on this system.
        /// </summary>
        public static long NumProcs { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this system is running in 64-bit mode.
        /// </summary>
        public static bool Is64Bit { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the SysInteractor Init() function has been called.
        /// </summary>
        public static bool IsInitialized { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Populates all fields in this class. Must be called before any PInteractor is initialized or any class
        /// derived of PInteractor is initialized.
        /// </summary>
        public static void Init()
        {
            WinApi.SYSTEM_INFO si = new WinApi.SYSTEM_INFO();
            bool isThisWow64;
            WinApi.IsWow64Process(Process.GetCurrentProcess().Handle, out isThisWow64);
            if (isThisWow64)
            {
                WinApi.GetSystemInfo(out si);
            }
            else
            {
                WinApi.GetNativeSystemInfo(out si);
            }

            SysInteractor.MaxAddress = si.maximumApplicationAddress;
            SysInteractor.NumProcs = si.numberOfProcessors;
            SysInteractor.PageSize = si.pageSize;
            SysInteractor.Is64Bit = si.processorArchitecture == 6 || si.processorArchitecture == 9;

            SysInteractor.IsInitialized = true;
        }

        #endregion
    }
}
