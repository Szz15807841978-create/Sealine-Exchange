using UnityEngine;

namespace Fishing
{
    /// <summary>
    /// 13 种股市套路形态。keys = [时间进度 0~1, 基准价]，中间平滑插值；
    /// 实际幅度再乘 Cfg.PatternScale。6 种是亏损形态，2 种是急涨急跌的波浪形态。
    /// </summary>
    public static class Patterns
    {
        public struct Pattern
        {
            public readonly string Name;
            public readonly float[][] Keys;

            public Pattern(string name, float[][] keys)
            {
                Name = name;
                Keys = keys;
            }
        }

        private static float[] K(params float[] v) { return v; }

        private static float[][] Ks(params float[][] rows) { return rows; }

        public static readonly Pattern[] All =
        {
            /* —— 拉升 / 洗盘 —— */
            new Pattern("急涨缓跌", Ks(
                K(0f,100f), K(0.08f,122f), K(0.20f,188f), K(0.28f,207f),
                K(0.55f,168f), K(0.80f,136f), K(1f,118f))),

            new Pattern("暴跌回调后急涨", Ks(
                K(0f,100f), K(0.06f,86f), K(0.17f,57f), K(0.23f,66f),
                K(0.50f,172f), K(0.63f,238f), K(0.82f,214f), K(1f,196f))),

            new Pattern("横盘后拉升", Ks(
                K(0f,100f), K(0.30f,103f), K(0.55f,97f), K(0.63f,101f),
                K(0.72f,176f), K(0.83f,257f), K(0.92f,236f), K(1f,228f))),

            new Pattern("慢涨急跌", Ks(
                K(0f,100f), K(0.20f,116f), K(0.45f,141f), K(0.70f,179f),
                K(0.79f,186f), K(0.87f,97f), K(1f,86f))),

            new Pattern("假突破", Ks(
                K(0f,100f), K(0.18f,152f), K(0.34f,224f), K(0.45f,177f),
                K(0.60f,105f), K(0.74f,91f), K(1f,83f))),

            /* —— 波浪：急涨急跌 —— */
            new Pattern("急涨急跌", Ks(
                K(0f,100f), K(0.10f,148f), K(0.20f,104f), K(0.32f,165f), K(0.44f,100f),
                K(0.56f,182f), K(0.68f,112f), K(0.82f,170f), K(1f,132f))),

            new Pattern("锯齿震荡", Ks(
                K(0f,100f), K(0.12f,134f), K(0.24f,94f), K(0.36f,138f), K(0.48f,92f),
                K(0.60f,142f), K(0.72f,96f), K(0.86f,140f), K(1f,118f))),

            /* —— 下跌：只有开盘一小段能跑 —— */
            new Pattern("高位跳水", Ks(
                K(0f,100f), K(0.12f,152f), K(0.26f,196f), K(0.40f,166f), K(0.55f,193f),
                K(0.66f,148f), K(0.78f,74f), K(0.90f,45f), K(1f,38f))),

            new Pattern("阴跌不止", Ks(
                K(0f,100f), K(0.08f,105f), K(0.20f,98f), K(0.38f,90f),
                K(0.56f,82f), K(0.74f,74f), K(1f,66f))),

            new Pattern("开盘即顶", Ks(
                K(0f,100f), K(0.05f,109f), K(0.15f,98f), K(0.32f,90f),
                K(0.52f,82f), K(0.72f,73f), K(1f,65f))),

            new Pattern("反弹诱多", Ks(
                K(0f,100f), K(0.10f,104f), K(0.22f,85f), K(0.34f,95f),
                K(0.48f,86f), K(0.66f,72f), K(0.84f,60f), K(1f,53f))),

            new Pattern("崩盘", Ks(
                K(0f,100f), K(0.06f,104f), K(0.18f,90f), K(0.34f,82f),
                K(0.52f,72f), K(0.70f,60f), K(0.86f,50f), K(1f,44f))),

            new Pattern("高开低走", Ks(
                K(0f,100f), K(0.07f,128f), K(0.20f,110f), K(0.38f,98f),
                K(0.58f,88f), K(0.78f,79f), K(1f,73f))),
        };
    }
}
