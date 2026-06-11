using XGDTool.Lib.Image.Formats;
using XGDTool.Lib.Image;

namespace XGDTool.Lib.MockFileSystem;

public static class Create
{
    public static T FromReader<T>(IReader reader) where T : Entry<T, Image.Reader.DirectoryEntry>, new()
    {
        var root = new T
        {
            FileName = "", 
            IsFile = false, 
            Context = null
        };

        var dirEntries = reader.DirectoryEntries;
        var dQueue = new Queue<Image.Reader.DirectoryEntry>(dirEntries);
        RecurseFromReader(ref dQueue, ref root, 0);
        return root;
    }

    private static void RecurseFromReader<T>(ref Queue<Image.Reader.DirectoryEntry> dQueue, ref T parentFsEntry, int depth) where T : Entry<T, Image.Reader.DirectoryEntry>, new()
    {
        if (depth > 255)
            throw new InvalidOperationException("Directory depth exceeds maximum of 255.");

        while (dQueue.Count > 0)
        {
            var dirEntry = dQueue.Dequeue();
            var subFsEntry = new T
            {
                FileName = Path.GetFileName(dirEntry.FilePath),
                IsFile = !dirEntry.Header.Attributes.HasFlag(XISO.DirAttribute.Directory),
                Context = dirEntry
            };

            if (subFsEntry.IsDirectory)
            {
                var subdirEntries = new Queue<Image.Reader.DirectoryEntry>();
                var remainingQueue = new Queue<Image.Reader.DirectoryEntry>();
                var currentPath = dirEntry.FilePath;

                while (dQueue.Count > 0)
                {
                    var item = dQueue.Dequeue();
                    if (item.FilePath.StartsWith(currentPath, StringComparison.OrdinalIgnoreCase))
                        subdirEntries.Enqueue(item);
                    else
                        remainingQueue.Enqueue(item);
                }

                dQueue = remainingQueue;

                if (subdirEntries.Count > 0)
                    RecurseFromReader(ref subdirEntries, ref subFsEntry, depth + 1);
            }
            else if (dirEntry.Header.FileSize == 0)
            {
                continue;
            }

            parentFsEntry.AddSubEntry(subFsEntry);
        }
    }
}
