// =============================================================================
//  无头测试用的 UnityEngine 最小替身（shim）。
//
//  只实现 Runtime 层里被用到的那几个 API，让 Sim / Loot / PathMaker / GaugeKit
//  能在普通 .NET 上跑起来做规则断言 —— 不参与 Unity 工程编译（放在 tools/ 下，
//  Unity 不会导入 Assets/ 之外的东西）。
// =============================================================================
using System;
using System.Globalization;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero { get { return new Vector2(0f, 0f); } }
        public static Vector2 one { get { return new Vector2(1f, 1f); } }
        public static Vector2 right { get { return new Vector2(1f, 0f); } }
        public static Vector2 left { get { return new Vector2(-1f, 0f); } }
        public static Vector2 up { get { return new Vector2(0f, 1f); } }
        public static Vector2 down { get { return new Vector2(0f, -1f); } }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        public static Vector2 operator -(Vector2 a) { return new Vector2(-a.x, -a.y); }
        public static Vector2 operator *(Vector2 a, float s) { return new Vector2(a.x * s, a.y * s); }
        public static Vector2 operator *(float s, Vector2 a) { return new Vector2(a.x * s, a.y * s); }
        public static Vector2 operator *(Vector2 a, Vector2 b) { return new Vector2(a.x * b.x, a.y * b.y); }
        public static Vector2 operator /(Vector2 a, float s) { return new Vector2(a.x / s, a.y / s); }
        public float magnitude { get { return (float)Math.Sqrt(x * x + y * y); } }
        public float sqrMagnitude { get { return x * x + y * y; } }
        public Vector2 normalized { get { float m = magnitude; return m > 1e-6f ? new Vector2(x / m, y / m) : zero; } }
        public static float Distance(Vector2 a, Vector2 b) { return (a - b).magnitude; }
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { t = t < 0f ? 0f : (t > 1f ? 1f : t); return a + (b - a) * t; }
        public override string ToString() { return "(" + x + ", " + y + ")"; }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero { get { return new Vector3(0f, 0f, 0f); } }
        public static Vector3 one { get { return new Vector3(1f, 1f, 1f); } }
        public static Vector3 up { get { return new Vector3(0f, 1f, 0f); } }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator *(Vector3 a, float s) { return new Vector3(a.x * s, a.y * s, a.z * s); }
        public static Vector3 operator *(float s, Vector3 a) { return new Vector3(a.x * s, a.y * s, a.z * s); }
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return a + (b - a) * t;
        }
        public override string ToString() { return "(" + x + ", " + y + ", " + z + ")"; }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1f; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white { get { return new Color(1f, 1f, 1f, 1f); } }
        public static Color black { get { return new Color(0f, 0f, 0f, 1f); } }
        public static Color magenta { get { return new Color(1f, 0f, 1f, 1f); } }
        public static Color Lerp(Color a, Color b, float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        }
        public static Color operator *(Color c, float s) { return new Color(c.r * s, c.g * s, c.b * s, c.a * s); }
        public static Color operator *(float s, Color c) { return new Color(c.r * s, c.g * s, c.b * s, c.a * s); }
    }

    public static class Mathf
    {
        public const float PI = 3.14159265358979f;
        public const float Deg2Rad = PI / 180f;

        public static float Sqrt(float v) { return (float)Math.Sqrt(v); }
        public static float Log(float v) { return (float)Math.Log(v); }
        public static float Exp(float v) { return (float)Math.Exp(v); }
        public static float Sin(float v) { return (float)Math.Sin(v); }
        public static float Cos(float v) { return (float)Math.Cos(v); }
        public static float Atan2(float y, float x) { return (float)Math.Atan2(y, x); }
        public static float Pow(float f, float p) { return (float)Math.Pow(f, p); }
        public static float Sign(float v) { return v > 0f ? 1f : (v < 0f ? -1f : 0f); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * (t < 0f ? 0f : (t > 1f ? 1f : t)); }
        public static float MoveTowards(float cur, float target, float maxDelta)
        {
            float d = target - cur;
            if (Math.Abs(d) <= maxDelta) return target;
            return cur + Math.Sign(d) * maxDelta;
        }
        public static float Abs(float v) { return Math.Abs(v); }
        public static int Abs(int v) { return Math.Abs(v); }
        public static float Min(float a, float b) { return Math.Min(a, b); }
        public static int Min(int a, int b) { return Math.Min(a, b); }
        public static float Max(float a, float b) { return Math.Max(a, b); }
        public static int Max(int a, int b) { return Math.Max(a, b); }

        /// <summary>Unity 的 Mathf.Round 是"四舍六入五成双"，这里保持一致。</summary>
        public static float Round(float v) { return (float)Math.Round(v, MidpointRounding.ToEven); }

        public static int RoundToInt(float v) { return (int)Math.Round(v, MidpointRounding.ToEven); }
        public static float Ceil(float v) { return (float)Math.Ceiling(v); }
        public static int CeilToInt(float v) { return (int)Math.Ceiling(v); }
        public static float Floor(float v) { return (float)Math.Floor(v); }
        public static int FloorToInt(float v) { return (int)Math.Floor(v); }
        public static float Clamp(float v, float a, float b) { return v < a ? a : (v > b ? b : v); }
        public static int Clamp(int v, int a, int b) { return v < a ? a : (v > b ? b : v); }
        public static float Clamp01(float v) { return Clamp(v, 0f, 1f); }
        public static bool Approximately(float a, float b) { return Math.Abs(a - b) < 1e-6f; }
    }

    public static class ColorUtility
    {
        public static bool TryParseHtmlString(string html, out Color color)
        {
            color = Color.magenta;
            if (string.IsNullOrEmpty(html)) return false;

            string s = html.TrimStart('#');
            if (s.Length != 6 && s.Length != 8) return false;

            int r, g, b;
            if (!int.TryParse(s.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r)) return false;
            if (!int.TryParse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g)) return false;
            if (!int.TryParse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b)) return false;

            float a = 1f;
            if (s.Length == 8)
            {
                int ai;
                if (!int.TryParse(s.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ai)) return false;
                a = ai / 255f;
            }

            color = new Color(r / 255f, g / 255f, b / 255f, a);
            return true;
        }
    }

    /// <summary>测试用的确定性随机源：先灌种子，再替换掉 Unity 的 Random.value。</summary>
    public static class Random
    {
        private static System.Random _rng = new System.Random(12345);

        public static void Seed(int seed) { _rng = new System.Random(seed); }

        public static float value { get { return (float)_rng.NextDouble(); } }

        public static float Range(float min, float max)
        {
            if (max <= min) return min;
            return min + (float)_rng.NextDouble() * (max - min);
        }

        public static int Range(int min, int max)
        {
            if (max <= min) return min;
            return _rng.Next(min, max);
        }
    }

    public class Object
    {
        public string name = "";
    }
}
