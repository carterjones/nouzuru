namespace Nouzuru
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using Bunseki;
    using Logger;

    /// <summary>
    /// The foundational class that is used for interacting with processes.
    /// </summary>
    public class PInteractor
    {
        #region Fields

        /// <summary>
        /// A disassembler that can be used to disassemble code read from the target process.
        /// </summary>
        protected internal Disassembler d = new Disassembler();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the PInteractor class.
        /// </summary>
        public PInteractor()
        {
            this.Status = new Logger(Logger.Type.CONSOLE | Logger.Type.FILE, Logger.Level.NONE, "nouzuru.log");
            this.d.Engine = Disassembler.InternalDisassembler.BeaEngine;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the process identifier (PID) of the target process.
        /// </summary>
        public int PID
        {
            get { return this.Proc.Id; }
        }

        /// <summary>
        /// Gets tname of the target process.
        /// </summary>
        public string ProcessName
        {
            get { return this.Proc.ProcessName; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the target process is 64-bit or not.
        /// </summary>
        public bool Is64Bit { get; protected set; }

        /// <summary>
        /// Gets a value indicating whether a target process has been opened.
        /// </summary>
        public bool IsOpen
        {
            get
            {
                return
                    (this.ProcHandle != IntPtr.Zero) &&
                    (this.Proc != null) &&
                    (this.Proc.Id != 0) &&
                    !this.Proc.HasExitedSafe() &&
                    (this.EntryPointAddress != IntPtr.Zero);
            }
        }

        /// <summary>
        /// Gets the thread ID of the current process.
        /// </summary>
        /// <remarks>TODO: Verify this is accurate.</remarks>
        public int ThreadID
        {
            get { return this.Proc.Threads[0].Id; }
        }

        /// <summary>
        /// Gets the base address of the target process.
        /// </summary>
        public IntPtr BaseAddress
        {
            get
            {
                if (this.Proc != null)
                {
                    try
                    {
                        return this.Proc.MainModule.BaseAddress;
                    }
                    catch (Win32Exception)
                    {
                        return IntPtr.Zero;
                    }
                    catch (NullReferenceException)
                    {
                        return IntPtr.Zero;
                    }
                }
                else
                {
                    return IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// Gets the entry point address of the target process.
        /// </summary>
        public IntPtr EntryPointAddress
        {
            get
            {
                if (this.Proc != null)
                {
                    try
                    {
                        return this.Proc.MainModule.EntryPointAddress;
                    }
                    catch (Win32Exception)
                    {
                        return IntPtr.Zero;
                    }
                }
                else
                {
                    return IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// Gets or sets a .NET Process instance that points to the target process.
        /// </summary>
        public Process Proc { get; protected set; }

        /// <summary>
        /// Gets or sets a handle with special rights to the target process. This should be used when more rights are
        /// needed to a process than the rights returned by a C# Process object via the Process.Handle IntPtr.
        /// </summary>
        /// <remarks>
        /// This is different from the IntPtr that is returned by Proc.Handle, which is automatically created by the
        /// C# Process library. These two handles have completetly separate purposes.
        /// </remarks>
        protected IntPtr ProcHandle { get; set; }

        /// <summary>
        /// Gets or sets a logger used to report status updates, errors, etc.
        /// </summary>
        protected Logger Status { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Closes the handle to the process, if it is open.
        /// </summary>
        public void Close()
        {
            if (this.ProcHandle != null && !this.ProcHandle.Equals(IntPtr.Zero))
            {
                WinApi.CloseHandle(this.ProcHandle);
                this.ProcHandle = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Determines the module, if one exists, that contains the supplied address within its address range.
        /// </summary>
        /// <param name="address">The address that occurs within the module's address range.</param>
        /// <returns>Returns the module if it exists. If no such module exists, null is returned.</returns>
        public ProcessModule DetermineModuleFromAddress(IntPtr address)
        {
            if (!this.IsOpen)
            {
                this.Status.Log(
                    "Unable to get module by using its address, because the target process has not been opened.",
                    Logger.Level.HIGH);
                return null;
            }

            ProcessModuleCollection loadedModules = this.GetLoadedModules();
            if (loadedModules == null || loadedModules.Count == 0)
            {
                this.Status.Log(
                    "Unable to get module by using its address, because module list is empty.",
                    Logger.Level.HIGH);
                return null;
            }

            // Create a list of modules, for sorting purposes.
            List<ProcessModule> modules = new List<ProcessModule>();
            foreach (ProcessModule module in loadedModules)
            {
                modules.Add(module);
            }

            // Sort the modules by address.
            modules.Sort(CompareModules);

            // Iterate until the provided address is greater than the current module's address.
            foreach (ProcessModule module in modules)
            {
#if _WIN64
                if (module.BaseAddress.ToInt64() > address.ToInt64())
#else
                if (module.BaseAddress.ToInt32() > address.ToInt32())
#endif
                {
                    return module;
                }
            }

            // No module was found.
            this.Status.Log(
                "Unable to determine module address, because no module at " + this.IntPtrToFormattedAddress(address) +
                "could be found.",
                Logger.Level.HIGH);
            return null;
        }

        /// <summary>
        /// Disassembles the provided address range.
        /// </summary>
        /// <param name="from">The base address of the target range for disassembly.</param>
        /// <param name="rangeSize">The size of the range of memory that will be disassembled.</param>
        /// <returns>Returns the disassembly as a List of instructions.</returns>
        public List<Instruction> DisassembleAddressRange(IntPtr from, long rangeSize)
        {
            if (!this.IsOpen)
            {
                this.Status.Log(
                    "Unable to decompose the instructions at " + this.IntPtrToFormattedAddress(from) +
                    ", because the target process has not been opened.",
                    Logger.Level.HIGH);
                return new List<Instruction>();
            }

            byte[] data = new byte[rangeSize];
            if (!this.Read(from, data))
            {
                this.Status.Log(
                    "Unable to decompose the instructions at " + this.IntPtrToFormattedAddress(from) +
                    ", because the address supplied (" + this.IntPtrToFormattedAddress(from) + ") could not be read.",
                    Logger.Level.HIGH);
                return new List<Instruction>();
            }

            return this.d.DisassembleInstructions(data, from).ToList();
        }

        /// <summary>
        /// Disassemble the instruction at the specified address.
        /// </summary>
        /// <param name="address">The address of the instruction.</param>
        /// <returns>Returns the disassembled instruction. On failure, returns an invalid instruction.</returns>
        public Instruction DisassembleInstruction(IntPtr address)
        {
            List<Instruction> insts = this.DisassembleAddressRange(address, 15);
            if (insts.Count > 0)
            {
                return insts[0];
            }
            else
            {
                return Instruction.CreateInvalidInstruction();
            }
        }

        /// <summary>
        /// Gets the name of the file referenced by the supplied hModule.
        /// </summary>
        /// <param name="moduleHandle">The hModule.</param>
        /// <returns>Returns the full path to the file.</returns>
        public string GetFileNameFromHModule(IntPtr moduleHandle)
        {
            StringBuilder filename = new StringBuilder(255);
            WinApi.GetModuleFileNameEx(this.ProcHandle, moduleHandle, filename, filename.Capacity);
            return filename.ToString();
        }

        /// <summary>
        /// Iterates through all modules in the target process and searches them for the provided function name.
        /// </summary>
        /// <param name="funcName">The name of the function that is being looked up.</param>
        /// <returns>Returns a vector of matching addresses.</returns>
        public List<IntPtr> GetFunctionAddresses(string funcName)
        {
            if (!this.IsOpen)
            {
                this.Status.Log(
                    "Unable to get function addresses, because the target process has not been opened.",
                    Logger.Level.HIGH);
                return new List<IntPtr>();
            }

            List<IntPtr> addresses = new List<IntPtr>();
            ProcessModuleCollection modules = this.GetLoadedModules();
            if (modules == null || modules.Count == 0)
            {
                this.Status.Log(
                    "Unable to get function addresses, because the loaded module list is empty.",
                    Logger.Level.HIGH);
                return addresses;
            }

            // Iterate through each module and see if it has the function.
            foreach (ProcessModule module in modules)
            {
                IntPtr funcAddress = WinApi.GetProcAddress(module.BaseAddress, funcName);

                // If the function exists in this module, add it to the address list.
                if (funcAddress != null)
                {
                    addresses.Add(funcAddress);
                }
            }

            return addresses;
        }

        /// <summary>
        /// Reads the first 14 bytes at the given address and returns the length of instruction located at the 0
        /// offset. (Intel instructions are never larger than 14 bytes)
        /// </summary>
        /// <param name="address">The address that holds the instruction of interest.</param>
        /// <returns>Returns the size of the instruction on success and -1 on failure.</returns>
        public int GetInstructionSize(IntPtr address)
        {
            if (!this.IsOpen)
            {
                this.Status.Log(
                    "Unable to get the instruction size of the instruction at " +
                    this.IntPtrToFormattedAddress(address) + ", because the target process has not been opened.",
                    Logger.Level.HIGH);
                return -1;
            }

            byte[] data = new byte[14];
            if (!this.Read(address, data))
            {
                this.Status.Log(
                    "Unable to get the instruction size of the instruction at " +
                    this.IntPtrToFormattedAddress(address) + ", because there was an error reading from the address.",
                    Logger.Level.HIGH);
                return -1;
            }

            List<Instruction> insts = this.d.DisassembleInstructions(data, address).ToList();
            if (insts.Count < 1)
            {
                this.Status.Log(
                    "Unable to get the instruction size of the instruction at " +
                    this.IntPtrToFormattedAddress(address) + ". There was an error during decomposition.",
                    Logger.Level.HIGH);
                return -1;
            }
            else
            {
                return (int)insts[0].NumBytes;
            }
        }

        /// <summary>
        /// Gets the modules currently loaded in this process.
        /// </summary>
        /// <returns>Returns the modules currently loaded in this process.</returns>
        public ProcessModuleCollection GetLoadedModules()
        {
            // TODO: Create workaround for 64->32 bit procs by using EnumProcessModulesEx.
            if (!this.IsOpen)
            {
                this.Status.Log(
                    "Unable to get loaded modules, because the target process has not been opened.",
                    Logger.Level.HIGH);
                return default(ProcessModuleCollection);
            }

            try
            {
                return this.Proc.Modules;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                this.Status.Log(
                    "Unable to get loaded modules, because an exception occured: " + Marshal.GetLastWin32Error() + ".",
                    Logger.Level.HIGH);
                return new ProcessModuleCollection(null);
            }
        }

        /// <summary>
        /// Gets a process module, based on the process module's name.
        /// </summary>
        /// <param name="name">The name of the module of interest.</param>
        /// <returns>
        /// Returns the process module, based on the module's name. If no such module exists, null is returned.
        /// </returns>
        public ProcessModule GetModuleByName(string name)
        {
            if (!this.IsOpen)
            {
                this.Status.Log(
                    "Unable to get module by using its name, because the target process has not been opened.",
                    Logger.Level.HIGH);
                return null;
            }

            ProcessModuleCollection loadedModules = this.GetLoadedModules();
            if (loadedModules == null || loadedModules.Count == 0)
            {
                this.Status.Log(
                    "Unable to get module by using its name, because module list is empty.",
                    Logger.Level.HIGH);
                return null;
            }

            foreach (ProcessModule m in loadedModules)
            {
                if (m.ModuleName.Equals(name))
                {
                    return m;
                }
            }

            // No module was found.
            this.Status.Log(
                "Unable to determine module address, because no module named " + name + "could be found.",
                Logger.Level.HIGH);
            return null;
        }

        /// <summary>
        /// Creates a hexidecimally formatted string from the given an address.
        /// 32-bit processes will be 8 characters in length (excluding the prepended 0x) and 64-bit processes will be
        /// 16 characters in length (excluding the prepended 0x).
        /// </summary>
        /// <param name="address">The address to be formatted.</param>
        /// <returns>Returns a string in the format of 0xabcde098.</returns>
        public string IntPtrToFormattedAddress(IntPtr address)
        {
            if (this.Is64Bit)
            {
                return "0x" + address.ToInt64().ToString("X").PadLeft(16, '0');
            }
            else
            {
                return "0x" + address.ToInt32().ToString("X").PadLeft(8, '0');
            }
        }

        /// <summary>
        /// Determines if the page in which address appears is readable.
        /// </summary>
        /// <param name="address">The address to test for readability.</param>
        /// <returns>Returns true if the provided address is readable for the specified number of bytes.</returns>
        public bool IsReadable(IntPtr address)
        {
            if (!this.IsOpen)
            {
                this.Status.Log(
                    "Unable to determine memory range readability, because the target process has not been opened.",
                    Logger.Level.HIGH);
                return false;
            }

            WinApi.MEMORY_BASIC_INFORMATION mbi = new WinApi.MEMORY_BASIC_INFORMATION();
            if (WinApi.VirtualQueryEx(this.ProcHandle, address, out mbi, (uint)Marshal.SizeOf(mbi)))
            {
                this.Status.Log(
                    "Unable to determine memory range readability, because the target memory range could not be " +
                    "analyzed with VirtualQueryEx.",
                    Logger.Level.HIGH);
                return false;
            }

            return this.IsReadable(mbi);
        }

        /// <summary>
        /// Determines if the provided address is readable for the specified number of bytes.
        /// </summary>
        /// <param name="mbi">The memory basic information variable containing data about the target region.</param>
        /// <returns>Returns true if the provided address is readable for the specified number of bytes.</returns>
        public bool IsReadable(WinApi.MEMORY_BASIC_INFORMATION mbi)
        {
            if (!this.IsOpen)
            {
                this.Status.Log(
                    "Unable to determine memory range readability, because the target process has not been opened.",
                    Logger.Level.HIGH);
                return false;
            }

            return
                (mbi.Protect == WinApi.MemoryProtect.PAGE_READWRITE ||
                 mbi.Protect == WinApi.MemoryProtect.PAGE_EXECUTE_READWRITE ||
                 mbi.Protect == WinApi.MemoryProtect.PAGE_WRITECOPY ||
                 mbi.Protect == WinApi.MemoryProtect.PAGE_READONLY ||
                 mbi.Protect == WinApi.MemoryProtect.PAGE_EXECUTE_READ) &&
                (mbi.Type != WinApi.MemoryType.MEM_MAPPED);
        }

        /// <summary>
        /// Opens the process by using the provided C# Process instance.
        /// </summary>
        /// <param name="process">The process to be opened.</param>
        /// <returns>Returs true on success.</returns>
        public bool Open(Process process)
        {
            return this.Open((uint)process.Id);
        }

        /// <summary>
        /// Searches the list of current processes for a process that matches the supplied process name. Stores
        /// information about the target process.
        /// </summary>
        /// <param name="procName">The name of the target process.</param>
        /// <returns>Returns true on success.</returns>
        public bool Open(string procName)
        {
            Process[] procs = Process.GetProcessesByName(procName);
            if (procs.Length > 0)
            {
                return this.Open((uint)procs[0].Id);
            }

            this.Status.Log(
                "Unable to open the target process, because no process with the name '" + procName +
                "' could be found.",
                Logger.Level.HIGH);
            return false;
        }

        /// <summary>
        /// Opens the process by id. Stores information about the target process.
        /// </summary>
        /// <param name="pid">The process identifier of the target process.</param>
        /// <returns>Returns true on success.</returns>
        public bool Open(uint pid)
        {
            if (!SysInteractor.IsInitialized)
            {
                SysInteractor.Init();
            }

            WinApi.ProcessRights flags =
                WinApi.ProcessRights.QUERY_INFORMATION |
                WinApi.ProcessRights.VM_READ |
                WinApi.ProcessRights.VM_WRITE |
                WinApi.ProcessRights.VM_OPERATION;
            this.ProcHandle = WinApi.OpenProcess(flags, false, pid);
            if (this.ProcHandle != null)
            {
                try
                {
                    this.Proc = Process.GetProcessById((int)pid);
                }
                catch (ArgumentException)
                {
                    WinApi.CloseHandle(this.ProcHandle);
                    return false;
                }

                bool isWow64;
                if (!WinApi.IsWow64Process(this.ProcHandle, out isWow64))
                {
                    this.Status.Log(
                        "Unable to determine bitness of process: " + this.Proc.ProcessName, Logger.Level.HIGH);
                }

                // 64-bit process detection.
                // Note: This does not take into account for PAE. No plans to support PAE currently exist.
                if (isWow64)
                {
                    // For scanning purposes, Wow64 processes will be treated as as 32-bit processes.
                    this.Is64Bit = false;
                    this.d.TargetArchitecture = Disassembler.Architecture.x86_32;
                }
                else
                {
                    // If it is not Wow64, then the process is natively running, so set it according to the OS
                    // architecture.
                    this.Is64Bit = SysInteractor.Is64Bit;
                    this.d.TargetArchitecture =
                        this.Is64Bit ? Disassembler.Architecture.x86_64 : Disassembler.Architecture.x86_32;
                }

                return true;
            }
            else
            {
                this.Status.Log("Unable to open the target process.", Logger.Level.HIGH);
                return false;
            }
        }

        /// <summary>
        /// Reads from the address with enough data to fill up the data parameter.
        /// </summary>
        /// <param name="address">The address to be read.</param>
        /// <param name="data">The destination buffer for the data.</param>
        /// <returns>Returns the result of ReadProcessMemory().</returns>
        public bool Read(IntPtr address, byte[] data)
        {
            if (!this.IsOpen)
            {
                this.Status.Log(
                    "Unable to read from " + this.IntPtrToFormattedAddress(address) +
                    ", because the target process has not been opened.",
                    Logger.Level.HIGH);
                return false;
            }

            uint nbw = 0;
            if (!WinApi.ReadProcessMemory(this.ProcHandle, address, data, (uint)data.Length, out nbw))
            {
                this.Status.Log(
                    "Could not read value at address: " + this.IntPtrToFormattedAddress(address),
                    Logger.Level.HIGH);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Reads from the address with enough data to fill up the data parameter.
        /// </summary>
        /// <param name="address">The address to be read.</param>
        /// <param name="data">The destination buffer for the data.</param>
        /// <returns>Returns the result of ReadProcessMemory().</returns>
        public bool Read(ulong address, byte[] data)
        {
            return this.Read(new IntPtr((long)address), data);
        }

        /// <summary>
        /// Reads from the address for the number of bytes that equals the size of the specified type. This is read
        /// directly into the structure, so no extra copying takes place.
        /// </summary>
        /// <typeparam name="T">The type of object to be populated.</typeparam>
        /// <param name="address">The address containing the data used to populate the object to be returned.</param>
        /// <returns>Returns a structure that has been populated with data from the specified address.</returns>
        public T ReadStructure<T>(IntPtr address)
        {
            T structure = default(T);
            this.ReadStructure<T>(address, ref structure);
            return structure;
        }

        /// <summary>
        /// Reads from the address for the number of bytes that equals the size of the specified type. This is read
        /// directly into the structure, so no extra copying takes place.
        /// </summary>
        /// <typeparam name="T">The type of object to be populated.</typeparam>
        /// <param name="address">The address containing the data used to populate the object to be returned.</param>
        /// <param name="structure">The structure to be populated.</param>
        public void ReadStructure<T>(IntPtr address, ref T structure)
        {
            GCHandle gch = GCHandle.Alloc(structure, GCHandleType.Pinned);
            IntPtr dataAddress = gch.AddrOfPinnedObject();
            uint nbr = 0;
            WinApi.ReadProcessMemory(this.ProcHandle, address, dataAddress, (uint)Marshal.SizeOf(typeof(T)), out nbr);
            structure = (T)Marshal.PtrToStructure(dataAddress, typeof(T));
            gch.Free();
        }

        /// <summary>
        /// Compares two loaded modules within a process to see which one occurs in lower memory. Similar to memcmp.
        /// </summary>
        /// <param name="m1">The first module to be compared.</param>
        /// <param name="m2">The second module to be compared.</param>
        /// <returns>Returns -1 if m1 is less than m2, 1 if m1 is greater than m2, and 0 if m1 equals m2.</returns>
        private static int CompareModules(ProcessModule m1, ProcessModule m2)
        {
#if _WIN64
            if (m1.BaseAddress.ToInt64() < m2.BaseAddress.ToInt64())
            {
                return -1;
            }
            else if (m1.BaseAddress.ToInt64() > m2.BaseAddress.ToInt64())
            {
                return 1;
#else
            if (m1.BaseAddress.ToInt32() < m2.BaseAddress.ToInt32())
            {
                return -1;
            }
            else if (m1.BaseAddress.ToInt32() > m2.BaseAddress.ToInt32())
            {
                return 1;
#endif
            }
            else
            {
                return 0;
            }
        }

        #endregion
    }
}
