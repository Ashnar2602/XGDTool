using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDTool.Format;

namespace XGDTool.Image
{
    internal class ReaderXISO : Reader
    {
        public override long TotalSectors { get; protected set; }
        public override long ImageOffset { get; protected set; }
        public override Format.Image ImageType => Format.Image.XISO;

        private readonly IoStream.In InStream;

        public ReaderXISO(IReadOnlyList<string> files, long imageOffset) : base(files)
        {
            InStream = new IoStream.In(files);
            TotalSectors = (uint)(InStream.Length / XISO.SECTOR_SIZE);
            ImageOffset = imageOffset;
        }

        public override byte[] ReadSector(uint sector)
        {
            InStream.Seek(sector * XISO.SECTOR_SIZE);
            return InStream.Read(XISO.SECTOR_SIZE);
        }

        public override byte[] ReadBytes(long offset, long size)
        {
            InStream.Seek(offset);
            return InStream.Read(size);
        }
    }
}
