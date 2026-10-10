using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Fishing
{
    /// <summary>各窗口共用的表格构建器：表头 + 数据行（列宽按比例分配）。</summary>
    public class TableBlock
    {
        public RectTransform Root;
        public readonly List<Text[]> Rows = new List<Text[]>();

        private readonly float[] _ratios;
        private readonly int _fontSize;
        private readonly float _rowHeight;

        public TableBlock(WindowFrame frame, string[] headers, float[] ratios, float rowHeight = 27f, int fontSize = 13)
        {
            _ratios = ratios;
            _rowHeight = rowHeight;
            _fontSize = fontSize;

            Root = UiKit.Rect("表格", frame.Content);
            UiKit.Size(Root, -1f, 0f);
            UiKit.Col(Root, 0f);

            if (headers != null && headers.Length > 0 && headers[0] != null)
            {
                var row = MakeRow(true);
                for (int i = 0; i < headers.Length; i++)
                {
                    var t = MakeCell(row, i, headers[i], Palette.Muted, TextAnchor.MiddleLeft);
                    if (i == headers.Length - 1) t.alignment = TextAnchor.MiddleRight;
                }
                AddBottomLine(row, Palette.LineSoft);
            }
        }

        /// <summary>加一行数据，返回该行的文本数组（按列顺序）。</summary>
        public Text[] AddRow()
        {
            var row = MakeRow(false);
            var cells = new Text[_ratios.Length];
            for (int i = 0; i < _ratios.Length; i++)
            {
                cells[i] = MakeCell(row, i, "", Palette.Text, TextAnchor.MiddleLeft);
                if (i == _ratios.Length - 1) cells[i].alignment = TextAnchor.MiddleRight;
            }
            Rows.Add(cells);
            return cells;
        }

        public void AddEmptyRow(string text)
        {
            var row = MakeRow(false);
            var t = Txt.Make(row, "空", _fontSize, Palette.Dim, TextAnchor.MiddleLeft);
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            t.text = text;
        }

        private RectTransform MakeRow(bool header)
        {
            var row = UiKit.Rect(header ? "表头" : "行", Root);
            UiKit.Size(row, -1f, _rowHeight);
            var h = UiKit.Row(row, 6f);
            h.childForceExpandWidth = true;
            if (!header) AddBottomLine(row, Palette.RowLine);
            Root.GetComponent<LayoutElement>().preferredHeight += _rowHeight;
            return row;
        }

        private static void AddBottomLine(RectTransform row, Color color)
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

        private Text MakeCell(RectTransform row, int column, string text, Color color, TextAnchor anchor)
        {
            var t = Txt.Make(row, "格", _fontSize, color, anchor);
            var le = t.gameObject.AddComponent<LayoutElement>();
            float r = column < _ratios.Length ? _ratios[column] : 1f;
            le.flexibleWidth = r;
            le.minWidth = 30f;
            t.text = text;
            return t;
        }

        public void Set(Text[] cells, params string[] values)
        {
            for (int i = 0; i < cells.Length && i < values.Length; i++) cells[i].text = values[i];
        }

        public void SetCell(Text[] cells, int index, string value, Color color)
        {
            if (index < 0 || index >= cells.Length) return;
            cells[index].text = value;
            cells[index].color = color;
        }
    }

    /// <summary>「网」状态区：破损网列表 + 需要多少线虫。对应 netsStatusHTML()。</summary>
    public static class NetsStatus
    {
        public static int NeedWorms(BrokenNet b)
        {
            return Mathf.CeilToInt((100f - b.Repair) / Cfg.RepairPerWorm);
        }

        public static void Section(WindowFrame frame, GameState s)
        {
            if (s.BrokenNets.Count == 0) return;

            frame.Caption("网");
            var sorted = new List<BrokenNet>(s.BrokenNets);
            sorted.Sort((a, b) => b.Repair.CompareTo(a.Repair));

            int total = 0;
            for (int i = 0; i < sorted.Count; i++)
            {
                var b = sorted[i];
                int need = NeedWorms(b);
                total += need;
                int lack = Mathf.Max(0, need - s.Worms);

                var row = frame.Row(26f, 10f, TextAnchor.MiddleLeft);
                frame.RowText(row, "破损网 #" + b.Id + " · 完整度 " + Num.F1(b.Repair) + "%", 13,
                              Palette.Text, 300f, TextAnchor.MiddleLeft, FontStyle.Normal, 1f);
                frame.RowText(row, lack > 0 ? "缺 " + lack + " 线虫" : "自动修复中", 13,
                              lack > 0 ? Palette.RedSoft : Palette.GreenSoft, 170f,
                              TextAnchor.MiddleRight, FontStyle.Normal, 0f);
            }

            frame.Sub("需 " + total + " 线虫 · 现有 " + s.Worms, 20f);
        }
    }

    /// <summary>鱼篓胶囊区（对应 fishHTML）。</summary>
    public static class FishRows
    {
        public static void Add(WindowFrame frame, Dictionary<string, int> bag, bool showValue, bool skipWorm)
        {
            var ids = new List<string>();
            foreach (var kv in bag)
            {
                if (kv.Value <= 0) continue;
                if (skipWorm && kv.Key == "worm") continue;
                ids.Add(kv.Key);
            }

            if (ids.Count == 0)
            {
                frame.Sub("鱼篓是空的。", 24f);
                return;
            }

            ids.Sort((a, b) =>
            {
                var fa = FishDefs.ById(a);
                var fb = FishDefs.ById(b);
                int va = fa != null ? fa.Value : 0;
                int vb = fb != null ? fb.Value : 0;
                return vb.CompareTo(va);
            });

            const int perRow = 3;
            for (int start = 0; start < ids.Count; start += perRow)
            {
                var row = frame.Row(32f, 7f);
                int end = Mathf.Min(start + perRow, ids.Count);
                for (int i = start; i < end; i++)
                {
                    var f = FishDefs.ById(ids[i]);
                    if (f == null) continue;
                    var chip = Widgets.MakeFishChip(row);
                    Widgets.SetFishChip(chip, f, bag[ids[i]], showValue);
                }
            }
        }
    }
}
