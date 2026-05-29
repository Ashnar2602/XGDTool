using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDTool.Format;

namespace XGDTool.Image
{
    public class Writer
    {
        public Writer(Reader reader)
        {

        }

        protected XISO.DirectoryHeader CreateDirectoryHeader(Avl.Node node)
        {
            var header = new XISO.DirectoryHeader();
            var subDirEmpy = node.Subdirectory is Avl.EmptyNode;

            header.LeftOffset = 
                (node.LeftChild != null) 
                    ? (ushort)(node.LeftChild.DirectoryOffset / 4) 
                    : (ushort)0;

            header.RightOffset = 
                (node.RightChild != null)
                    ? (ushort)(node.RightChild.DirectoryOffset / 4) 
                    : (ushort)0;

            header.StartSector = (uint)node.StartSector;

            if (node.Subdirectory != null || subDirEmpy)
            {
                header.FileSize =
                    (uint)node.FileSize +
                    (uint)((XISO.SECTOR_SIZE - (node.FileSize % XISO.SECTOR_SIZE)) % XISO.SECTOR_SIZE);
            }
            else
            {
                header.FileSize = (uint)node.FileSize;
            }

            header.Attributes = (node.Subdirectory != null || subDirEmpy) 
                ? XISO.DirAttribute.Directory 
                : XISO.DirAttribute.File;

            header.NameLength = (byte)Math.Min(node.Filename.Length, 255);

            return header;
        }
    }
}
