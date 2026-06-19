using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

using XGDTool.Lib.Util;
using XGDTool.Lib.Image.Formats;

namespace XGDTool.Lib.Tests;

public class ISerializableTests
{
    private static readonly Random Rng = new();

    // Excluded: each of these intentionally discards or normalizes part of the
    // input on deserialize (variable-length name, type-discriminator bit, reserved
    // padding), so byte-for-byte equality with arbitrary random input doesn't hold.
    // Each has its own test below asserting the real invariant instead.
    private static readonly Type[] Excluded =
    [
        typeof(XDVDFS.DirectoryEntry),
        typeof(XDVDFS.VolumeDescriptor),
        typeof(ZAR.DirectoryEntry),
        typeof(ZAR.FileEntry),
    ];

    public static IEnumerable<object[]> SerializableTypes() =>
        typeof(ISerializable).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ISerializable).IsAssignableFrom(t))
            .Where(t => t.GetConstructor(Type.EmptyTypes) != null)
            .Where(t => !Excluded.Contains(t))
            .Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(SerializableTypes))]
    public void RoundTrips_IsIdempotent(Type type)
    {
        // Start from random bytes, do one full cycle to produce a valid serialized
        // form (reserved/padding bytes are normalised to whatever Serialize writes),
        // then assert that a second cycle produces identical output.
        var instance = (ISerializable)Activator.CreateInstance(type)!;

        var seed = new byte[instance.Size()];
        Rng.NextBytes(seed);
        instance.Deserialize(seed);

        var first = new byte[instance.Size()];
        instance.Serialize(first);

        instance.Deserialize(first);

        var second = new byte[instance.Size()];
        instance.Serialize(second);

        if (!first.AsSpan().SequenceEqual(second))
        {
            int pos = Enumerable.Range(0, first.Length).First(i => first[i] != second[i]);
            Assert.Fail($"{type.FullName} serialize/deserialize is not idempotent: bytes differ at position {pos}");
        }
    }

    [Fact]
    public void XdvdfsDirectoryEntry_RoundTrips_WithRandomName()
    {
        var entry = new XDVDFS.DirectoryEntry
        {
            LeftOffset = (ushort)Rng.Next(),
            RightOffset = (ushort)Rng.Next(),
            StartSector = (uint)Rng.Next(),
            FileSize = (uint)Rng.Next(),
            Attributes = XDVDFS.DirAttributes.Normal | XDVDFS.DirAttributes.File,
        };
        entry.SetName(RandomAsciiName(Rng.Next(1, 32)));

        var original = new byte[entry.Size()];
        entry.Serialize(original);

        var roundTripped = new XDVDFS.DirectoryEntry();
        roundTripped.Deserialize(original);

        var reSerialized = new byte[roundTripped.Size()];
        roundTripped.Serialize(reSerialized);

        Assert.Equal(original, reSerialized);
    }

    [Fact]
    public void XdvdfsVolumeDescriptor_RoundTrips_IgnoringReservedPadding()
    {
        var original = new byte[XDVDFS.VolumeDescriptor.SIZE];
        Rng.NextBytes(original);

        var descriptor = new XDVDFS.VolumeDescriptor();
        descriptor.Deserialize(original);

        var serialized = new byte[descriptor.Size()];
        descriptor.Serialize(serialized);

        // Bytes between FileTime and Magic2 are reserved padding, not captured by
        // Deserialize, and are always zeroed by Serialize.
        int reservedStart = XDVDFS.MAGIC_SIZE + sizeof(uint) + sizeof(uint) + sizeof(ulong);
        int reservedEnd = XDVDFS.VolumeDescriptor.SIZE - XDVDFS.MAGIC_SIZE;
        Array.Clear(original, reservedStart, reservedEnd - reservedStart);

        Assert.Equal(original, serialized);
    }

    [Fact]
    public void ZarDirectoryEntry_RoundTrips_WithTypeBitCleared()
    {
        var original = new byte[ZAR.DirectoryEntry.SIZE];
        Rng.NextBytes(original);

        var entry = new ZAR.DirectoryEntry();
        entry.Deserialize(original);

        var serialized = new byte[entry.Size()];
        entry.Serialize(serialized);

        // Top bit of NameOffset is the file/directory discriminator; a DirectoryEntry
        // always clears it regardless of what was in the input.
        original[0] &= 0x7F;

        // DirectoryEntry only carries 12 of the fixed 16-byte slot's bytes
        // (NameOffset, NodeStartIndex, NodeCount); the rest is unused padding.
        Array.Clear(original, 12, 4);

        Assert.Equal(original, serialized);
    }

    [Fact]
    public void ZarFileEntry_RoundTrips_WithTypeBitSet()
    {
        var original = new byte[ZAR.FileEntry.SIZE];
        Rng.NextBytes(original);

        var entry = new ZAR.FileEntry();
        entry.Deserialize(original);

        var serialized = new byte[entry.Size()];
        entry.Serialize(serialized);

        // Top bit of NameOffset is the file/directory discriminator; a FileEntry
        // always sets it regardless of what was in the input.
        original[0] |= 0x80;

        Assert.Equal(original, serialized);
    }

    private static string RandomAsciiName(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        return new string([.. Enumerable.Range(0, length).Select(_ => chars[Rng.Next(chars.Length)])]);
    }
}
