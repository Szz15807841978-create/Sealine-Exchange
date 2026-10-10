using UnityEngine;

namespace Fishing
{
    /// <summary>
    /// 随机数：对应 index.html 里的 Math.random / randInt / rand / randn。
    /// randn 用 Box-Muller，并保留一个 spare 值 —— 和原 JS 实现逐位一致。
    /// </summary>
    public static class Rng
    {
        private static bool _hasSpare;
        private static float _spare;

        /// <summary>对应 Math.random()，返回 [0,1)。</summary>
        public static float Value()
        {
            return Random.value;
        }

        /// <summary>对应 randInt(a,b)：闭区间整数。</summary>
        public static int Range(int a, int b)
        {
            if (b < a) return a;
            return Random.Range(a, b + 1);
        }

        /// <summary>对应 rand(a,b)：浮点区间。</summary>
        public static float Range(float a, float b)
        {
            return Random.Range(a, b);
        }

        public static int Index(int count)
        {
            if (count <= 0) return 0;
            return Random.Range(0, count);
        }

        /// <summary>对应 randn()：标准正态分布。</summary>
        public static float Randn()
        {
            if (_hasSpare)
            {
                _hasSpare = false;
                return _spare;
            }

            float u, v, s;
            do
            {
                u = Value() * 2f - 1f;
                v = Value() * 2f - 1f;
                s = u * u + v * v;
            }
            while (s == 0f || s >= 1f);

            float m = Mathf.Sqrt(-2f * Mathf.Log(s) / s);
            _spare = v * m;
            _hasSpare = true;
            return u * m;
        }
    }
}
