using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace XGDTool.GUI.Services;

public class DialogService
{
    private readonly Window _owner;

    public DialogService(Window owner)
    {
        _owner = owner;
    }

    public async Task<string?> PickInputDirectoryAsync()
    {
        var folders = await _owner.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Select input directory",
                AllowMultiple = false
            });

        return folders.Count > 0
            ? folders[0].Path.LocalPath
            : null;
    }

    public async Task<string?> PickOutputDirectoryAsync()
    {
        var folders = await _owner.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Select output directory",
                AllowMultiple = false
            });

        return folders.Count > 0
            ? folders[0].Path.LocalPath
            : null;
    }

    public async Task<IReadOnlyList<string>> PickInputFilesAsync()
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Select Xbox image files",
                AllowMultiple = true,
                FileTypeFilter =
                [
                    new FilePickerFileType("Xbox image files")
                    {
                        Patterns =
                        [
                            "*.iso",
                            "*.iso.old",
                            "*.cci",
                            "*.cso",
                            "*.zar"
                        ]
                    },
                    FilePickerFileTypes.All
                ]
            });

        return files
            .Select(f => f.Path.LocalPath)
            .ToArray();
    }
}
