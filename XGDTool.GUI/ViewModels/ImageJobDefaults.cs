using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XGDTool.GUI.ViewModels;

public sealed class ImageJobDefaults
{
    public Lib.Image.Format OutputFormat { get; init; } = Lib.Image.Format.XISO;
    public ImageJobProcessOptions ProcessOptions { get; init; } = ImageJobProcessOptions.Scrub;
    public bool Split { get; init; } = true;
    public bool AllowedMediaPatch { get; init; } = false;
    public bool AttachXbe { get; init; } = true;
}
