using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDToolLib.Image.Format;
using XGDToolLib.Util;

namespace XGDToolLib.Image.Writers;

internal class XisoReauthor : Writer
{
    private readonly SplitIoStream.Out Out;
    private CancellationToken CancellationToken;
    private IProgress<Converter.Progress>? Progress;
    private Converter.Progress ProgData = new();

    public XisoReauthor(Reader reader, Options options, Title.Info titleInfo)
        : base(reader, options, titleInfo)
    {
        Out = new SplitIoStream.Out(
            Path.Join(OutOptions.OutDirectory, TitleInfo.ImageName + ".iso"),
            OutOptions.Split ?? false ? XISO.SPLIT_MARGIN : null);
    }

    public override Task<IReadOnlyList<string>> Convert(
        IProgress<Converter.Progress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        CancellationToken = cancellationToken;
        Progress = progress;

        Avl.Tree avlTree = new("Root");
        avlTree.BuildTree(Reader.DirectoryEntries);

        ProgData.Stage = Converter.Stage.WritingData;
        ProgData.Current = 0;
        ProgData.Total = avlTree.TotalBytes;

        WriteHeader(avlTree);

        Out.Seek(avlTree.RootNode.StartSector * XISO.SECTOR_SIZE);

        var root = avlTree.RootNode;
        int _ = 0;
        Avl.Tree.Traverse(Avl.Traversal.Prefix, ref root, 0, WriteTreeCb, ref _);

        if (CancellationToken.IsCancellationRequested)
            return Task.FromCanceled<IReadOnlyList<string>>(CancellationToken);

        Out.Seek(0, SeekOrigin.End);
        PadToModulus(XISO.FILE_MODULUS, 0);

        ProgData.Current = ProgData.Total;
        Progress?.Report(ProgData);

        return Task.FromResult(Out.Filepaths);
    }

    protected void WriteTreeCb(ref Avl.Node node, int depth, ref int _)
    {
        if (CancellationToken.IsCancellationRequested)
            return;

        if (node.Subdirectory == null)
            return;

        if (node.Subdirectory is not Avl.EmptyNode)
        {
            Avl.Tree.Traverse(Avl.Traversal.Prefix, ref node.Subdirectory, 0, WriteFileCb, ref _);
            Avl.Tree.Traverse(Avl.Traversal.Prefix, ref node.Subdirectory, 0, WriteTreeCb, ref _);

            Out.Seek(node.StartSector * XISO.SECTOR_SIZE);

            Avl.Tree.Traverse(Avl.Traversal.Prefix, ref node.Subdirectory, 0, WriteEntryCb, ref _);

            PadToModulus(XISO.SECTOR_SIZE, XISO.PAD_BYTE);
        }
        else
        {
            var padBytes = new byte[XISO.SECTOR_SIZE];
            Array.Fill(padBytes, XISO.PAD_BYTE);

            Out.Seek(node.StartSector * XISO.SECTOR_SIZE);
            Out.Write(padBytes);
        }
    }

    private void WriteFileCb(ref Avl.Node node, int depth, ref int _)
    {
        if (CancellationToken.IsCancellationRequested)
            return;

        if (node.Subdirectory != null || node is Avl.EmptyNode)
            return;

        Out.Seek(node.StartSector * XISO.SECTOR_SIZE);

        long remaining = node.FileSize;
        long offset = Reader.ImageOffset + node.OldStartSector * XISO.SECTOR_SIZE;
        var buffer = new byte[Math.Min(BUFFER_SIZE, remaining)];

        while (remaining > 0)
        {
            var readLen = (int)Math.Min(BUFFER_SIZE, remaining);

            Reader.ReadBytes(offset, buffer.AsSpan(0, readLen));

            Out.Write(buffer);
            offset += readLen;
            remaining -= readLen;

            ProgData.Current += readLen;
            Progress?.Report(ProgData);

            if (CancellationToken.IsCancellationRequested)
                return;
        }

        if ((node.FileSize + (node.StartSector * XISO.SECTOR_SIZE)) != Out.Position)
            throw new Exception($"File {node.Filename} was not written correctly.");

        PadToModulus(XISO.SECTOR_SIZE, XISO.PAD_BYTE);
    }

    private void WriteEntryCb(ref Avl.Node node, int depth, ref int _)
    {
        if (CancellationToken.IsCancellationRequested)
            return;

        var entry = CreateDirectoryEntry(node);
        var padLen = node.DirectoryOffset + node.DirectoryStart - Out.Position;
        var padBuffer = new byte[padLen];
        Array.Fill(padBuffer, XISO.PAD_BYTE);

        Out.Write(padBuffer);
        Out.Write(entry.ToBytes());
    }

    private void WriteHeader(Avl.Tree avlTree)
    {
        var header = new XISO.FileHeader
        (
            (uint)avlTree.RootNode.StartSector,
            (uint)avlTree.RootNode.FileSize,
            (uint)(XISO.CalculateTotalSize(avlTree.RootNode) / XISO.SECTOR_SIZE)
        );

        Out.Seek(0);
        Out.Write(header.ToBytes());
    }

    private void PadToModulus(long modulus, byte padByte)
    {
        if ((Out.Position % modulus) == 0)
            return;

        var padLen = modulus - (Out.Position % modulus);
        var padBytes = new byte[padLen];
        Array.Fill(padBytes, padByte);
        Out.Write(padBytes);
    }
}
