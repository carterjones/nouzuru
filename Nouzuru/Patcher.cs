namespace Nouzuru
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using Logger;

    /// <summary>
    /// Patches an external process, using easy-to-use functions.
    /// </summary>
    public class Patcher : PInteractor
    {
        #region Fields

        /// <summary>
        /// A semaphore, used by the freeze thread.
        /// </summary>
        private object saveValuesLock = new object();

        /// <summary>
        /// A list of values that have been modified by this patcher.
        /// </summary>
        private List<Address> savedValues = new List<Address>();

        /// <summary>
        /// A thread used for setting addresses to 'frozen' values, at a specified interval.
        /// </summary>
        private Thread freezeThread;

        #endregion

        #region Enumerations

        /// <summary>
        /// Options that can be set when calling Patcher.Write(...).
        /// </summary>
        [Flags]
        public enum WriteOptions : uint
        {
            /// <summary>
            /// No write options are set.
            /// </summary>
            None = 0,

            /// <summary>
            /// The old value will be saved for future restoration.
            /// </summary>
            SaveOldValue = 1,

            /// <summary>
            /// The new value will be frozen.
            /// </summary>
            FreezeNewValue = 2
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the number of milliseconds to pause before setting values to their 'frozen' values.
        /// </summary>
        public int FreezeFrequency { get; set; }

        /// <summary>
        /// Gets the list of addresses that have been modified by this Patcher.
        /// </summary>
        public List<Address> SavedAddresses
        {
            get
            {
                return this.savedValues;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Adds the specified address to the list of frozen addresses.
        /// </summary>
        /// <param name="address">The address at which the new value will be written.</param>
        /// <param name="newValue">The new value to be frozen.</param>
        /// <returns>Returns true on successfully freezing the address.</returns>
        public bool Freeze(IntPtr address, byte[] newValue)
        {
            byte[] oldValue = new byte[newValue.Length];
            if (!this.Read(address, oldValue))
            {
                this.Status.Log(
                    "There was an error reading from 0x" + address.ToString("x") + " before freezing memory.",
                    Logger.Level.HIGH);
                return false;
            }

            this.SaveAddress(new Address(address, true, oldValue, newValue));
            return true;
        }

        /// <summary>
        /// Adds the specified address to the list of frozen addresses.
        /// </summary>
        /// <param name="address">The address at which the saved value will be written.</param>
        /// <param name="numBytes">The number of bytes to be written.</param>
        /// <returns>Returns true on successfully freezing the address.</returns>
        public bool Freeze(IntPtr address, uint numBytes = 1)
        {
            byte[] oldValue = new byte[numBytes];
            return this.Freeze(address, oldValue);
        }

        /// <summary>
        /// Writes a NOP sled at the provided address for the specified number of bytes.
        /// </summary>
        /// <param name="address">The address at which the nop sled will be written.</param>
        /// <param name="numBytes">The number of 0x90 bytes to be written.</param>
        /// <returns>Returns true on successful write.</returns>
        public bool NOP(IntPtr address, uint numBytes)
        {
            byte[] nop = new byte[numBytes];
            for (uint i = 0; i < numBytes; ++i)
            {
                nop[i] = 0x90;
            }

            return this.Write(address, nop);
        }

        /// <summary>
        /// Writes a NOP sled at the provided address for the specified number of bytes.
        /// </summary>
        /// <param name="address">The address at which the nop sled will be written.</param>
        /// <param name="numBytes">The number of 0x90 bytes to be written.</param>
        /// <returns>Returns true on successful write.</returns>
        public bool NOP(ulong address, uint numBytes)
        {
            return this.NOP(new IntPtr((long)address), numBytes);
        }

        /// <summary>
        /// Saves original data at the specified address, for setting it to its original value in the future. Writes
        /// NOP bytes to the specified address for the amount of bytes necessary to NOP the entire instruction.
        /// </summary>
        /// <param name="address">The address at which the nops will be written.</param>
        /// <returns>Returns true on successful NOPing.</returns>
        public bool NOPInstruction(IntPtr address)
        {
            int instSize = GetInstructionSize(address);
            if (instSize == -1)
            {
                this.Status.Log(
                    "There was an error determining the size of the instruction at 0x" + address.ToString("x") +
                    " prior to NOPing the instruction.",
                    Logger.Level.HIGH);
                return false;
            }

            byte[] nops = new byte[instSize];
            for (int i = 0; i < nops.Length; ++i)
            {
                nops[i] = (byte)0x90;
            }

            return this.Write(address, nops, Patcher.WriteOptions.SaveOldValue);
        }

        /// <summary>
        /// Saves original data at the specified address, for setting it to its original value in the future. Writes
        /// NOP bytes to the specified address for the amount of bytes necessary to NOP the entire instruction.
        /// </summary>
        /// <param name="address">The address at which the nops will be written.</param>
        /// <returns>Returns true on successful NOPing.</returns>
        public bool NOPInstruction(ulong address)
        {
            return this.NOPInstruction(new IntPtr((long)address));
        }

        /// <summary>
        /// Restores the original value at the specified address, if it has been saved. Removes the address from the
        /// list of saved addresses, if desired.
        /// </summary>
        /// <param name="address">The address that will have its original value restored.</param>
        /// <param name="removeFromSavedAddresses">
        /// If true, the address will no longer be saved for future restoration.
        /// </param>
        /// <returns>Returns true on successful write.</returns>
        public bool Restore(IntPtr address, bool removeFromSavedAddresses = true)
        {
            if (!this.IsOpen)
            {
                this.Status.Log(
                    "Unable to restore memory, because the target process has not been opened.",
                    Logger.Level.HIGH);
                return false;
            }

            Address savedAddress = this.savedValues.Find(a => a.AsIntPtr == address);
            if (savedAddress == null)
            {
                this.Status.Log(
                    "Could not find the specified address, 0x" + address.ToString("x") + ", in saved list.",
                    Logger.Level.HIGH);
                return false;
            }

            if (this.Write(savedAddress.AsIntPtr, savedAddress.OldValue, Patcher.WriteOptions.None))
            {
                if (removeFromSavedAddresses)
                {
                    lock (this.saveValuesLock)
                    {
                        this.savedValues.Remove(savedAddress);

                        // Stop the freeze thread, if this is the last item in the saved address list.
                        if (this.savedValues.Count == 0 && this.freezeThread != null)
                        {
                            this.freezeThread.Abort();
                        }
                    }
                }

                return true;
            }

            this.Status.Log(
                "Unable to restore the original value to 0x" + address.ToString("x") + ".",
                Logger.Level.HIGH);
            return false;
        }

        /// <summary>
        /// Restores all original values at their respective addresses. Optionally removes these addresses from the
        /// list of saved addresses.
        /// </summary>
        /// <param name="removeFromSavedAddresses">
        /// If true, the saved addresses will no longer remain saved for future restoration.
        /// </param>
        /// <returns>Returns true on successful restoration of values.</returns>
        public bool RestoreAll(bool removeFromSavedAddresses = true)
        {
            if (!this.IsOpen)
            {
                this.Status.Log(
                    "Unable to restore memory, because the target process has not been opened.",
                    Logger.Level.HIGH);
                return false;
            }

            foreach (Address address in this.savedValues)
            {
                if (!this.Write(address.AsIntPtr, address.OldValue, Patcher.WriteOptions.None))
                {
                    this.Status.Log(
                        "Could not write value (" + address.OldValue.ToString() + ") to 0x" + address.AsString + ".",
                        Logger.Level.HIGH);
                    return false;
                }
            }

            if (removeFromSavedAddresses)
            {
                lock (this.saveValuesLock)
                {
                    this.savedValues.Clear();

                    // Stop the freeze thread, since no items remain in the addresss list.
                    if (this.freezeThread != null)
                    {
                        this.freezeThread.Abort();
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Starts this Patcher's freeze thread.
        /// </summary>
        public void StartFreezeThread()
        {
            this.FreezeFrequency = 100;
            this.freezeThread = new Thread(() => this.FreezeThread());
            this.freezeThread.IsBackground = true;
            this.freezeThread.Start();
        }

        /// <summary>
        /// Modifies the specified address to be unfrozen and
        ///   1) optionally restore the original value and
        ///   2) optionally remove it from the list of saved addresses.
        /// </summary>
        /// <param name="address">The address that will be unfrozen.</param>
        /// <param name="restoreValue">
        /// If true, the original value will be written at the address to be unfrozen.
        /// </param>
        /// <param name="removeFromSavedAddresses">
        /// If true, the saved addresses will no longer remain saved for future restoration.
        /// </param>
        /// <returns>Returns true on successfully unfreezing the value.</returns>
        public bool UnFreeze(IntPtr address, bool restoreValue = false, bool removeFromSavedAddresses = true)
        {
            Address frozenAddress = this.savedValues.Find(a => a.AsIntPtr.Equals(address));
            if (frozenAddress == null)
            {
                this.Status.Log(
                    "Could not find the specified address, 0x" + address.ToString("x") + ", in saved list.",
                    Logger.Level.HIGH);
                return false;
            }

            if (restoreValue)
            {
                if (!this.Write(frozenAddress.AsIntPtr, frozenAddress.OldValue, Patcher.WriteOptions.None))
                {
                    this.Status.Log(
                        "Could not restore the old value to to 0x" + address.ToString("x") + ", while unfreezing.",
                        Logger.Level.HIGH);
                    return false;
                }
            }

            if (removeFromSavedAddresses)
            {
                lock (this.saveValuesLock)
                {
                    this.savedValues.Remove(frozenAddress);
                }
            }

            frozenAddress.IsFrozen = false;
            return true;
        }

        /// <summary>
        /// Writes to the address for size bytes from data. Saves the old value, if desired.
        /// </summary>
        /// <param name="address">The address to which a new value will be written.</param>
        /// <param name="newValue">The new value to be written.</param>
        /// <param name="options">Various options that determine what happens with the old and new address.</param>
        /// <returns>Returns the result of WriteProcessMemory().</returns>
        public bool Write(IntPtr address, byte newValue, Patcher.WriteOptions options = WriteOptions.SaveOldValue)
        {
            byte[] newValueArray = new byte[1];
            newValueArray[0] = newValue;
            return this.Write(address, newValueArray, options);
        }

        /// <summary>
        /// Writes to the address for size bytes from data. Saves the old value, if desired.
        /// </summary>
        /// <param name="address">The address to which a new value will be written.</param>
        /// <param name="newValue">The new value to be written.</param>
        /// <param name="options">Various options that determine what happens with the old and new address.</param>
        /// <returns>Returns the result of WriteProcessMemory().</returns>
        public bool Write(IntPtr address, byte[] newValue, Patcher.WriteOptions options = WriteOptions.SaveOldValue)
        {
            if (!this.IsOpen)
            {
                this.Status.Log(
                    "Unable to write memory, because the target process has not been opened.",
                    Logger.Level.HIGH);
                return false;
            }

            byte[] oldValue = null;
            if (((uint)options & (uint)WriteOptions.SaveOldValue) > 0)
            {
                oldValue = new byte[newValue.Length];
                if (!this.Read(address, oldValue))
                {
                    this.Status.Log(
                        "There was an error reading from 0x" + address.ToString("x") + " before writing to memory.",
                        Logger.Level.HIGH);
                    return false;
                }
            }

            uint nbw = 0;
            if (!WinApi.WriteProcessMemory(this.ProcHandle, address, newValue, (uint)newValue.Length, out nbw))
            {
                this.Status.Log(
                    "Could not write value (" + newValue.ToString() + ") to 0x" + address.ToString("x") + ".",
                    Logger.Level.HIGH);
                return false;
            }

            Address newAddress =
                new Address(address, (((uint)options & (uint)WriteOptions.FreezeNewValue) > 0), oldValue, newValue);

            if ((((uint)options & (uint)WriteOptions.SaveOldValue) > 0) ||
                (((uint)options & (uint)WriteOptions.FreezeNewValue) > 0))
            {
                this.SaveAddress(newAddress);
            }

            return true;
        }

        /// <summary>
        /// Writes the supplied strsucture to the specified address in memory.
        /// </summary>
        /// <typeparam name="T">The type of object to be populated.</typeparam>
        /// <param name="address">The address containing the data used to populate the object to be returned.</param>
        /// <param name="newValue">The structure to be written to memory.</param>
        /// <param name="options">Various options that determine what happens with the old and new address.</param>
        /// <returns>Returns a structure that has been populated with data from the specified address.</returns>
        public bool WriteStructure<T>(
            IntPtr address, T newValue, Patcher.WriteOptions options = WriteOptions.SaveOldValue)
        {
            byte[] bytes = newValue.GetBytes();

            IntPtr allocMem = WinApi.VirtualAllocEx(
                this.ProcHandle,
                address,
                (uint)bytes.Length,
                WinApi.MemoryState.MEM_COMMIT | WinApi.MemoryState.MEM_RESERVE,
                WinApi.MemoryProtect.PAGE_READWRITE);

            if (allocMem == IntPtr.Zero)
            {
                return false;
            }

            uint nbw = 0;
            return WinApi.WriteProcessMemory(this.ProcHandle, address, bytes, (uint)Marshal.SizeOf(typeof(T)), out nbw);
        }

        /// <summary>
        /// The function that writes the 'frozen' addresses with the desired values.
        /// </summary>
        protected virtual void FreezeThread()
        {
            // TODO: Verify that this function works properly.
            while (true)
            {
                lock (this.saveValuesLock)
                {
                    foreach (Address address in this.savedValues)
                    {
                        if (address.IsFrozen)
                        {
                            if (!this.Write(address.AsIntPtr, address.CurValue, Patcher.WriteOptions.None))
                            {
                                Logger.Log(
                                    "Could not freeze the value at " + address.AsString + ".",
                                    "nouzuru_freezethread.log",
                                    Logger.Level.HIGH);
                            }
                        }
                    }
                }

                System.Threading.Thread.Sleep(this.FreezeFrequency);
            }
        }

        /// <summary>
        /// Adds an address to the list of saved addresses.
        /// </summary>
        /// <param name="address">The address to be added.</param>
        private void SaveAddress(Address address)
        {
            for (int i = 0; i < this.savedValues.Count; ++i)
            {
                if (this.savedValues[i].AsIntPtr.Equals(address.AsIntPtr))
                {
                    this.savedValues[i].CurValue = address.CurValue;
                    this.savedValues[i].IsFrozen = address.IsFrozen;
                    return;
                }
            }

            lock (this.saveValuesLock)
            {
                this.savedValues.Add(address);
            }

            // Start the freeze thread, if it is not currently running.
            if (this.freezeThread == null || !this.freezeThread.IsAlive)
            {
                this.StartFreezeThread();
            }
        }

        #endregion
    }
}
