using UnityEngine;

namespace Fishing
{
    /// <summary>
    /// 木筏相对静止水线的位姿增量。全部是「增量」而不是绝对位姿：
    /// 场景里手工调好的木筏静态位置/朝向是零点，浮力只往上叠。
    /// </summary>
    public struct RaftWavePose
    {
        /// <summary>升沉（米）。正 = 比静止水线高。</summary>
        public float Heave;

        /// <summary>俯仰角（度，绕木筏本地 Z 轴）。正 = 本地 +X 端抬高。</summary>
        public float PitchDeg;

        /// <summary>横滚角（度，绕木筏本地 X 轴）。正 = 本地 +Z 端抬高。</summary>
        public float RollDeg;

        /// <summary>采样点平均水面高度（绝对世界 Y，诊断用）。</summary>
        public float MeanHeight;

        /// <summary>拟合残差均方根（米）。越小说明 5 个点越接近一个平面。</summary>
        public float ResidualRms;
    }

    /// <summary>
    /// 浮力解算：把 N 个采样点的水面高度拟合成一个平面，取出
    /// 「整体升降 + 前后俯仰 + 左右倾斜」三个量。
    ///
    /// 为什么用最小二乘平面拟合而不是相邻两点相减：
    /// 5 个点两两相减会得到互相矛盾的角度，噪声被放大；拟合平面用到全部点，
    /// 单个采样点抖动只贡献 1/5 权重，出来的姿态明显更稳。
    ///
    /// 本文件是纯逻辑：只用到 UnityEngine.Mathf / Vector2，不碰场景、不碰 Crest，
    /// 因此可以被 tools/run-raftcheck.ps1 在没有 Unity 的环境里跑断言。
    /// </summary>
    public static class RaftBuoyancySolver
    {
        public const float RadToDeg = 57.29577951308232f;
        public const float Ln2 = 0.6931471805599453f;

        /// <summary>
        /// 拟合平面 y = a + b·x + c·z（x 为前后向，z 为左右向），然后：
        ///   Heave    = a                     （采样坐标系原点的水面高度）
        ///   PitchDeg = atan(b)               （b = dY/dX，正表示 +X 端高）
        ///   RollDeg  = atan(c)               （c = dY/dZ，正表示 +Z 端高）
        /// 采样点退化（共线、重合、数量不足）时返回 false，调用方应保持上一帧姿态。
        /// </summary>
        public static bool Solve(float[] heights, Vector2[] offsetsXZ, int count, out RaftWavePose pose)
        {
            pose = default(RaftWavePose);
            if (heights == null || offsetsXZ == null || count < 3) return false;
            if (heights.Length < count || offsetsXZ.Length < count) return false;

            double n = count;
            double sx = 0, sz = 0, sy = 0, sxx = 0, szz = 0, sxz = 0, sxy = 0, szy = 0;
            for (int i = 0; i < count; i++)
            {
                double x = offsetsXZ[i].x;
                double z = offsetsXZ[i].y;
                double y = heights[i];
                if (double.IsNaN(x) || double.IsNaN(z) || double.IsNaN(y)) return false;
                sx += x; sz += z; sy += y;
                sxx += x * x; szz += z * z; sxz += x * z;
                sxy += x * y; szy += z * y;
            }

            // 法方程 [n sx sz; sx sxx sxz; sz sxz szz] · [a b c]ᵀ = [sy sxy szy]ᵀ
            double m00 = n,   m01 = sx,  m02 = sz;
            double m10 = sx,  m11 = sxx, m12 = sxz;
            double m20 = sz,  m21 = sxz, m22 = szz;

            double det = m00 * (m11 * m22 - m12 * m21)
                       - m01 * (m10 * m22 - m12 * m20)
                       + m02 * (m10 * m21 - m11 * m20);
            if (System.Math.Abs(det) < 1e-9) return false;

            double a = (sy * (m11 * m22 - m12 * m21)
                      - m01 * (sxy * m22 - m12 * szy)
                      + m02 * (sxy * m21 - m11 * szy)) / det;

            double b = (m00 * (sxy * m22 - m12 * szy)
                      - sy * (m10 * m22 - m12 * m20)
                      + m02 * (m10 * szy - sxy * m20)) / det;

            double c = (m00 * (m11 * szy - sxy * m21)
                      - m01 * (m10 * szy - sxy * m20)
                      + sy * (m10 * m21 - m11 * m20)) / det;

            pose.Heave = (float)a;
            pose.MeanHeight = (float)(sy / n);
            pose.PitchDeg = (float)(System.Math.Atan(b) * RadToDeg);
            pose.RollDeg = (float)(System.Math.Atan(c) * RadToDeg);

            // 残差：衡量 5 个点离拟合平面的距离，用来判断采样是否可信
            double sum = 0;
            for (int i = 0; i < count; i++)
            {
                double e = offsetsXZ[i].y * c + offsetsXZ[i].x * b + a - heights[i];
                sum += e * e;
            }
            pose.ResidualRms = (float)System.Math.Sqrt(sum / n);
            return true;
        }

        /// <summary>
        /// 与帧率无关的指数趋近。halfLife（秒）表示「走完一半差值」所需时间，
        /// 不受 Time.deltaTime 波动影响；halfLife ≤ 0 表示直接吸附到目标。
        /// </summary>
        public static float SmoothTowards(float current, float target, float halfLife, float dt)
        {
            if (halfLife <= 1e-4f) return target;
            if (dt <= 0f) return current;
            float k = 1f - Mathf.Exp(-Ln2 * dt / halfLife);
            return current + (target - current) * Mathf.Clamp01(k);
        }

        /// <summary>对称限幅。limit 为负时按 0 处理。</summary>
        public static float ClampSymmetric(float value, float limit)
        {
            if (limit <= 0f) return 0f;
            return Mathf.Clamp(value, -limit, limit);
        }

        /// <summary>
        /// 单点浸没比例：水深高于该点时，返回该点没入水中的深度占 hullHeight 的比例（0—1）。
        /// 第二阶段完整刚体浮力用它算排水量；受控浮力阶段用不到。
        /// </summary>
        public static float SubmergedFraction(float waterHeight, float pointHeight, float hullHeight)
        {
            if (hullHeight <= 1e-4f) return 0f;
            float depth = waterHeight - pointHeight;
            if (depth <= 0f) return 0f;
            return Mathf.Clamp01(depth / hullHeight);
        }
    }
}
