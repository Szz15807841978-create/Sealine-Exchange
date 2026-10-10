using UnityEngine;
using UnityEngine.UI;

namespace WhaleFall
{
    /// <summary>HUD：体力/生命/深度/背包/工具/首领血条/飘字/结算遮罩。只读 DiveState。</summary>
    public class DiveHudView
    {
        private readonly Image _staminaFill;
        private readonly Text _staminaText;
        private readonly GameObject _hpRoot;
        private readonly Image _hpFill;
        private readonly Text _hpText;
        private readonly Text _depthText;
        private readonly Text _bagText;
        private readonly Text _modeText;
        private readonly Text _hintText;
        private readonly GameObject _bossRoot;
        private readonly Image _bossFill;
        private readonly Text _bossText;
        private readonly Text _toastText;
        private readonly GameObject _resultRoot;
        private readonly Text _resultText;

        private float _toastT;
        private float _bossShow;

        public DiveHudView(RectTransform canvas)
        {
            // 体力条
            _staminaFill = Bar(canvas, "体力条", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -14f), 320f, 18f,
                               Fishing.Palette.Hex("#5D929A"), out _staminaText);

            // 生命条
            var hpBg = Fishing.UiKit.Block("生命条底", canvas, new Color(0f, 0f, 0f, 0.4f));
            Fishing.UiKit.Fixed((RectTransform)hpBg.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -40f), 320f, 18f);
            var hpFillGo = new GameObject("生命条填充", typeof(RectTransform), typeof(Image));
            hpFillGo.transform.SetParent(hpBg.transform, false);
            _hpFill = hpFillGo.GetComponent<Image>();
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;
            _hpFill.color = Fishing.Palette.Hex("#D9674E");
            Stretch((RectTransform)hpFillGo.transform, 2f);
            _hpText = Fishing.Txt.Make(hpBg.transform, "生命文字", 12, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            Stretch((RectTransform)_hpText.transform, 6f);
            _hpRoot = hpBg.gameObject;
            _hpRoot.SetActive(false);

            // 深度 / 层
            _depthText = Fishing.Txt.Make(canvas, "深度", 18, Fishing.Palette.Hex("#F2E7CB"), TextAnchor.UpperRight, FontStyle.Bold);
            Fishing.UiKit.Fixed((RectTransform)_depthText.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -14f), 300f, 26f);
            _depthText.alignment = TextAnchor.UpperRight;

