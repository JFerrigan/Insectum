// Minimal stand-ins for the UnityEngine types the level generator uses, so it can run under plain .NET.
// Semantics follow Unity's (float math, Mathf rounding, Rect min/max, Vector2 equality tolerance).
using System;
using System.IO;

namespace UnityEngine
{
    public struct Vector2 : IEquatable<Vector2>
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }

        public static Vector2 zero => new(0f, 0f);
        public static Vector2 one => new(1f, 1f);
        public static Vector2 up => new(0f, 1f);
        public static Vector2 down => new(0f, -1f);
        public static Vector2 left => new(-1f, 0f);
        public static Vector2 right => new(1f, 0f);
        public static Vector2 positiveInfinity => new(float.PositiveInfinity, float.PositiveInfinity);
        public static Vector2 negativeInfinity => new(float.NegativeInfinity, float.NegativeInfinity);

        public float magnitude => MathF.Sqrt(x * x + y * y);
        public float sqrMagnitude => x * x + y * y;
        public Vector2 normalized
        {
            get
            {
                float m = magnitude;
                return m > 1e-5f ? new Vector2(x / m, y / m) : zero;
            }
        }

        public float this[int i]
        {
            get => i == 0 ? x : i == 1 ? y : throw new IndexOutOfRangeException();
            set { if (i == 0) x = value; else if (i == 1) y = value; else throw new IndexOutOfRangeException(); }
        }

        public void Normalize() => this = normalized;
        public void Set(float nx, float ny) { x = nx; y = ny; }

        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;
        public static float SqrMagnitude(Vector2 a) => a.sqrMagnitude;
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => LerpUnclamped(a, b, Mathf.Clamp01(t));
        public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t) => new(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
        public static Vector2 Min(Vector2 a, Vector2 b) => new(MathF.Min(a.x, b.x), MathF.Min(a.y, b.y));
        public static Vector2 Max(Vector2 a, Vector2 b) => new(MathF.Max(a.x, b.x), MathF.Max(a.y, b.y));
        public static Vector2 Scale(Vector2 a, Vector2 b) => new(a.x * b.x, a.y * b.y);
        public static Vector2 Perpendicular(Vector2 v) => new(-v.y, v.x);
        public static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDelta)
        {
            Vector2 d = target - current;
            float m = d.magnitude;
            return m <= maxDelta || m == 0f ? target : current + d / m * maxDelta;
        }
        public static float Angle(Vector2 from, Vector2 to)
        {
            float denominator = MathF.Sqrt(from.sqrMagnitude * to.sqrMagnitude);
            if (denominator < 1e-15f) return 0f;
            return MathF.Acos(Mathf.Clamp(Dot(from, to) / denominator, -1f, 1f)) * Mathf.Rad2Deg;
        }
        public static float SignedAngle(Vector2 from, Vector2 to) =>
            Angle(from, to) * MathF.Sign(from.x * to.y - from.y * to.x);
        public static Vector2 ClampMagnitude(Vector2 v, float max) =>
            v.sqrMagnitude > max * max ? v.normalized * max : v;

        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
        public static Vector2 operator -(Vector2 a) => new(-a.x, -a.y);
        public static Vector2 operator *(Vector2 a, float d) => new(a.x * d, a.y * d);
        public static Vector2 operator *(float d, Vector2 a) => new(a.x * d, a.y * d);
        public static Vector2 operator *(Vector2 a, Vector2 b) => new(a.x * b.x, a.y * b.y);
        public static Vector2 operator /(Vector2 a, float d) => new(a.x / d, a.y / d);
        public static Vector2 operator /(Vector2 a, Vector2 b) => new(a.x / b.x, a.y / b.y);
        public static bool operator ==(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 9.99999944E-11f;
        public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);
        public static implicit operator Vector2(Vector2Int v) => new(v.x, v.y);

        public bool Equals(Vector2 other) => x.Equals(other.x) && y.Equals(other.y);
        public override bool Equals(object obj) => obj is Vector2 other && Equals(other);
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);
        public override string ToString() => $"({x:F2}, {y:F2})";
    }

    public struct Vector2Int : IEquatable<Vector2Int>
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }

        public static Vector2Int zero => new(0, 0);
        public static Vector2Int one => new(1, 1);
        public static Vector2Int up => new(0, 1);
        public static Vector2Int down => new(0, -1);
        public static Vector2Int left => new(-1, 0);
        public static Vector2Int right => new(1, 0);

        public float magnitude => MathF.Sqrt(x * x + y * y);
        public int sqrMagnitude => x * x + y * y;

        public static Vector2Int RoundToInt(Vector2 v) => new(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y));
        public static Vector2Int FloorToInt(Vector2 v) => new(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y));
        public static Vector2Int CeilToInt(Vector2 v) => new(Mathf.CeilToInt(v.x), Mathf.CeilToInt(v.y));
        public static Vector2Int Min(Vector2Int a, Vector2Int b) => new(Math.Min(a.x, b.x), Math.Min(a.y, b.y));
        public static Vector2Int Max(Vector2Int a, Vector2Int b) => new(Math.Max(a.x, b.x), Math.Max(a.y, b.y));
        public static float Distance(Vector2Int a, Vector2Int b) => (a - b).magnitude;

        public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new(a.x + b.x, a.y + b.y);
        public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new(a.x - b.x, a.y - b.y);
        public static Vector2Int operator -(Vector2Int a) => new(-a.x, -a.y);
        public static Vector2Int operator *(Vector2Int a, int d) => new(a.x * d, a.y * d);
        public static Vector2Int operator *(int d, Vector2Int a) => new(a.x * d, a.y * d);
        public static Vector2Int operator /(Vector2Int a, int d) => new(a.x / d, a.y / d);
        public static bool operator ==(Vector2Int a, Vector2Int b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(Vector2Int a, Vector2Int b) => !(a == b);

        public bool Equals(Vector2Int other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is Vector2Int other && Equals(other);
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);
        public override string ToString() => $"({x}, {y})";
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public Rect(Vector2 position, Vector2 size) : this(position.x, position.y, size.x, size.y) { }

        public static Rect MinMaxRect(float xmin, float ymin, float xmax, float ymax) => new(xmin, ymin, xmax - xmin, ymax - ymin);

        public float xMin { get => x; set { float max = xMax; x = value; width = max - x; } }
        public float yMin { get => y; set { float max = yMax; y = value; height = max - y; } }
        public float xMax { get => x + width; set => width = value - x; }
        public float yMax { get => y + height; set => height = value - y; }
        public Vector2 min { get => new(xMin, yMin); set { xMin = value.x; yMin = value.y; } }
        public Vector2 max { get => new(xMax, yMax); set { xMax = value.x; yMax = value.y; } }
        public Vector2 position { get => new(x, y); set { x = value.x; y = value.y; } }
        public Vector2 size { get => new(width, height); set { width = value.x; height = value.y; } }
        public Vector2 center
        {
            get => new(x + width / 2f, y + height / 2f);
            set { x = value.x - width / 2f; y = value.y - height / 2f; }
        }

        public bool Contains(Vector2 p) => p.x >= xMin && p.x < xMax && p.y >= yMin && p.y < yMax;
        public bool Overlaps(Rect o) => o.xMax > xMin && o.xMin < xMax && o.yMax > yMin && o.yMin < yMax;
        public override string ToString() => $"(x:{x:F2}, y:{y:F2}, width:{width:F2}, height:{height:F2})";
    }

    public static class Mathf
    {
        public const float PI = MathF.PI;
        public const float Deg2Rad = MathF.PI / 180f;
        public const float Rad2Deg = 180f / MathF.PI;
        public const float Infinity = float.PositiveInfinity;
        public const float NegativeInfinity = float.NegativeInfinity;
        public static readonly float Epsilon = float.Epsilon;

        public static float Abs(float v) => MathF.Abs(v);
        public static int Abs(int v) => Math.Abs(v);
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Min(params float[] values) { float m = values[0]; foreach (float v in values) if (v < m) m = v; return m; }
        public static int Min(int a, int b) => a < b ? a : b;
        public static int Min(params int[] values) { int m = values[0]; foreach (int v in values) if (v < m) m = v; return m; }
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Max(params float[] values) { float m = values[0]; foreach (float v in values) if (v > m) m = v; return m; }
        public static int Max(int a, int b) => a > b ? a : b;
        public static int Max(params int[] values) { int m = values[0]; foreach (int v in values) if (v > m) m = v; return m; }
        public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
        public static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static float InverseLerp(float a, float b, float v) => a != b ? Clamp01((v - a) / (b - a)) : 0f;
        public static float MoveTowards(float current, float target, float maxDelta) =>
            MathF.Abs(target - current) <= maxDelta ? target : current + MathF.Sign(target - current) * maxDelta;
        public static float SmoothStep(float from, float to, float t)
        {
            t = Clamp01(t);
            t = -2f * t * t * t + 3f * t * t;
            return to * t + from * (1f - t);
        }
        public static float Sqrt(float v) => MathF.Sqrt(v);
        public static float Pow(float a, float b) => MathF.Pow(a, b);
        public static float Exp(float v) => MathF.Exp(v);
        public static float Log(float v) => MathF.Log(v);
        public static float Sin(float v) => MathF.Sin(v);
        public static float Cos(float v) => MathF.Cos(v);
        public static float Tan(float v) => MathF.Tan(v);
        public static float Asin(float v) => MathF.Asin(v);
        public static float Acos(float v) => MathF.Acos(v);
        public static float Atan(float v) => MathF.Atan(v);
        public static float Atan2(float y, float x) => MathF.Atan2(y, x);
        public static float Sign(float v) => v >= 0f ? 1f : -1f;
        public static float Floor(float v) => MathF.Floor(v);
        public static float Ceil(float v) => MathF.Ceiling(v);
        public static float Round(float v) => MathF.Round(v, MidpointRounding.ToEven);
        public static int FloorToInt(float v) => (int)MathF.Floor(v);
        public static int CeilToInt(float v) => (int)MathF.Ceiling(v);
        public static int RoundToInt(float v) => (int)MathF.Round(v, MidpointRounding.ToEven);
        public static float Repeat(float t, float length) => Clamp(t - MathF.Floor(t / length) * length, 0f, length);
        public static float PingPong(float t, float length) { t = Repeat(t, length * 2f); return length - MathF.Abs(t - length); }
        public static float DeltaAngle(float current, float target)
        {
            float delta = Repeat(target - current, 360f);
            return delta > 180f ? delta - 360f : delta;
        }
        public static bool Approximately(float a, float b) =>
            MathF.Abs(b - a) < MathF.Max(1E-06f * MathF.Max(MathF.Abs(a), MathF.Abs(b)), float.Epsilon * 8f);
        public static int NextPowerOfTwo(int v) { int p = 1; while (p < v) p <<= 1; return p; }
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }

    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class MinAttribute : Attribute { public MinAttribute(float min) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string text) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string fileName { get; set; }
        public string menuName { get; set; }
        public int order { get; set; }
    }

    public class Object
    {
        public string name { get; set; }
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }

    public sealed class TextAsset : Object
    {
        public TextAsset(string text) { this.text = text; }
        public string text { get; }
    }

    // Resources.Load reads text assets straight from the game's Assets/Resources folder.
    public static class Resources
    {
        public static string Root;

        public static T[] LoadAll<T>(string path) where T : Object
        {
            var found = new System.Collections.Generic.List<T>();
            string dir = Path.Combine(Root, path);
            if (typeof(T) != typeof(TextAsset) || !Directory.Exists(dir))
                return found.ToArray();
            foreach (string file in Directory.GetFiles(dir))
            {
                string extension = Path.GetExtension(file);
                if (extension is ".json" or ".txt" or ".bytes" or ".csv")
                    found.Add(new TextAsset(File.ReadAllText(file)) { name = Path.GetFileNameWithoutExtension(file) } as T);
            }
            return found.ToArray();
        }

        public static T Load<T>(string path) where T : Object
        {
            if (typeof(T) != typeof(TextAsset))
                return null;
            foreach (string extension in new[] { ".txt", ".json", ".bytes", ".csv", "" })
            {
                string file = Path.Combine(Root, path + extension);
                if (File.Exists(file))
                    return new TextAsset(File.ReadAllText(file)) { name = Path.GetFileName(path) } as T;
            }
            return null;
        }
    }

    // JSON with Unity's rules: public fields by name, no properties.
    public static class JsonUtility
    {
        private static readonly System.Text.Json.JsonSerializerOptions Options = new()
        {
            IncludeFields = true,
            IgnoreReadOnlyProperties = true,
            WriteIndented = false
        };

        private static readonly System.Text.Json.JsonSerializerOptions Pretty = new()
        {
            IncludeFields = true,
            IgnoreReadOnlyProperties = true,
            WriteIndented = true
        };

        public static string ToJson(object value, bool prettyPrint = false) =>
            System.Text.Json.JsonSerializer.Serialize(value, value.GetType(), prettyPrint ? Pretty : Options);

        public static T FromJson<T>(string json) => System.Text.Json.JsonSerializer.Deserialize<T>(json, Options);
    }

    public static class Debug
    {
        public static void Log(object message) => Console.Error.WriteLine(message);
        public static void LogWarning(object message) => Console.Error.WriteLine("warning: " + message);
        public static void LogError(object message) => Console.Error.WriteLine("error: " + message);
        public static void Assert(bool condition, string message = null)
        {
            if (!condition) Console.Error.WriteLine("assert: " + message);
        }
    }
}
