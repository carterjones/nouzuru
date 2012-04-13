namespace Nouzuru
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Distorm3cs;

    public class FlowAnalyzer : PInteractor
    {
        private List<Page> pageCache = new List<Page>();

        public IntPtr IdentifyFunctionStartAddress(IntPtr address)
        {
            if (!this.IsOpen)
            {
                return IntPtr.Zero;
            }

            if (!SysInteractor.IsInitialized)
            {
                SysInteractor.Init();
            }

            Page p = null;
            IntPtr baseAddress = new IntPtr((address.ToInt64() / SysInteractor.PageSize) * SysInteractor.PageSize);
            foreach (Page cachedPage in this.pageCache)
            {
                if (cachedPage.Address.ToInt64() == baseAddress.ToInt64())
                {
                    p = cachedPage;
                    break;
                }
            }

            if (p == null)
            {
                p = new Page();
                p.Address = baseAddress;
                if (!this.Read(p.Address, p.Data))
                {
                    return IntPtr.Zero;
                }
            }

            if (p.InstructionsDecomposed.Length == 0 || p.InstructionsDisassembled.Count == 0)
            {
                if (this.Is64Bit)
                {
                    if (!p.Disassemble(Distorm.DecodeType.Decode64Bits))
                    {
                        return IntPtr.Zero;
                    }
                }
                else
                {
                    if (!p.Disassemble(Distorm.DecodeType.Decode32Bits))
                    {
                        return IntPtr.Zero;
                    }
                }
            }

            long targetInstIndex = -1;
            for (long i = 0; i < p.Data.Length; ++i)
            {
                if (p.InstructionsDecomposed[i].addr == (ulong)address.ToInt64())
                {
                    targetInstIndex = i;
                }
            }

            if (targetInstIndex == -1)
            {
                return IntPtr.Zero;
            }

            for (long i = targetInstIndex; i >= 0; --i)
            {
                //if (p.InstructionsDecomposed[i].InstructionType == Distorm.InstructionType.)
            }

            return IntPtr.Zero;
        }
    }
}
