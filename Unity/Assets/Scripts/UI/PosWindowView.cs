using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Fishing
{
    /// <summary>持仓二级窗口：对应 index.html 的 posWindowHTML() / renderNets() 里的明细表。</summary>
    public class PosWindowView
    {
        // 列宽比例：勾选 / 网 / 买入 / 当前 / 收益 / 预告 / 操作
        private static readonly float[] Ratios = { 0.34f, 0.90f, 0.66f, 0.66f, 0.74f, 1.50f, 0.92f };

        private WindowFrame _frame;
        private RectTransform _modal;
        private readonly List<Text[]> _rowCells = new List<Text[]>();
        private readonly List<Toggle> _rowToggles = new List<Toggle>();
        private readonly List<Button> _rowButtons = new List<Button>();
        private Toggle _chkAll;
        private Button _collectSel;
        private Text _castVal;
        private Button _castMinus, _castPlus, _castNow, _allIn;
        private string _sig;

        public bool Visible { get { return _frame != null && _frame.Active; } }

        public void Show(RectTransform modal, GameState s)
        {
            // 只有数据变了才重建卡片，按钮/勾选的状态走 RefreshRowStates
            string sig = s.Nets.Count + "|" + JoinIds(s.Nets) + "|" + s.Ui.CastCount + "|" + s.CompleteNets
                       + "|" + s.Phase + "|" + Num.F1(s.Cd);
            if (Visible && sig == _sig)
            {
                modal.gameObject.SetActive(true);
                RefreshRowStates(s);
                return;
            }
            _sig = sig;
            Rebuild(modal, s);
        }

        private static string JoinIds(List<Net> nets)
        {
            string t = "";
            for (int i = 0; i < nets.Count; i++) t += nets[i].Id + ":";
            return t;
        }

        private void Rebuild(RectTransform modal, GameState s)
        {
            _modal = modal;
            modal.gameObject.SetActive(true);

            if (_frame == null) _frame = new WindowFrame();
            ClearToggleHandlers();
            _frame.Build(modal);
            _frame.Active = true;

            _rowCells.Clear();
            _rowToggles.Clear();
            _rowButtons.Clear();

            var f = _frame;
            f.Title("持仓");

            s.Ui.CastCount = Num.Clamp(s.Ui.CastCount, 1, Mathf.Max(1, s.CompleteNets));

            // ---- 表头（左：列名  /  右：收选中）----
            var head = UiKit.Rect("表头", f.Content);
            UiKit.Size(head, -1f, 30f);
            UiKit.Row(head, 8f);

            var cols = UiKit.Rect("列名", head);
            UiKit.Size(cols, -1f, 30f, 1f);
            MakeColumns(cols);

            // 第一列放勾选框；"网" 列的标题往右挪一点，避开勾选列
            var firstCol = CellText(cols, 0, "", 11, Palette.Muted, TextAnchor.MiddleLeft);
            var allGo = UiKit.Rect("全选", firstCol.rectTransform.parent as RectTransform);
            UiKit.Fixed(allGo, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), 18f, 18f);
            _chkAll = MakeToggle(allGo, null);
            Hook(_chkAll, on =>
            {
                s.Ui.Selected.Clear();
                if (on)
                {
                    for (int i = 0; i < s.Nets.Count; i++) s.Ui.Selected.Add(s.Nets[i].Id);
                }
                SyncRowToggles(s);
                RefreshRowStates(s);
            });

            var netHeader = CellText(cols, 1, "网", 11, Palette.Muted, TextAnchor.MiddleLeft);
            UiKit.Stretch(netHeader.rectTransform, 14f, 0f, 0f, 0f);
            CellText(cols, 2, "买入", 11, Palette.Muted, TextAnchor.MiddleLeft);
            CellText(cols, 3, "当前", 11, Palette.Muted, TextAnchor.MiddleLeft);
            CellText(cols, 4, "收益", 11, Palette.Muted, TextAnchor.MiddleLeft);
            CellText(cols, 5, "预告", 11, Palette.Muted, TextAnchor.MiddleLeft);

            _collectSel = Widgets.MakeButton(head, "收选中 0", Widgets.Hue.Ghost, 11, 100f, 26f, () =>
            {
                if (GameController.Instance != null) GameController.Instance.CollectSelected();
            });
            AddLine(head, Palette.LineSoft);

            // ---- 数据行 ----
            var scrollHost = UiKit.Rect("行区", f.Content);
            UiKit.Size(scrollHost, -1f, 0f);
            var scrollLe = scrollHost.GetComponent<LayoutElement>();
            scrollLe.minHeight = 40f;

            var scroll = scrollHost.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            var viewport = UiKit.Rect("视口", scrollHost);
            UiKit.Stretch(viewport);
            var mask = viewport.gameObject.AddComponent<RectMask2D>();
            mask.padding = Vector4.zero;

            var rows = UiKit.Rect("行容器", viewport);
            rows.anchorMin = new Vector2(0f, 1f);
            rows.anchorMax = new Vector2(1f, 1f);
            rows.pivot = new Vector2(0.5f, 1f);
            rows.offsetMin = new Vector2(0f, 0f);
            rows.offsetMax = new Vector2(0f, 0f);
            var rowsCol = UiKit.Col(rows, 0f);
            rowsCol.childForceExpandWidth = true;
            rowsCol.childControlHeight = true;
            var fitter = rows.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = rows;

            if (s.Nets.Count == 0)
            {
                var empty = Txt.Make(rows, "空", 12, Palette.Dim, TextAnchor.MiddleLeft);
                UiKit.Size(empty.rectTransform, -1f, 34f);
                empty.text = "还没有下网";
            }
            else
            {
                for (int i = 0; i < s.Nets.Count; i++)
                {
                    BuildNetRow(rows, s, s.Nets[i]);
                }
            }

            float rowsHeight = Mathf.Max(40f, s.Nets.Count * 32f + 4f);
            scrollLe.preferredHeight = Mathf.Min(rowsHeight, 240f);
            scrollLe.minHeight = Mathf.Min(rowsHeight, 240f);

            // ---- 底部：下网 / 全部收网 / 关闭 ----
            var footer = f.Row(40f, 10f, TextAnchor.MiddleLeft);
            var stepper = UiKit.Rect("步进器", footer);
            UiKit.Size(stepper, 108f, 34f);
            UiKit.Row(stepper, 0f);

            _castMinus = Widgets.MakeButton(stepper, "−", Widgets.Hue.Normal, 18, 36f, 34f, () =>
            {
                s.Ui.CastCount = Mathf.Max(1, s.Ui.CastCount - 1);
                UpdateStepper(s);
            });
            _castVal = Txt.Make(stepper, "数值", 17, Palette.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Size(_castVal.rectTransform, 36f, 34f);
            _castVal.text = s.Ui.CastCount.ToString();
            _castPlus = Widgets.MakeButton(stepper, "+", Widgets.Hue.Normal, 18, 36f, 34f, () =>
            {
                s.Ui.CastCount = Num.Clamp(s.Ui.CastCount + 1, 1, Mathf.Max(1, s.CompleteNets));
                UpdateStepper(s);
            });

            _castNow = Widgets.MakeButton(footer, "下网", Widgets.Hue.Cyan, 12, 76f, 34f, () =>
            {
                if (GameController.Instance != null) GameController.Instance.CastNets(s.Ui.CastCount);
            });

            var right = UiKit.Rect("右侧", footer);
            UiKit.Size(right, 300f, 34f, 1f);
            var rightRow = UiKit.Row(right, 10f);
            rightRow.childAlignment = TextAnchor.MiddleRight;

            _allIn = Widgets.MakeButton(right, "全部收网", Widgets.Hue.Green, 12, 100f, 34f, () =>
            {
                if (GameController.Instance != null) GameController.Instance.CollectAll();
            });
            Widgets.MakeButton(right, "关闭", Widgets.Hue.Normal, 12, 76f, 34f, () =>
            {
                if (GameController.Instance != null) GameController.Instance.CloseModal();
            });

            _frame.Finish();
            UpdateStepper(s);
            RefreshRowStates(s);
        }

        private void BuildNetRow(RectTransform parent, GameState s, Net net)
        {
            var row = UiKit.Rect("网#" + net.Id, parent);
            UiKit.Size(row, -1f, 32f);
            MakeColumns(row);

            // 勾选框（放进第一列，和表头对齐）
            var toggleGo = UiKit.Rect("勾选", CellHost(row, 0));
            UiKit.Fixed(toggleGo, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), 18f, 18f);
            int netId = net.Id;
            var toggle = MakeToggle(toggleGo, null);
            Hook(toggle, on =>
            {
                if (on) s.Ui.Selected.Add(netId);
                else s.Ui.Selected.Remove(netId);
                RefreshRowStates(s);
            });
            _rowToggles.Add(toggle);

            // 网名（带颜色小方块）
            var nameHost = CellHost(row, 1);
            var dot = UiKit.Block("点", nameHost, net.Color);
            UiKit.Fixed(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), 8f, 8f);
            var name = Txt.Make(nameHost, "名", 13, Palette.Text, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)name.transform, 14f, 0f, 0f, 0f);
            name.text = "网#" + net.Id;

            var cells = new Text[7];
            cells[2] = CellText(row, 2, "", 13, Palette.Text, TextAnchor.MiddleLeft);
            cells[3] = CellText(row, 3, "", 13, Palette.Text, TextAnchor.MiddleLeft);
            cells[4] = CellText(row, 4, "", 13, Palette.Text, TextAnchor.MiddleLeft);
            cells[5] = CellText(row, 5, "", 12, Palette.Muted, TextAnchor.MiddleLeft);

            var btn = Widgets.MakeButton(row, "收网", Widgets.Hue.Normal, 12, -1f, 26f, () =>
            {
                if (GameController.Instance != null) GameController.Instance.TryCollect(netId);
            });
            var le = btn.gameObject.GetComponent<LayoutElement>();
            le.flexibleWidth = Ratios[6];

            _rowCells.Add(cells);
            _rowButtons.Add(btn);

            AddLine(row, Palette.RowLine);
        }

        private static void MakeColumns(RectTransform row)
        {
            var h = UiKit.Row(row, 4f);
            h.childForceExpandWidth = true;
            h.childControlWidth = true;
            h.childControlHeight = true;
        }

        private static RectTransform CellHost(RectTransform row, int column)
        {
            var host = UiKit.Rect("列" + column, row);
            var le = host.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = Ratios[column];
            le.minWidth = 24f;
            return host;
        }

        private static Text CellText(RectTransform row, int column, string text, int size, Color color, TextAnchor anchor)
        {
            var host = CellHost(row, column);
            var t = Txt.Make(host, "文本", size, color, anchor);
            UiKit.Stretch(t.rectTransform);
            t.text = text;
            return t;
        }

        private static void AddLine(RectTransform row, Color color)
        {
            var line = UiKit.Block("下边线", row, color);
            line.raycastTarget = false;
            var rt = line.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 1f);
        }

        private static Toggle MakeToggle(RectTransform host, UnityEngine.Events.UnityAction<bool> onChanged)
        {
            var bg = UiKit.Outline("框", host, Palette.BoxBg, Palette.Line);
            UiKit.Stretch(bg.rectTransform);
            var toggle = bg.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = bg;

            var check = UiKit.Block("勾", bg.rectTransform, Palette.Cyan);
            UiKit.Stretch(check.rectTransform, 3f, 3f, 3f, 3f);
            toggle.graphic = check;

            if (onChanged != null) toggle.onValueChanged.AddListener(onChanged);
            return toggle;
        }

        private readonly Dictionary<Toggle, UnityEngine.Events.UnityAction<bool>> _toggleHandlers =
            new Dictionary<Toggle, UnityEngine.Events.UnityAction<bool>>();

        /// <summary>程序化设置勾选状态：先摘掉回调，避免刷新时递归触发。</summary>
        private void SetToggleSilently(Toggle t, bool on)
        {
            if (t == null || t.isOn == on) return;
            UnityEngine.Events.UnityAction<bool> handler;
            bool has = _toggleHandlers.TryGetValue(t, out handler);
            if (has) t.onValueChanged.RemoveListener(handler);
            t.isOn = on;
            if (has) t.onValueChanged.AddListener(handler);
        }

        /// <summary>注册回调并记录，供 SetToggleSilently 使用。</summary>
        private void Hook(Toggle t, UnityEngine.Events.UnityAction<bool> onChanged)
        {
            _toggleHandlers[t] = onChanged;
            t.onValueChanged.AddListener(onChanged);
        }

        private void ClearToggleHandlers()
        {
            foreach (var kv in _toggleHandlers)
            {
                if (kv.Key != null) kv.Key.onValueChanged.RemoveListener(kv.Value);
            }
            _toggleHandlers.Clear();
        }

        private void UpdateStepper(GameState s)
        {
            if (_castVal == null) return;
            int n = Num.Clamp(s.Ui.CastCount, 1, Mathf.Max(1, s.CompleteNets));
            s.Ui.CastCount = n;
            _castVal.text = n.ToString();
            _castMinus.interactable = n > 1;
            _castPlus.interactable = n < s.CompleteNets;
        }

        /// <summary>刷新每行的数值 / 按钮可用性 / 勾选状态（对应 renderNets 里那段同步）。</summary>
        public void RefreshRowStates(GameState s)
        {
            if (!Visible) return;

            bool paused = s.Phase == "paused";
            bool canAct = paused && s.Cd <= 0f;

            for (int i = 0; i < _rowCells.Count && i < s.Nets.Count; i++)
            {
                var net = s.Nets[i];
                var cells = _rowCells[i];
                float pl = s.Price - net.Entry;
                var st = GameController.StatusOf(s.Price, net.Entry);

                cells[2].text = Num.F1(net.Entry);
                cells[3].text = Num.F1(s.Price);
                cells[4].text = Num.Signed(pl);
                cells[4].color = pl >= 0f ? Palette.GreenSoft : Palette.RedSoft;
                cells[5].text = st.Text;
                cells[5].color = st.Kind == "risk" ? Palette.RedSoft :
                                 (st.Kind == "lose" ? Palette.Amber : Palette.Muted);

                _rowButtons[i].interactable = canAct;
                _rowToggles[i].interactable = canAct;
                SetToggleSilently(_rowToggles[i], s.Ui.Selected.Contains(net.Id));
            }

            int sel = 0;
            for (int i = 0; i < s.Nets.Count; i++) if (s.Ui.Selected.Contains(s.Nets[i].Id)) sel++;

            if (_collectSel != null)
            {
                var label = Widgets.LabelOf(_collectSel);
                if (label != null) label.text = "收选中 " + sel;
                _collectSel.interactable = canAct && sel > 0;
            }

            if (_chkAll != null)
            {
                SetToggleSilently(_chkAll, s.Nets.Count > 0 && sel == s.Nets.Count);
                _chkAll.interactable = paused && s.Nets.Count > 0;
            }

            if (_castNow != null) _castNow.interactable = paused && s.CompleteNets > 0;
            if (_allIn != null) _allIn.interactable = canAct && s.Nets.Count > 0;
            UpdateStepper(s);
        }

        public void Tick(GameState s)
        {
            if (Visible) RefreshRowStates(s);
        }

        /// <summary>把每行的勾选框同步成 Selected 集合的状态（不触发回调）。</summary>
        private void SyncRowToggles(GameState s)
        {
            for (int i = 0; i < _rowToggles.Count && i < s.Nets.Count; i++)
            {
                SetToggleSilently(_rowToggles[i], s.Ui.Selected.Contains(s.Nets[i].Id));
            }
        }

        public void Hide()
        {
            if (_frame == null || !_frame.Active) return;
            _frame.Active = false;
            if (_modal != null) _modal.gameObject.SetActive(false);
        }
    }
}
