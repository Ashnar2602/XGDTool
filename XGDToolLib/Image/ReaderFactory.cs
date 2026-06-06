using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDToolLib.Image.Format;

namespace XGDToolLib.Image;

public static class ReaderFactory
{
    public static Reader Create(IReadOnlyList<string> files)
    {
        if (files.Count == 0)
            throw new ArgumentException("No files provided.");

        if (Readers.Extract.IsValid(files[0]))
            return new Readers.Extract(files[0]);

        if (Readers.Xiso.IsValid(files, out var _))
            return new Readers.Xiso(files);

        // continue going through the different formats once theyre implemented

        throw new NotSupportedException("The provided file format is not supported.");
    }
}
