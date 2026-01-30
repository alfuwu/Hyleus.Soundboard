namespace Hyleus.Soundboard.Framework.Structs;
public class Ref<T>(T val) {
    public T Value { get; set; } = val;

    public static implicit operator T(Ref<T> r) => r.Value;
    public static implicit operator Ref<T>(T v) => new(v);
}
