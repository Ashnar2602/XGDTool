using System.Collections.ObjectModel;
using System.Threading;
using System.IO;
using Avalonia.Controls;
using Avalonia.Controls.Converters;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XGDTool.GUI.ViewModels;

public partial class ImageJobViewModel : ObservableObject
{
    public required Lib.Converter.InputEntry SourceEntry { get; init; }
    public required Lib.Title.Info TitleInfo { get; init; }

    [ObservableProperty]
    private bool selected = true;

    [ObservableProperty]
    private Lib.Image.Format inputFormat = Lib.Image.Format.Unknown;

    [ObservableProperty]
    private Lib.Exe.Platform platform = Lib.Exe.Platform.Xbox;

    [ObservableProperty]
    private string discNumber = "";

    [ObservableProperty]
    private int discParts = 1;

    [ObservableProperty]
    private string titleName = "";

    [ObservableProperty]
    private ImageJobStatus status = ImageJobStatus.Idle;

    [ObservableProperty]
    private double progress = 0.0;

    [ObservableProperty]
    private string message = "";

    //public string OutputPath => OutputDirectory;
    public string ImageTitleName => TitleInfo.TitleName;

    //partial void OnOutputDirectoryChanged(string value) => OnPropertyChanged(nameof(OutputPath));
    //partial void OnOutputTitleNameChanged(string value) => OnPropertyChanged(nameof(OutputPath));
    partial void OnTitleNameChanged(string value) => OnPropertyChanged(nameof(ImageTitleName));
}
