using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XGDToolLib.Image.Writer;

internal class ISectorSinkFactory
{
    public static ISectorSink Create(IReader reader, IWriterOptions options, Title.Info titleInfo)
    {
        //return new SectorSink(options, titleInfo);
    }
}
