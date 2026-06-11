using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XGDTool.GUI.Services;
//using XGDTool.Lib.Image;
//using XGDTool.Lib.Util;
//using XGDTool.Lib;

namespace XGDTool.GUI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly DialogService _dialogs;
    private readonly ConversionService _conversion;
    private CancellationTokenSource? _cts;

    public ObservableCollection<ImageJobViewModel> Jobs { get; } = new();

    public IReadOnlyList<ImageJobAutoFormat> AutoFormats { get; } = new[]
    {
        ImageJobAutoFormat.None,
        ImageJobAutoFormat.Xbox,
        ImageJobAutoFormat.Xbox360,
        ImageJobAutoFormat.Xemu,
        ImageJobAutoFormat.Xenia
    };

    public IReadOnlyList<Lib.Image.Format> OutputFormats { get; } = new[]
    {
        Lib.Image.Format.Extract,
        Lib.Image.Format.XISO,
        Lib.Image.Format.GOD,
        Lib.Image.Format.CCI,
        Lib.Image.Format.CSO,
        Lib.Image.Format.ZAR
    };

    public IReadOnlyList<ImageJobProcessOptions> ImageProcessOptions { get; } = new[]
    {
        ImageJobProcessOptions.None,
        ImageJobProcessOptions.Reauthor,
        ImageJobProcessOptions.Scrub
    };

    [ObservableProperty]
    private string inputPath = "";

    [ObservableProperty]
    private string outputDirectory = "";

    [ObservableProperty]
    private ImageJobAutoFormat defaultAutoFormat = ImageJobAutoFormat.None;

    [ObservableProperty]
    private Lib.Image.Format defaultOutputFormat = Lib.Image.Format.Extract;

    [ObservableProperty]
    private ImageJobProcessOptions defaultProcessOption = ImageJobProcessOptions.None;

    [ObservableProperty]
    private bool defaultSplit = true;

    [ObservableProperty]
    private bool defaultAttachXbe = true;

    [ObservableProperty]
    private bool defaultAllowedMediaPatch = false;

    [ObservableProperty]
    private bool isRunning = false;

    [ObservableProperty]
    private string statusText = "Idle";

    public MainWindowViewModel(
        DialogService dialogs,
        ConversionService conversion)
    {
        _dialogs = dialogs;
        _conversion = conversion;
    }

    [RelayCommand]
    private async Task BrowseInputDirectoryAsync()
    {
        var path = await _dialogs.PickInputDirectoryAsync();
        if (path == null)
            return;

        InputPath = path;
        await ScanAsync();
    }

    [RelayCommand]
    private async Task BrowseOutputDirectoryAsync()
    {
        var path = await _dialogs.PickOutputDirectoryAsync();
        if (path == null)
            return;

        OutputDirectory = path;
        StartSelectedCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task AddInputFilesAsync()
    {
        var files = await _dialogs.PickInputFilesAsync();

        foreach (var file in files)
        {
            InputPath = file;
            break;
        }

        await ScanAsync();
    }

    //[RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        IsRunning = true;
        StatusText = "Scanning...";
        Jobs.Clear();

        _cts = new CancellationTokenSource();

        try
        {
            //var defaults = BuildDefaults();

            var jobs = await _conversion.ScanAsync(InputPath, _cts.Token);

            foreach (var job in jobs)
                Jobs.Add(job);

            StatusText = $"Found {Jobs.Count} image(s).";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Scan failed: {ex.Message}.";
            //Jobs.Add(new ImageJobViewModel
            //{
            //    SourceEntry = new Lib.Converter.InputEntry(),
            //    TitleInfo = new Lib.Title.Info(),
            //    Selected = false,
            //    InputFormat = "Error",
            //    DiscNumber = "0/0",
            //    TitleName = "",
            //    Status = ImageJobStatus.Failed,
            //    Progress = 0,
            //    Message = ex.Message,
            //});
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            IsRunning = false;
            NotifyCommandStates();
        }
    }

    //private bool CanScan()
    //{
    //    return !IsRunning &&
    //           !string.IsNullOrWhiteSpace(InputPath) &&
    //           !string.IsNullOrWhiteSpace(OutputDirectory);
    //}

    //[RelayCommand]
    //private void ApplyDefaultsToSelected()
    //{
    //    foreach (var job in Jobs.Where(j => j.Selected))
    //        ApplyDefaults(job);
    //}

    //[RelayCommand]
    //private void ApplyDefaultsToAll()
    //{
    //    foreach (var job in Jobs)
    //        ApplyDefaults(job);
    //}

    //private void ApplyDefaults(ImageJobViewModel job)
    //{
    //    job.OutputFormat = DefaultOutputFormat;
    //    job.ProcessOptions = DefaultProcessOptions;
    //    job.AttachXbe = DefaultAttachXbe;
    //    job.Split = DefaultSplit;
    //}

    [RelayCommand(CanExecute = nameof(CanStartSelected))]
    private async Task StartSelectedAsync()
    {
        IsRunning = true;
        //OverallProgress = 0;
        StatusText = "Running...";

        _cts = new CancellationTokenSource();

        var selectedJobs = Jobs.Where(j => j.Selected).ToArray();

        try
        {
            for (int i = 0; i < selectedJobs.Length; i++)
            {
                var job = selectedJobs[i];

                _cts.Token.ThrowIfCancellationRequested();

                job.Status = ImageJobStatus.Running;
                job.Progress = 0;
                job.Message = "Starting...";

                var progress = new Progress<Lib.Converter.Progress>(p =>
                {
                    job.Progress = p.Total == 0
                        ? 0
                        : (double)p.Current / p.Total;

                    job.Message = $"{p.Stage}: {p.Current}/{p.Total}";

                    //OverallProgress =
                    //    (i + job.Progress) / selectedJobs.Length;

                    StatusText =
                        $"Running {i + 1}/{selectedJobs.Length}: {Path.GetFileName(job.TitleName)}";
                });

                try
                {
                    await _conversion.RunJobAsync(BuildDefaults(), job, progress, _cts.Token);

                    job.Progress = 1;
                    job.Status = ImageJobStatus.Done;
                    job.Message = "Done";
                }
                catch (OperationCanceledException)
                {
                    job.Status = ImageJobStatus.Cancelled;
                    job.Message = "Cancelled";
                    throw;
                }
                catch (Exception ex)
                {
                    job.Status = ImageJobStatus.Failed;
                    job.Message = ex.Message;
                }
            }

            //OverallProgress = 1;
            StatusText = "Done.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled.";
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            IsRunning = false;
            NotifyCommandStates();
        }
    }

    private bool CanStartSelected()
    {
        return !IsRunning &&
               Jobs.Any(j => j.Selected) &&
               !string.IsNullOrWhiteSpace(OutputDirectory);
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var job in Jobs)
            job.Selected = true;
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var job in Jobs)
            job.Selected = false;
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        foreach (var job in Jobs.Where(j => j.Selected).ToArray())
            Jobs.Remove(job);

        StartSelectedCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    private bool CanCancel()
    {
        return IsRunning;
    }

    partial void OnInputPathChanged(string value)
    {
        //ScanCommand.NotifyCanExecuteChanged();
        //_cts?.Cancel();
        ScanAsync().ConfigureAwait(false);
    }

    partial void OnOutputDirectoryChanged(string value)
    {
        //ScanCommand.NotifyCanExecuteChanged();
        StartSelectedCommand.NotifyCanExecuteChanged();
    }

    private ImageJobDefaults BuildDefaults()
    {
        return new ImageJobDefaults
        {
            OutputFormat = DefaultOutputFormat,
            ProcessOptions = DefaultProcessOption,
            Split = DefaultSplit,
            AllowedMediaPatch = DefaultAllowedMediaPatch,
            AttachXbe = DefaultAttachXbe
        };
    }

    private void NotifyCommandStates()
    {
        //ScanCommand.NotifyCanExecuteChanged();
        StartSelectedCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }
}
