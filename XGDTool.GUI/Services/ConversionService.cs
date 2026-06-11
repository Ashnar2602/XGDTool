using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XGDTool.GUI.ViewModels;
using XGDTool.Lib.Converter;

namespace XGDTool.GUI.Services;

public sealed class ConversionService
{
    public async Task<IReadOnlyList<ImageJobViewModel>> ScanAsync(
        string inputPath,
        CancellationToken ct)
    {
        //List<InputEntry> inEntries;
        //try
        //{
            var inEntries = await InputHelper.GenerateEntriesAsync(new[] { inputPath }, 1, ct);
        //}
        //catch
        //{
        //    return [];
        //}
        var jobs = new List<ImageJobViewModel>();
        string errorMessage = string.Empty;

        foreach (var entry in inEntries)
        {
            try
            {
                var reader = Lib.Image.IReader.Create(entry.InputFormat, entry.InputPaths);
                await reader.Initialize(ct: ct);

                var headerTool = new Lib.Exe.HeaderTool();
                headerTool.Initialize(reader);

                var titleInfo = new Lib.Title.Info(headerTool);

                jobs.Add(new ImageJobViewModel
                {
                    SourceEntry = entry,
                    TitleInfo = titleInfo,
                    Selected = true,
                    InputFormat = entry.InputFormat,
                    Platform = titleInfo.Platform,
                    DiscNumber = $"{titleInfo.DiscNumber}/{titleInfo.DiscCount}",
                    DiscParts = entry.InputPaths.Count,
                    TitleName = titleInfo.TitleName,
                    Status = ImageJobStatus.Idle,
                    Progress = 0,
                    Message = "Ready"
                });
            }
            catch (Exception ex)
            {
                jobs.Add(new ImageJobViewModel
                {
                    SourceEntry = entry,
                    TitleInfo = new Lib.Title.Info(),
                    Selected = false,
                    InputFormat = Lib.Image.Format.Unknown,
                    Platform = Lib.Exe.Platform.Unknown,
                    DiscNumber = "",
                    DiscParts = 0,
                    TitleName = entry.InputPaths.FirstOrDefault() ?? string.Empty,
                    Status = ImageJobStatus.Failed,
                    Progress = 0,
                    Message = $"Error: {ex.Message}"
                });
                errorMessage = ex.Message;
            }
        }

        if (jobs.Count == 0 && !string.IsNullOrEmpty(errorMessage))
            throw new Exception($"Failed to scan input path. Error: {errorMessage}");

        return jobs;
    }

    public async Task RunJobAsync(
        ImageJobDefaults defaults,
        ImageJobViewModel job,
        IProgress<Lib.Converter.Progress> progress,
        CancellationToken ct)
    {
        /*
         * Replace this with the same conversion orchestration your CLI uses,
         * but built from the row's options.
         *
         * Example:
         *
         * var request = new ConversionRequest
         * {
         *     SourceEntry = (YourDiscoveredEntryType)job.SourceEntry,
         *     OutputDirectory = job.OutputDirectory,
         *     OutputName = job.OutputName,
         *     OutputFormat = job.OutputFormat,
         *     Scrub = job.Scrub,
         *     Reauthor = job.Reauthor,
         *     Split = job.Split
         * };
         *
         * await ConversionRunner.RunAsync(request, progress, ct);
         */

        await Task.CompletedTask;
    }
}
