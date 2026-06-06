using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XGDToolLib.Image;

public static class IWriterFactory
{
    public static IWriter Create(IReader reader, IWriterOptions options)
    {
        var titleInfo = Title.Resolver.Resolve(reader);
        var reauthor = options.ConvertType == Converter.Type.Reauthor;

        return options.ImageType switch
        {
            Type.XISO => reauthor
                ? new Writers.XisoReauthor(reader, options, titleInfo)
                : new Writers.XisoPassthrough(reader, options, titleInfo),
            Type.GOD => reauthor
                ? new Writers.GodReauthor(reader, options, titleInfo)
                : new Writers.GodPassthrough(reader, options, titleInfo),
            //Type.CCI => new Writers.CCI(reader, options, titleInfo),
            //Type.CSO => new Writers.CSO(reader, options, titleInfo),
            Type.Extract => new Writers.Extract(reader, options, titleInfo),
            _ => throw new NotImplementedException($"Writer for {options.ImageType} is not implemented.")
        };
    }
}
