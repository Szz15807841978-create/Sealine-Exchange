using UnityEngine;
using UnityEngine.UI;

namespace Fishing
{
    /// <summary>
    /// 让纸片底板刚好裹住文字。
    ///
    /// 为什么需要运行时来量：工程里的字体是运行时才赋的（`RaftWeatherController.Awake` 里
    /// `text.font = FontUtil.Get()`，注释说明是为了避免把系统字体序列化进场景）。
    /// 所以安装器在编辑器里拿 `preferredWidth` 不可靠 —— 那时字体还没定，
    /// 量出来的是默认字体的宽度，底板要么裹不住、要么被拉成一条长空壳。
    /// 挂上这个组件，等运行时字体就位后再量一次即可。
    /// </summary>
    [AddComponentMenu("鲸落/UI 底板适配")]
    [ExecuteAlways]
    public sealed class UiPlateFitter : MonoBehaviour
    {
        [Tooltip("要裹住的文字。留空则取自身或子物体上的 Text。")]
        public Text Target;

        [Tooltip("左右各留多少像素。")]
        public float PadX = 20f;

        [Tooltip("上下各留多少像素（0 = 不改高度）。")]
        public float PadY = 0f;

        private float _doneFor = -1f;

        private void OnEnable() { Fit(); }

        private void LateUpdate()
        {
            // 字体是运行时赋的，宽度会在赋完之后才正确，所以再量一次（只量一次即可）
            if (Time.timeSinceLevelLoad != _doneFor) Fit();
        }

        public void Fit()
        {
            if (!Target) Target = GetComponent<Text>();
            if (!Target) Target = GetComponentInChildren<Text>(true);
            if (!Target) return;

            var rt = (RectTransform)transform;
            float w = Target.preferredWidth + PadX * 2f;
            if (float.IsNaN(w) || w <= 1f) return;   // 字体没就位时量不出来，下次再来

            var size = rt.sizeDelta;
            rt.sizeDelta = new Vector2(w, PadY > 0f ? Target.preferredHeight + PadY * 2f : size.y);
            _doneFor = Time.timeSinceLevelLoad;
        }
    }
}
