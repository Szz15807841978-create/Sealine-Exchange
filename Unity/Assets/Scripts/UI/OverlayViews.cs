using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Fishing
{
    /// <summary>开盘前：对应 index.html 的 preopenHTML()。</summary>
    public class PreopenView
    {
        private RectTransform _root;
        private Text _title;
        private Text _caption;
        private Text _value;
        private Text _summary;
        private Button _minus;
        private Button _plus;
        private Button _skip;
        private Button _start;
        private string _titleTemplate;
        private string _captionTemplate;
        private string _valueTemplate;
        private string _summaryTemplate;
        private bool _bound;

        public bool Visible { get { return _root != null && _root.gameObject.activeSelf; } }

        public void Hide(RectTransform overlay)
        {
            if (_root != null) _root.gameObject.SetActive(false);
            if (overlay != null) overlay.gameObject.SetActive(false);
        }

        public void Show(RectTransform overlay, GameState s)
        {
            overlay.gameObject.SetActive(true);
            if (!Bind(overlay)) return;

            _root.gameObject.SetActive(true);
            int max = Mathf.Max(0, s.CompleteNets);
            int n = Num.Clamp(s.Ui.CastCount, 0, max);
            s.Ui.CastCount = n;

            _title.text = Fill(_titleTemplate, s.Day, s.Session, max, n, max - n, Num.F1(100f * (1f + Cfg.Fee)));
            _caption.text = Fill(_captionTemplate, s.Day, s.Session, max, n, max - n, Num.F1(100f * (1f + Cfg.Fee)));
            _value.text = Fill(_valueTemplate, s.Day, s.Session, max, n, max - n, Num.F1(100f * (1f + Cfg.Fee)));
            _summary.text = Fill(_summaryTemplate, s.Day, s.Session, max, n, max - n, Num.F1(100f * (1f + Cfg.Fee)));
            _minus.interactable = n > 0;
            _plus.interactable = n < max;
            _start.interactable = max > 0;
        }

        private bool Bind(RectTransform overlay)
        {
            if (_bound) return _root != null;
            _root = overlay.Find("准备开始面板") as RectTransform;
            if (_root == null)
            {
                Debug.LogError("遮罩层中缺少场景对象“准备开始面板”，请重新执行可编辑 UI 场景化。", overlay);
                return false;
            }

            _title = Find<Text>(_root, "标题");
            _caption = Find<Text>(_root, "下网说明");
            _value = Find<Text>(_root, "网数控件/数值");
            _summary = Find<Text>(_root, "下网摘要");
            _minus = Find<Button>(_root, "网数控件/减少");
            _plus = Find<Button>(_root, "网数控件/增加");
            _skip = Find<Button>(_root, "跳过这一场");
            _start = Find<Button>(_root, "开始捕捞");

            // 场景中的文字就是模板。这样在 Inspector 修改文案后，Play 时不会被硬编码恢复。
            // 旧场景的预览文字会自动升级为带变量的模板。
            _titleTemplate = AsTemplate(_title.text, "第 1 天 · 第 1 场", "第 {day} 天 · {period}");
            if (_title.text == "第 1 天 · 早晨") _titleTemplate = "第 {day} 天 · {period}";
            _captionTemplate = AsTemplate(_caption.text, "下网数量 · 可选择 0～1 张", "下网数量 · 可选择 0～{max} 张");
            _valueTemplate = AsTemplate(_value.text, "1", "{count}");
            _summaryTemplate = AsTemplate(_summary.text, "下水 1 张 · 留手里 0 张 · 买入价 100.0",
                                           "下水 {count} 张 · 留手里 {reserve} 张 · 买入价 {price}");

            _minus.onClick.RemoveAllListeners();
            _plus.onClick.RemoveAllListeners();
            _skip.onClick.RemoveAllListeners();
            _start.onClick.RemoveAllListeners();
            _minus.onClick.AddListener(() => { if (GameController.Instance != null) GameController.Instance.StepCast(-1); });
            _plus.onClick.AddListener(() => { if (GameController.Instance != null) GameController.Instance.StepCast(1); });
            _skip.onClick.AddListener(() => { if (GameController.Instance != null) GameController.Instance.SkipSession(); });
            _start.onClick.AddListener(() => { if (GameController.Instance != null) GameController.Instance.StartSession(); });
            _bound = true;
            return true;
        }

        private static string AsTemplate(string sceneText, string oldPreview, string dynamicTemplate)
        {
            return string.IsNullOrEmpty(sceneText) || sceneText == oldPreview ? dynamicTemplate : sceneText;
        }

        private static string Fill(string template, int day, int session, int max, int count, int reserve, string price)
        {
            return (template ?? string.Empty)
                .Replace("{day}", day.ToString())
                .Replace("{session}", session.ToString())
                .Replace("{period}", session == 1 ? "早晨" : "下午")
                .Replace("{max}", max.ToString())
                .Replace("{count}", count.ToString())
                .Replace("{reserve}", reserve.ToString())
                .Replace("{price}", price);
        }

        private static T Find<T>(Transform root, string path) where T : Component
        {
            var child = root.Find(path);
            if (child == null || child.GetComponent<T>() == null)
                throw new MissingComponentException("准备开始面板缺少组件：" + path + " / " + typeof(T).Name);
            return child.GetComponent<T>();
        }
    }

    /// <summary>收市 / 收工结算：对应 index.html 的 settleHTML()。</summary>
    public class SettlementView
    {
        private readonly WindowFrame _frame = new WindowFrame();
        private SessionResult _shown;

        public bool Visible { get { return _frame.Active; } }

        public void Hide(RectTransform overlay)
        {
            if (!_frame.Active) return;
            _frame.Active = false;
            if (overlay != null) overlay.gameObject.SetActive(false);
        }

        public void Show(RectTransform overlay, GameState s)
        {
            // 结算内容是静态快照，同一份结果只建一次
            if (_frame.Active && ReferenceEquals(_shown, s.LastResult))
            {
                overlay.gameObject.SetActive(true);
                return;
            }
            _shown = s.LastResult;
            Rebuild(overlay, s);
        }

        private void Rebuild(RectTransform overlay, GameState s)
        {
            var r = s.LastResult;
            if (r == null)
            {
                r = new SessionResult { Records = new List<NetRecord>(), Price = 100f, Bag = new Dictionary<string, int>() };
            }

            _frame.Build(overlay);
            _frame.Active = true;
            var f = _frame;

            f.Title((r.Early ? "收工" : "收市") + " · " + Num.F1(r.Price));

            float sessionRate = s.SessionInvested > 0f ? r.Pl / s.SessionInvested * 100f : float.NaN;
            float histRate = s.TotalInvested > 0f ? s.TotalPl / s.TotalInvested * 100f : float.NaN;

            var boxes = f.Boxes(new[] { "本场收益", "累计", "资源", "线虫" });
            Widgets.SetBox(boxes[0], Num.Signed(r.Pl) + (float.IsNaN(sessionRate) ? "" : "（" + (r.Pl > 0f ? "+" : "") + Num.F1(sessionRate) + "%）"),
                           Palette.Of(r.Pl));
            Widgets.SetBox(boxes[1], Num.Signed(s.TotalPl) + (float.IsNaN(histRate) ? "" : "（" + (histRate > 0f ? "+" : "") + Num.F1(histRate) + "%）"),
                           Palette.Of(s.TotalPl));
            Widgets.SetBox(boxes[2], Num.F1(r.Loot), Palette.Text);
            Widgets.SetBox(boxes[3], r.Worms + (s.SessionWormsUsed > 0 ? " − " + s.SessionWormsUsed : ""), Palette.Text);

            f.Caption("逐张");
            var table = new TableBlock(f, new[] { "网", "买入", "卖出", "收益", "资源", "结果" },
                                       new[] { 0.9f, 0.8f, 0.8f, 1f, 0.8f, 2.2f }, 27f, 13);

            if (r.Records.Count == 0)
            {
                table.AddEmptyRow("本场没有下网。");
            }
            else
            {
                for (int i = 0; i < r.Records.Count; i++)
                {
                    var x = r.Records[i];
                    var cells = table.AddRow();
                    table.Set(cells,
                              "网#" + x.Id,
                              Num.F1(x.Entry),
                              Num.F1(x.Price),
                              Num.Signed(x.Pl),
                              x.Loot > 0f ? Num.F1(x.Loot) : "—",
                              "");
                    table.SetCell(cells, 3, Num.Signed(x.Pl), x.Pl >= 0f ? Palette.GreenSoft : Palette.RedSoft);
                    table.SetCell(cells, 4, x.Loot > 0f ? Num.F1(x.Loot) : "—", Palette.GreenSoft);
                    table.SetCell(cells, 5, StripTags(GameController.Instance.NetResultText(s, x)), ResultColorOf(x));
                }
            }

            f.Spacer(4f);
            FishRows.Add(f, r.Bag, false, false);

            f.Spacer(6f);
            var footer = f.Row(38f, 10f, TextAnchor.MiddleRight);
            f.RowText(footer, "完整 " + s.CompleteNets + " · 破损 " + s.BrokenNets.Count + " · 线虫 " + s.Worms,
                      12, Palette.Muted, 260f, TextAnchor.MiddleLeft, FontStyle.Normal, 1f);

            bool last = s.Session >= Cfg.SessionsPerDay || s.EndDayPending;
            if (!last)
            {
                f.RowButton(footer, "结束今天", Widgets.Hue.Ghost, 100f, 34f,
                    () => { if (GameController.Instance != null) GameController.Instance.EndDay(); }, 13);
            }
            f.RowButton(footer, last ? "进入傍晚" : "第 2 场", Widgets.Hue.Primary, 130f, 34f, () =>
            {
                if (GameController.Instance == null) return;
                if (last) GameController.Instance.ToEvening();
                else GameController.Instance.NextSession();
            }, 13);

            f.Finish();
        }

        private static Color ResultColorOf(NetRecord x)
        {
            if (x.Result == "lost") return Palette.RedSoft;
            if (x.Result == "damaged") return Palette.Amber;
            if (x.Result == "win" || x.NetReturn > 1) return Palette.GreenSoft;
            return Palette.Dim;
        }

        /// <summary>表格单元格不支持富文本配色，这里只去掉标签。</summary>
        private static string StripTags(string s)
        {
            int i = s.IndexOf('<');
            while (i >= 0)
            {
                int j = s.IndexOf('>', i);
                if (j < 0) break;
                s = s.Substring(0, i) + s.Substring(j + 1);
                i = s.IndexOf('<');
            }
            return s;
        }
    }

    /// <summary>傍晚结算：对应 index.html 的 eveningHTML()。</summary>
    public class EveningView
    {
        private readonly WindowFrame _frame = new WindowFrame();
        private string _sig;

        public bool Visible { get { return _frame.Active; } }

        public void Hide(RectTransform overlay)
        {
            if (!_frame.Active) return;
            _frame.Active = false;
            if (overlay != null) overlay.gameObject.SetActive(false);
        }

        public void Show(RectTransform overlay, GameState s)
        {
            string sig = s.Day + "|" + s.CompleteNets + "|" + s.BrokenNets.Count + "|" + s.Worms
                       + "|" + Num.F1(Loot.SellableValue(s.Fish)) + "|" + Num.F1(s.TotalPl)
                       + "|" + s.History.Count;
            if (_frame.Active && sig == _sig)
            {
                overlay.gameObject.SetActive(true);
                return;
            }
            _sig = sig;
            Rebuild(overlay, s);
        }

        private void Rebuild(RectTransform overlay, GameState s)
        {
            _frame.Build(overlay);
            _frame.Active = true;
            var f = _frame;

            f.Title("第 " + s.Day + " 天收工");

            var boxes = f.Boxes(new[] { "完整网", "破损网", "线虫", "渔获", "累计收益" });
            Widgets.SetBox(boxes[0], s.CompleteNets.ToString(), Palette.Cyan);
            Widgets.SetBox(boxes[1], s.BrokenNets.Count.ToString(), s.BrokenNets.Count > 0 ? Palette.Amber : Palette.Text);
            Widgets.SetBox(boxes[2], s.Worms.ToString(), Palette.Text);
            Widgets.SetBox(boxes[3], Num.F1(Loot.SellableValue(s.Fish)), Palette.GreenSoft);

            float rate = s.TotalInvested > 0f ? s.TotalPl / s.TotalInvested * 100f : float.NaN;
            Widgets.SetBox(boxes[4], Num.Signed(s.TotalPl) + (float.IsNaN(rate) ? "" : "（" + (rate > 0f ? "+" : "") + Num.F1(rate) + "%）"),
                           Palette.Of(s.TotalPl));

            NetsStatus.Section(f, s);

            f.Caption("记录");
            var table = new TableBlock(f, new[] { "场次", "形态", "收市价", "收益", "资源" },
                                       new[] { 1.3f, 1.5f, 0.9f, 1f, 0.9f }, 27f, 13);

            if (s.History.Count == 0)
            {
                table.AddEmptyRow("还没有记录。");
            }
            else
            {
                for (int i = 0; i < s.History.Count; i++)
                {
                    var h = s.History[i];
                    var cells = table.AddRow();
                    table.Set(cells,
                              "第" + h.Day + "天 第" + h.Session + "场",
                              h.Pattern,
                              Num.F1(h.Price),
                              Num.Signed(h.Pl),
                              h.Loot);
                    table.SetCell(cells, 3, Num.Signed(h.Pl), h.Pl >= 0f ? Palette.GreenSoft : Palette.RedSoft);
                }
            }

            f.Spacer(8f);
            var footer = f.Row(38f, 10f, TextAnchor.MiddleRight);
            f.RowButton(footer, "渔获 " + Loot.FishCount(s.Fish), Widgets.Hue.Cyan, 120f, 34f,
                () => { if (GameController.Instance != null) GameController.Instance.OpenFishWindow(); }, 13);
            f.RowButton(footer, "第 " + (s.Day + 1) + " 天", Widgets.Hue.Primary, 130f, 34f,
                () => { if (GameController.Instance != null) GameController.Instance.NextDay(); }, 13);

            f.Finish();
        }
    }

    /// <summary>渔获二级窗口：对应 index.html 的 fishWindowHTML()。</summary>
    public class FishWindowView
    {
        private readonly WindowFrame _frame = new WindowFrame();
        private string _sig;

        public bool Visible { get { return _frame.Active; } }

        public void Show(RectTransform overlay, GameState s)
        {
            string sig = s.Worms + "|" + FishSig(s.Fish);
            if (_frame.Active && sig == _sig)
            {
                overlay.gameObject.SetActive(true);
                return;
            }
            _sig = sig;
            Rebuild(overlay, s);
        }

        private static string FishSig(Dictionary<string, int> bag)
        {
            string t = "";
            foreach (var kv in bag) t += kv.Key + ":" + kv.Value + ",";
            return t;
        }

        private void Rebuild(RectTransform overlay, GameState s)
        {
            _frame.Build(overlay);
            _frame.Active = true;
            var f = _frame;

            f.Title("渔获");
            var boxes = f.Boxes(new[] { "条数", "价值", "线虫" });
            Widgets.SetBox(boxes[0], Loot.FishCount(s.Fish).ToString(), Palette.Text);
            Widgets.SetBox(boxes[1], Num.F1(Loot.SellableValue(s.Fish)), Palette.GreenSoft);
            Widgets.SetBox(boxes[2], s.Worms.ToString(), Palette.Text);

            f.Caption("鱼篓");
            FishRows.Add(f, s.Fish, true, true);

            f.Spacer(8f);
            var footer = f.Row(38f, 10f, TextAnchor.MiddleRight);
            f.RowButton(footer, "关闭", Widgets.Hue.Primary, 110f, 34f,
                () => { if (GameController.Instance != null) GameController.Instance.CloseModal(); }, 13);

            f.Finish();
        }

        public void Hide()
        {
            if (!_frame.Active) return;
            _frame.Active = false;
        }
    }
}
