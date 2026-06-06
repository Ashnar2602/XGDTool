using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDToolLib.Image.Format;

namespace XGDToolLib.Image.Writers.Base;

public abstract class Reauthor(Reader reader, IWriterOptions options, Title.Info titleTinfo) 
    : Writer(reader, options, titleTinfo)
{
    private readonly Avl.Tree AvlTree = new(titleTinfo.TitleName);
    private Lazy<long> TotalXisoBytes => new(() => XISO.CalculateTotalSize(AvlTree.RootNode));
    private CancellationToken CancellationToken;
    private IProgress<Converter.Progress>? Progress;

    protected void WriteXisoHeader()
    {

    }
}
