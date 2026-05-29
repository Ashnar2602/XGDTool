using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDTool.Format;

namespace XGDTool.Image
{
    public static class ReaderFactory
    {
        public static Reader Create(IReadOnlyList<string> files)
        {
            if (files.Count == 0)
                throw new ArgumentException("No files provided to create a reader.");

            var orderedFiles = files.OrderBy(f => f).ToList();
            var stream = new StreamReader(orderedFiles.First());
            var magicBytes = new byte[XISO.MAGIC_SIZE];

            var xisoOffset = new long[4]
            {
                0,
                XISO.LSEEK_OFFSET_GLOBAL,
                XISO.LSEEK_OFFSET_XGD3,
                XISO.LSEEK_OFFSET_XGD1
            };

            foreach (var offset in xisoOffset)
            {
                stream.BaseStream.Read(
                    magicBytes,
                    (int)(XISO.MAGIC_OFFSET + offset),
                    XISO.MAGIC_SIZE);

                if (magicBytes.AsSpan().SequenceEqual(XISO.MAGIC))
                    return new ReaderXISO(orderedFiles, offset);
            }

            // continue going through the different formats once theyre implemented

            throw new NotSupportedException("The provided file format is not supported.");
        }
    }
}
