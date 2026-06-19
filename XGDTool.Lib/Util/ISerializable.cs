namespace XGDTool.Lib.Util;

public interface ISerializable
{
    public int Size();
    public void Serialize(Span<byte> buffer);
    public void Deserialize(ReadOnlySpan<byte> data);
    public static T Deserialize<T>(ReadOnlySpan<byte> data) where T : ISerializable, new()
    {
        var instance = new T();
        instance.Deserialize(data);
        return instance;
    }
}

public static class ISerializableExtensions
{
    public static byte[] Serialize<T>(this T instance) where T : ISerializable
    {
        byte[] buffer = new byte[instance.Size()];
        instance.Serialize(buffer);
        return buffer;
    }
}
