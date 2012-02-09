namespace Nouzuru
{
    using System;

    /// <summary>
    /// Represents an address in memory.
    /// Data stored at the specified address can be held in an instance of this class.
    /// </summary>
    public class Address
    {
        #region Fields

        /// <summary>
        /// The literal address of the point in memory.
        /// </summary>
        private IntPtr address;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the Address class.
        /// </summary>
        /// <param name="address">A pointer to the address in memory.</param>
        /// <param name="isFrozen">
        /// A value indicating whether or not the address should be frozen by a freeze thread.
        /// </param>
        /// <param name="oldValue">The old value at this address.</param>
        /// <param name="curValue">The current value at this address.</param>
        public Address(IntPtr address, bool isFrozen = false, byte[] oldValue = null, byte[] curValue = null)
        {
            this.address = address;
            this.IsFrozen = isFrozen;
            this.OldValue = oldValue;
            this.CurValue = curValue;
        }

        /// <summary>
        /// Initializes a new instance of the Address class.
        /// </summary>
        public Address()
            : this(IntPtr.Zero)
        {
        }

        /// <summary>
        /// Initializes a new instance of the Address class.
        /// </summary>
        /// <param name="address">A value representing the address in memory.</param>
        public Address(int address)
            : this(new IntPtr(address))
        {
        }

        /// <summary>
        /// Initializes a new instance of the Address class.
        /// </summary>
        /// <param name="address">A value representing the address in memory.</param>
        public Address(uint address)
            : this(new IntPtr(address))
        {
        }

        /// <summary>
        /// Initializes a new instance of the Address class.
        /// </summary>
        /// <param name="address">A value representing the address in memory.</param>
        public Address(long address)
            : this(new IntPtr(address))
        {
        }

        /// <summary>
        /// Initializes a new instance of the Address class.
        /// </summary>
        /// <param name="address">A value representing the address in memory.</param>
        public Address(ulong address)
            : this(new IntPtr((long)address))
        {
        }

        /// <summary>
        /// Initializes a new instance of the Address class.
        /// </summary>
        /// <param name="address">A value representing the address in memory.</param>
        /// <param name="size">The amount of bytes to be cached for this address.</param>
        public Address(IntPtr address, uint size)
            : this(address, false, new byte[size], new byte[size])
        {
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the address, as an IntPtr.
        /// </summary>
        public IntPtr AsIntPtr
        {
            get
            {
                return this.address;
            }
        }

        /// <summary>
        /// Gets the address, as a string.
        /// </summary>
        public string AsString
        {
            get
            {
                return "0x" + this.address.ToString("X").PadLeft(IntPtr.Size * 2, '0');
            }
        }

        /// <summary>
        /// Gets the address, as an unsigned long integer.
        /// </summary>
        public ulong AsUlong
        {
            get
            {
                return (ulong)this.address.ToInt64();
            }
        }

        /// <summary>
        /// Gets or sets the current value, stored at this address.
        /// </summary>
        public byte[] CurValue { get; set; }

        /// <summary>
        /// Gets or sets the old value, stored at this address.
        /// </summary>
        public byte[] OldValue { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the address's value is being frozen by a freeze thread.
        /// </summary>
        public bool IsFrozen { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Determines the equality of an object compared to this Address instance.
        /// </summary>
        /// <param name="obj">The object being compared.</param>
        /// <returns>
        /// Returns true if the the object being compared as the same values as this Address instance.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (this.GetType() != obj.GetType())
            {
                return false;
            }

            Address other = (Address)obj;
            return this.address.Equals(other.address);
        }

        /// <summary>
        /// Creates a hash code, for use in uniquely identifying this Address instance.
        /// </summary>
        /// <returns>Returns a unique identifier for this Address instance.</returns>
        public override int GetHashCode()
        {
            int hash = 17;
            hash = (hash * 23) + this.address.GetHashCode();
            hash = (hash * 23) + this.OldValue.GetHashCode();
            hash = (hash * 23) + this.CurValue.GetHashCode();
            return hash;
        }

        #endregion
    }
}
