using System.Buffers.Binary;
using System.IO;
using System.Reflection;

namespace ProcessLens.App;

// Reads only this assembly's embedded ICO. No filesystem path or WPF pack URI.
internal static class IconFile
{
    internal const string ResourceName = "ProcessLens.AppIcon";
    internal const int MaximumBytes = 256 * 1024;
    internal static byte[]? ReadEmbedded(Assembly assembly)
    {
        using var stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null || stream.Length is <= 0 or > MaximumBytes) return null;
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    internal static bool TrySelectImage(byte[]? file, int desiredSize, out byte[] bits)
    {
        bits = Array.Empty<byte>();
        if (file is null || file.Length is < 6 or > MaximumBytes || desiredSize is < 1 or > 256) return false;
        var data = file.AsSpan();
        if (BinaryPrimitives.ReadUInt16LittleEndian(data) != 0 || BinaryPrimitives.ReadUInt16LittleEndian(data[2..]) != 1) return false;
        int count = BinaryPrimitives.ReadUInt16LittleEndian(data[4..]);
        if (count is < 1 or > 64 || file.Length < 6 + count * 16) return false;
        int best = -1, distance = int.MaxValue;
        for (int i = 0; i < count; i++) {
            var entry = data.Slice(6 + i * 16, 16);
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]);
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(entry[12..]);
            if (entry[3] != 0 || length == 0 || offset < 6 + count * 16 || (ulong)offset + length > (ulong)file.Length) return false;
            int width = entry[0] == 0 ? 256 : entry[0], height = entry[1] == 0 ? 256 : entry[1];
            int delta = Math.Abs(width - desiredSize) + Math.Abs(height - desiredSize);
            if (delta < distance) { best = i; distance = delta; }
        }
        var selected = data.Slice(6 + best * 16, 16);
        int size = (int)BinaryPrimitives.ReadUInt32LittleEndian(selected[8..]);
        int start = (int)BinaryPrimitives.ReadUInt32LittleEndian(selected[12..]);
        bits = data.Slice(start, size).ToArray();
        return true;
    }
}
