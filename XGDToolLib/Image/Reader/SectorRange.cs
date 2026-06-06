using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XGDToolLib.Image.Reader;

public readonly struct SectorRange(uint start, uint endExclusive)
{
    public readonly uint Start = start;
    public readonly uint EndExclusive = endExclusive;

    public bool Contains(uint sector) => sector >= Start && sector < EndExclusive;
}
