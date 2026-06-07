using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XGDToolLib.Converter;

[Flags]
public enum Stage
{
    [Description("Idle")]
    Idle,
    [Description("Initializing")]
    Initializing,
    [Description("Parsing directory entries")]
    ParsingDirectoryEntries,
    [Description("Loading data sectors")]
    LoadingDataSectors,
    [Description("Loading security sectors")]
    LoadingSecuritySectors,
    [Description("Building sector ranges")]
    BuildingSectorRanges,
    [Description("Writing data")]
    WritingData,
    [Description("Finalizing")]
    Finalizing,
    [Description("Generating attach XBE")]
    GeneratingAttachXBE,
    [Description("Cancelled")]
    Cancelled,
    [Description("Done")]
    Done
}
