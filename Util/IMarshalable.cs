using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace XGDTool.Util
{
    public abstract class IMarshalable
    {
        public abstract int Size();

        public virtual void FromBytes(byte[] data)
        {
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                Marshal.PtrToStructure(handle.AddrOfPinnedObject(), this);
            }
            finally
            {
                handle.Free();
            }
        }

        public virtual byte[] ToBytes()
        {
            var data = new byte[this.Size()];
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                Marshal.StructureToPtr(this, handle.AddrOfPinnedObject(), false);
            }
            finally
            {
                handle.Free();
            }
            return data;
        }
    }
}
