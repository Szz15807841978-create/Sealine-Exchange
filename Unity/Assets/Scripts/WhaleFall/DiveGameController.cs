using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WhaleFall
{
    /// <summary>
    /// 搜打撤主控制器：输入 + 驱动 DiveSim + 刷新灰盒与 HUD。
    /// 规则全部在 DiveSim（纯逻辑，已无头断言）；这里只做「驱动 + 显示」。
    /// </summary>
    public class DiveGameController : MonoBehaviour
    {
        public DiveState S;
        private DiveWorldView _worldView;
        private DiveHudView _hud;
        private Camera _cam;
        private Canvas _canvas;
        private RectTransform _canvasRoot;

        private bool _pauseOpen;
        private bool _bagOpen;
        private GameObject _pausePanel;
        private GameObject _bagPanel;
        private Transform _bagRows;
        private readonly List<GameObject> _bagRowPool = new List<GameObject>();

        // 视口高度（米）。HTML 版 900px 视口 ÷ 32px/米 ≈ 28m，保持一致。
        private const float ViewHeightM = 28f;

        public bool IsPaused { get { return _pauseOpen || _bagOpen || (S != null && S.Over); } }

        public void Build(Canvas canvas, Camera cam)
        {
            _canvas = canvas;
            _cam = cam;
            _canvasRoot = (RectTransform)canvas.transform;

            if (_cam != null)
            {
                _cam.orthographic = true;
                _cam.orthographicSize = ViewHeightM * 0.5f;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = Fishing.Palette.Hex("#0B2233");
            }

            BuildPausePanel();
            BuildBagPanel();

            _worldView = new DiveWorldView();
            _hud = new DiveHudView(_canvasRoot);
            ResetGame();
        }

        private void Update()
        {
            if (S == null || _worldView == null || _hud == null) return;
            float dt = Time.unscaledDeltaTime;

            HandleInput();

            if (!IsPaused) DiveSim.Tick(S, dt);

            _hud.TickToast(dt);
            _worldView.RebuildTerrain(S);
            _worldView.Refresh(S);
            _hud.Refresh(S, EngagedBoss());
            if (_bagOpen) RefreshBagPanel();
            FollowCamera();
        }

        private void FollowCamera()
        {
            if (_cam == null) return;
            _cam.transform.position = new Vector3(S.Player.pos.x, S.Player.pos.y, -60f);
        }

        private EnemyInst? EngagedBoss()
        {
            for (int i = 0; i < S.World.Enemies.Count; i++)
            {
                var e = S.World.Enemies[i];
                if (e.dead) continue;
                if (!DiveDefs.EnemyOf(e.key).IsBoss) continue;
                float detect = DiveConfig.M(DiveDefs.EnemyOf(e.key).DetectPx);
                if ((e.pos - S.Player.pos).magnitude < detect) return e;
            }
            return null;
        }

        // =====================================================================
        //  输入
        // =====================================================================

        private void HandleInput()
        {
            // 地形调试：F6 换一个随机种子，F7 用当前种子重开（应得到完全相同的地图）。
            if (Input.GetKeyDown(KeyCode.F6)) { ResetGame(); return; }
            if (Input.GetKeyDown(KeyCode.F7)) { ResetGame(S.Seed); return; }
            if (Input.GetKeyDown(KeyCode.R) && S.Over) { ResetGame(); return; }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_bagOpen) { _bagOpen = false; _bagPanel.SetActive(false); return; }
                if (S.Over) return;
                TogglePause();
                return;
            }

            if (S.Over) return;

            if (Input.GetKeyDown(KeyCode.I))
            {
                if (_pauseOpen) return;
                _bagOpen = !_bagOpen;
                _bagPanel.SetActive(_bagOpen);
                if (_bagOpen) RefreshBagPanel();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                if (_pauseOpen) { Abort(); return; }   // 暂停菜单里 Q = 放弃
                if (_bagOpen) return;
                DiveSim.ToggleMode(S);
            }
            if (Input.GetKeyDown(KeyCode.C))
            {
                if (!IsPaused) DiveSim.Dash(S);
            }
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (!IsPaused) DiveSim.Interact(S);
            }

            if (!IsPaused)
            {
                // 移动
                float mx = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
                float my = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
                S.MoveAxis = new Vector2(mx, my);

                // 瞄准
                if (_cam != null)
                {
                    Vector3 mp = Input.mousePosition;
                    mp.z = -_cam.transform.position.z;
                    Vector3 world = _cam.ScreenToWorldPoint(mp);
                    S.AimWorld = new Vector2(world.x, world.y);
                    S.AimValid = true;
                }

                S.UseHeld = Input.GetMouseButton(0);
                if (Input.GetMouseButtonDown(0)) { S.UsePressed = true; DiveSim.UseDown(S); }
                if (Input.GetMouseButtonUp(0)) { S.UseReleased = true; DiveSim.UseUp(S); }
            }
            else
            {
                S.MoveAxis = Vector2.zero;
                S.UseHeld = false;
            }
        }

        private void TogglePause()
        {
            _pauseOpen = !_pauseOpen;
            _pausePanel.SetActive(_pauseOpen);
        }

        private void Abort()
        {
            DiveSim.Abort(S);
            _pauseOpen = false;
            _pausePanel.SetActive(false);
        }

        public void ResetGame()
        {
            ResetGame(UnityEngine.Random.Range(0, 1000000));
        }

        public void ResetGame(int seed)
        {
            S = new DiveState();
            S.Reset(seed);
            _worldView.RebuildTerrain(S);
            S.Player.pos = new Vector2(S.World.RaftPos.x, -0.5f);
            _pauseOpen = false;
            _bagOpen = false;
            if (_pausePanel != null) _pausePanel.SetActive(false);
            if (_bagPanel != null) _bagPanel.SetActive(false);
            _hud.ShowToast("新下潜 · Seed " + seed);
        }

        // =====================================================================
        //  面板
        // =====================================================================

        private void BuildPausePanel()
        {
            var panel = Fishing.UiKit.Block("暂停面板", _canvasRoot, new Color(0.03f, 0.1f, 0.16f, 0.9f));
            Fishing.UiKit.Stretch((RectTransform)panel.transform);
            var title = Fishing.Txt.Make(panel.transform, "标题", 26, Fishing.Palette.Hex("#F2E7CB"), TextAnchor.MiddleCenter, FontStyle.Bold);
            Fishing.UiKit.Fixed((RectTransform)title.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), 500f, 40f);
            title.text = "已暂停";
            var hint = Fishing.Txt.Make(panel.transform, "提示", 15, Fishing.Palette.Hex("#8FB4D8"), TextAnchor.MiddleCenter, FontStyle.Normal);
            Fishing.UiKit.Fixed((RectTransform)hint.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), 600f, 30f);
            hint.text = "Esc 继续 · Q 放弃下潜";
            _pausePanel = panel.gameObject;
            _pausePanel.SetActive(false);
        }

        private void BuildBagPanel()
        {
            var panel = Fishing.UiKit.Block("背包面板", _canvasRoot, new Color(0.04f, 0.15f, 0.24f, 0.97f));
            Fishing.UiKit.Fixed((RectTransform)panel.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), 480f, 520f);
            var title = Fishing.Txt.Make(panel.transform, "标题", 22, Fishing.Palette.Hex("#F2E7CB"), TextAnchor.MiddleLeft, FontStyle.Bold);
            Fishing.UiKit.Fixed((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -18f), 320f, 36f);
            title.text = "随身背包 · 6 格";
            var close = Fishing.UiKit.Block("关闭", panel.transform, Fishing.Palette.Hex("#25445A"));
            Fishing.UiKit.Fixed((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), 90f, 34f);
            var closeBtn = close.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = close;
            var closeTxt = Fishing.Txt.Make(close.transform, "文字", 14, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Fishing.UiKit.Stretch((RectTransform)closeTxt.transform, 3f);
            closeTxt.text = "关闭";
            closeBtn.onClick.AddListener(() => { _bagOpen = false; _bagPanel.SetActive(false); });

            var rows = Fishing.UiKit.Rect("背包行", panel.transform);
            Fishing.UiKit.Fixed(rows, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -72f), 440f, 400f);
            Fishing.UiKit.Col(rows, 8f, new RectOffset(0, 0, 0, 0));
            _bagRows = rows;
            _bagPanel = panel.gameObject;
            _bagPanel.SetActive(false);
        }

        private void RefreshBagPanel()
        {
            for (int i = 0; i < _bagRows.childCount; i++) _bagRows.GetChild(i).gameObject.SetActive(false);
            int row = 0;
            for (int i = 0; i < S.Bag.Slots.Length; i++)
            {
                var it = S.Bag.Slots[i];
                if (string.IsNullOrEmpty(it.Key)) continue;
                var rowGo = GetRow(row++);
                var nameTxt = rowGo.transform.Find("名称").GetComponent<Text>();
                nameTxt.text = it.Name + " ×" + it.Qty;
                var act = rowGo.transform.Find("动作").GetComponent<Button>();
                var actTxt = act.transform.Find("文字").GetComponent<Text>();
                if (it.Kind == "food")
                {
                    act.gameObject.SetActive(true);
                    actTxt.text = it.Heal > 0f ? "吃 +" + (int)it.Heal : "吃";
                    string key = it.Key;
                    act.onClick.RemoveAllListeners();
                    act.onClick.AddListener(() => { Eat(key); });
                }
                else
                {
                    act.gameObject.SetActive(false);
                }
            }
        }

        private GameObject GetRow(int index)
        {
            while (_bagRowPool.Count <= index)
            {
                var rowGo = Fishing.UiKit.Block("背包行_" + _bagRowPool.Count, _bagRows, Fishing.Palette.Hex("#16354B"));
                var le = rowGo.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 52f;
                le.minHeight = 52f;
                var nameTxt = Fishing.Txt.Make(rowGo.transform, "名称", 16, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                Fishing.UiKit.Size((RectTransform)nameTxt.transform, 260f, 44f);
                var act = Fishing.UiKit.Block("动作", rowGo.transform, Fishing.Palette.Hex("#B77932"));
                Fishing.UiKit.Size((RectTransform)act.transform, 110f, 40f);
                var actBtn = act.gameObject.AddComponent<Button>();
                actBtn.targetGraphic = act;
                var actTxt = Fishing.Txt.Make(act.transform, "文字", 14, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                Fishing.UiKit.Stretch((RectTransform)actTxt.transform, 3f);
                var hl = rowGo.gameObject.AddComponent<HorizontalLayoutGroup>();
                hl.spacing = 10f;
                hl.padding = new RectOffset(10, 10, 6, 6);
                hl.childControlWidth = true;
                hl.childControlHeight = true;
                hl.childForceExpandWidth = false;
                _bagRowPool.Add(rowGo.gameObject);
            }
            var r = _bagRowPool[index];
            r.SetActive(true);
            r.transform.SetAsLastSibling();
            return r;
        }

        private void Eat(string foodKey)
        {
            if (DiveSim.TryEat(S, foodKey))
            {
                _bagOpen = false;
                _bagPanel.SetActive(false);
            }
        }

        public void Toast(string msg) { if (_hud != null) _hud.ShowToast(msg); }
    }
}