            // 背包
            _bagText = Fishing.Txt.Make(canvas, "背包", 15, Fishing.Palette.Hex("#B7D3D1"), TextAnchor.LowerLeft, FontStyle.Bold);
            Fishing.UiKit.Fixed((RectTransform)_bagText.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 16f), 300f, 24f);

            // 模式 / 工具
            _modeText = Fishing.Txt.Make(canvas, "模式", 16, Fishing.Palette.Hex("#F2E7CB"), TextAnchor.LowerCenter, FontStyle.Bold);
            Fishing.UiKit.Fixed((RectTransform)_modeText.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), 400f, 26f);

            // 操作提示
            _hintText = Fishing.Txt.Make(canvas, "提示", 13, Fishing.Palette.Hex("#8FB4D8"), TextAnchor.LowerCenter, FontStyle.Normal);
            Fishing.UiKit.Fixed((RectTransform)_hintText.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -8f), 900f, 22f);

            // 首领血条
            var bossBg = Fishing.UiKit.Block("首领血条底", canvas, new Color(0f, 0f, 0f, 0.55f));
            Fishing.UiKit.Fixed((RectTransform)bossBg.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), 560f, 22f);
            var bossFillGo = new GameObject("首领血条", typeof(RectTransform), typeof(Image));
            bossFillGo.transform.SetParent(bossBg.transform, false);
            _bossFill = bossFillGo.GetComponent<Image>();
            _bossFill.type = Image.Type.Filled;
            _bossFill.fillMethod = Image.FillMethod.Horizontal;
            _bossFill.color = Fishing.Palette.Hex("#D9674E");
            Stretch((RectTransform)bossFillGo.transform, 2f);
            _bossText = Fishing.Txt.Make(bossBg.transform, "首领名", 13, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch((RectTransform)_bossText.transform, 4f);
            _bossRoot = bossBg.gameObject;
            _bossRoot.SetActive(false);

            // 飘字
            var toastBg = Fishing.UiKit.Block("飘字底", canvas, new Color(0f, 0f, 0f, 0f));
            Fishing.UiKit.Fixed((RectTransform)toastBg.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), 700f, 40f);
            _toastText = Fishing.Txt.Make(toastBg.transform, "飘字", 16, Fishing.Palette.Hex("#F2E7CB"), TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch((RectTransform)_toastText.transform, 4f);

            // 结算遮罩
            var result = Fishing.UiKit.Block("结算遮罩", canvas, new Color(0.03f, 0.1f, 0.16f, 0.88f));
            Fishing.UiKit.Stretch((RectTransform)result.transform);
            _resultText = Fishing.Txt.Make(result.transform, "结算文字", 26, Fishing.Palette.Hex("#F2E7CB"), TextAnchor.MiddleCenter, FontStyle.Bold);
            Fishing.UiKit.Stretch((RectTransform)_resultText.transform, 40f);
            _resultRoot = result.gameObject;
            _resultRoot.SetActive(false);
        }

        private static Image Bar(RectTransform canvas, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, float w, float h, Color fill, out Text label)
        {
            var bg = Fishing.UiKit.Block(name + "底", canvas, new Color(0f, 0f, 0f, 0.4f));
            Fishing.UiKit.Fixed((RectTransform)bg.transform, anchor, pivot, pos, w, h);
            var fillGo = new GameObject(name + "填充", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(bg.transform, false);
            var img = fillGo.GetComponent<Image>();
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.color = fill;
            Stretch((RectTransform)fillGo.transform, 2f);
            label = Fishing.Txt.Make(bg.transform, name + "文字", 12, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            Stretch((RectTransform)label.transform, 6f);
            return img;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public void ShowToast(string msg, float duration = 2.0f)
        {
            _toastText.text = msg;
            _toastText.transform.parent.gameObject.SetActive(true);
            _toastT = duration;
        }

        public void TickToast(float dt)
        {
            if (_toastT > 0f)
            {
                _toastT -= dt;
                if (_toastT <= 0f) _toastText.transform.parent.gameObject.SetActive(false);
            }
        }

        public void Refresh(DiveState s, EnemyInst? engagedBoss)
        {
            var p = s.Player;
            _staminaFill.fillAmount = p.stamina / p.staminaMax;
            _staminaText.text = "体力 " + (int)p.stamina + " / " + (int)p.staminaMax;
            _staminaFill.color = p.stamina <= DiveConfig.LowThreshold
                ? Fishing.Palette.Hex("#D9674E") : Fishing.Palette.Hex("#5D929A");

            bool showHp = p.hpActive || p.hp < p.hpMax;
            _hpRoot.SetActive(showHp && !s.Over);
            _hpFill.fillAmount = p.hp / p.hpMax;
            _hpText.text = "生命 " + (int)p.hp + " / " + (int)p.hpMax;

            float depth = DiveSim.DepthM(s);
            _depthText.text = "深度 " + depth.ToString("0.0") + " m  ·  " + LayerName(DiveSim.LayerAtDepth(depth));
            _bagText.text = "背包 " + s.Bag.UsedSlots() + " / " + DiveConfig.BagSlots;

            _modeText.text = s.Mode == ControlMode.Weapon
                ? "武器 · 鱼叉"
                : "工具 · " + (s.ActiveTool == ToolKind.Pick ? "小镐子" : "采集手");
            _modeText.color = s.Mode == ControlMode.Weapon ? Fishing.Palette.Hex("#DDA64F") : Fishing.Palette.Hex("#5D929A");

            _hintText.text = s.Over ? "" : "WASD 移动 · 鼠标 瞄准 · 左键 使用 · C 闪避 · Q 切换 · E 拾取 · I 背包 · Esc 暂停";

            // 首领血条
            bool bossShow = engagedBoss.HasValue;
            if (bossShow)
            {
                var b = engagedBoss.Value;
                _bossRoot.SetActive(true);
                _bossFill.fillAmount = b.hp / b.hpMax;
                _bossText.text = "奇虾 · 守矿者  " + (int)b.hp + " / " + (int)b.hpMax;
            }
            else
            {
                _bossShow -= Time.unscaledDeltaTime;
                _bossRoot.SetActive(false);
            }

            if (s.Over)
            {
                _resultRoot.SetActive(true);
                _resultText.text = ResultTitle(s) + "\n\n按 R 重新下潜";
            }
            else
            {
                _resultRoot.SetActive(false);
            }
        }

        private static string LayerName(LayerKind l)
        {
            switch (l)
            {
                case LayerKind.Shallow: return "浅层";
                case LayerKind.Mid: return "中层";
                case LayerKind.Deep: return "深层";
                default: return "深渊";
            }
        }

        private static string ResultTitle(DiveState s)
        {
            switch (s.Result)
            {
                case DiveResult.Extract: return "撤离成功";
                case DiveResult.Exhausted: return "力竭";
                case DiveResult.Killed: return s.DeathReason ?? "被击杀";
                case DiveResult.Aborted: return "已放弃下潜";
                default: return "结束";
            }
        }
    }
}
