using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XGDToolLib.Image.Writers.Base;

public abstract class Passthrough(Reader reader, IWriterOptions options, Title.Info titleTinfo) 
    : Writer(reader, options, titleTinfo)
{
}
