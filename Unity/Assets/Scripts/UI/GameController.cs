using System.Collections.Generic;
using UnityEngine;

namespace Fishing
{
    /// <summary>实时预告"现在收网会怎样"（对应 index.html 的 statusOf）。</summary>
    public struct NetStatus
    {
        public string Kind;   // risk | lose | flat | ok
        public string Text;
    }

    /// <summary>
    /// 游戏主控制器：主循环 + 输入 + 视图刷新。
    /// 规则全部委托给 <see cref="Sim"/>（纯逻辑、可无头测试），这里只负责驱动和显示。
    /// </summary>
    public class GameController : MonoBehaviour
    {
        public static GameController Instance;

        public readonly GameState S = new GameState();

        private MainLayout _layout;
        private HudView _hud;
        private StripView _strip;
        private ControlsView _controls;
        private GaugeView _gauge;
        private PreopenView _preopen;
        private SettlementView _settlement;
        private EveningView _evening;
        private FishWindowView _fishWindow;
        private PosWindowView _posWindow;
        private GaugeState _gaugeShown;
        private string _gaugeVerdictShown;

        private void Awake()
        {
            Instance = this;
            Sim.Toast += OnSimToast;
        }

        private void OnDestroy()
        {
            Sim.Toast -= OnSimToast;
            if (Instance == this) Instance = null;
        }

        private void OnSimToast(string msg)
        {
            if (_layout != null) _layout.Toast.Show(msg);
        }

        // =====================================================================
        //  视图搭建
        // =====================================================================

        public void BuildUI(Canvas canvas, bool includeStaticBackground = true)
        {
            var canvasRoot = canvas.GetComponent<RectTransform>();

            _layout = new MainLayout();
            _layout.Build(canvasRoot, includeStaticBackground);

            _hud = new HudView(_layout.Hud);
            _strip = new StripView(_layout.StripHost);
            _controls = new ControlsView(_layout.ControlsHost);
            _gauge = _layout.Gauge;

            _preopen = new PreopenView();
            _settlement = new SettlementView();
            _evening = new EveningView();
            _fishWindow = new FishWindowView();
            _posWindow = new PosWindowView();

            // 行尺寸必须按 Canvas 的画布单位算（不是屏幕像素），否则配了 CanvasScaler 会算错
            _layout.PlaceRowsFromCanvas();

            S.Ui.CastCount = Num.Clamp(1, 0, S.CompleteNets);
            RefreshAll();
        }

        // =====================================================================
        //  主循环（对应 index.html 的 loop）
        // =====================================================================

        private void Update()
        {
            float dt = Mathf.Min(0.25f, Time.unscaledDeltaTime);

            HandleKeys();

            Sim.TickSession(S, dt);
            if (Sim.GaugeTick(S, dt))
            {
                S.GaugeIdx++;
                _gauge.Show(false);
            }

            RefreshAll();
            _layout.Toast.Tick();
        }

        private void RefreshAll()
        {
            _hud.Refresh(S);

            bool gaugePending = S.Gauge != null && S.Gauge.Verdict == null;
            _layout.ChartView.Draw(S, Time.unscaledTime * 1000f, gaugePending);
            _strip.Refresh(S);
            _controls.Refresh(S, gaugePending);

            RefreshStatBar();
            RefreshOverlay();
            _posWindow.Tick(S);
            RefreshGaugeView();
        }

        private void RefreshStatBar()
        {
            float invest = 0f, floatPl = 0f;
            for (int i = 0; i < S.Nets.Count; i++)
            {
                invest += S.Nets[i].Entry;
                floatPl += S.Price - S.Nets[i].Entry;
            }
            float total = floatPl + S.RealizedPl;
            float rate = S.SessionInvested > 0f ? total / S.SessionInvested * 100f : float.NaN;

            _layout.SPrice.text = Num.F1(S.Price);
            _layout.SHold.text = S.Nets.Count + " 张 / " + Num.F1(invest);
            _layout.SFloat.text = Num.Signed(floatPl);
            _layout.SFloat.color = Palette.Of(floatPl);

            string totalText = Num.Signed(total);
            if (!float.IsNaN(rate)) totalText += "（" + (rate > 0f ? "+" : "") + Num.F1(rate) + "%）";
            _layout.STotal.text = totalText;
            _layout.STotal.color = Palette.Of(total);
        }

        private void RefreshOverlay()
        {
            bool wantPreopen = S.Phase == "preopen";
            bool wantSettle = S.Phase == "settlement";
            bool wantEvening = S.Phase == "evening";

            if (!wantPreopen && _preopen.Visible) _preopen.Hide(_layout.Overlay);
            if (!wantSettle && _settlement.Visible) _settlement.Hide(_layout.Overlay);
            if (!wantEvening && _evening.Visible) _evening.Hide(_layout.Overlay);

            if (wantPreopen)
            {
                _layout.Overlay.gameObject.SetActive(true);
                _preopen.Show(_layout.Overlay, S);
            }
            else if (wantSettle)
            {
                _layout.Overlay.gameObject.SetActive(true);
                _settlement.Show(_layout.Overlay, S);
            }
            else if (wantEvening)
            {
                _layout.Overlay.gameObject.SetActive(true);
                _evening.Show(_layout.Overlay, S);
            }
            else
            {
                _layout.Overlay.gameObject.SetActive(false);
            }
        }

        // =====================================================================
        //  工具
        // =====================================================================

        public void Toast(string msg)
        {
            _layout.Toast.Show(msg);
        }

        /// <summary>对应 statusOf(price, entry)。</summary>
        public static NetStatus StatusOf(float price, float entry)
        {
            var st = new NetStatus();
            float ratio = entry > 0f ? price / entry : 1f;

            if (ratio <= Cfg.WipeRatio)
            {
                st.Kind = "risk";
                st.Text = "收网即丢失 " + Num.F1((ratio - 1f) * 100f) + "%";
                return st;
            }
            if (ratio < 1f)
            {
                st.Kind = "lose";
                st.Text = "收网破损 " + Num.F1((1f - ratio) * 100f) + "%";
                return st;
            }

            float pl = price - entry;
            if (pl == 0f)
            {
                st.Kind = "flat";
                st.Text = "持平 · 归还 1 张";
                return st;
            }

            int nets = Mathf.Max(1, Mathf.FloorToInt(ratio + 1e-9f));
            float loot = Mathf.Max(0f, pl - (nets - 1) * Cfg.NetValue);
            st.Kind = "ok";
            st.Text = "收网得 " + nets + " 张网"
                     + (loot > 0f ? " + " + Num.F1(loot) + " 资源" : "")
                     + (price >= Cfg.BonusLine ? " + 线虫" : "");
            return st;
        }

        /// <summary>对应 netResultText(x)。</summary>
        public string NetResultText(GameState s, NetRecord x)
        {
            if (x.Result == "lost")
            {
                return "网丢失 " + Num.F1((1f - x.Price / x.Entry) * 100f) + "%";
            }
            if (x.Result == "damaged")
            {
                if (x.Fixed > 0) return "破损 " + Num.F1(x.Damage) + "% · 自动修补 −" + x.WormsUsed + " 线虫";
                int lack = Mathf.Max(0, Mathf.CeilToInt((100f - x.Integrity) / Cfg.RepairPerWorm) - s.Worms);
                return "破损 " + Num.F1(x.Damage) + "% · 待修补（缺 " + lack + " 条线虫）";
            }
            if (x.Result == "win" || x.NetReturn > 1)
            {
                return "丰收 " + Mathf.Max(1, x.NetReturn) + " 张"
                     + (x.NetSpend > 0f ? " · 网费 " + Num.F1(x.NetSpend) : "");
            }
            return Widgets.ResultText.ContainsKey(x.Result) ? Widgets.ResultText[x.Result] : x.Result;
        }

        // =====================================================================
        //  输入
        // =====================================================================

        private void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && (_posWindow.Visible || _fishWindow.Visible))
            {
                CloseModal();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (S.Phase == "running" || S.Phase == "paused") TogglePause();
                else if (S.Phase == "preopen" && S.CompleteNets > 0) StartSession();
                return;
            }

            if (Input.GetKeyDown(KeyCode.A))
            {
                if (S.Phase == "paused") CollectAll();
                return;
            }
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (S.Phase == "running" || S.Phase == "paused") EndDay();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Q)) { PressGauge(); return; }
            if (Input.GetKeyDown(KeyCode.R)) { OpenPosWindow(); return; }
            if (Input.GetKeyDown(KeyCode.F)) { OpenFishWindow(); return; }
            if (Input.GetKeyDown(KeyCode.Return) && S.Phase == "preopen" && S.CompleteNets > 0) StartSession();
            if (Input.GetKeyDown(KeyCode.Escape) && S.Phase == "paused") TogglePause();
        }

        // =====================================================================
        //  操作入口（薄封装：调 Sim + 刷 UI）
        // =====================================================================

        public void StepCast(int delta)
        {
            Sim.StepCast(S, delta);
            RefreshAll();
        }

        public void StartSession()
        {
            Sim.StartSession(S);
            _layout.Overlay.gameObject.SetActive(false);
            _gauge.Show(false);
            RefreshAll();
        }

        public void TogglePause()
        {
            if (S.Phase == "running")
            {
                S.Phase = "paused";
                S.Ui.CastCount = Num.Clamp(S.Ui.CastCount <= 0 ? 1 : S.Ui.CastCount,
                                           S.CompleteNets > 0 ? 1 : 0, S.CompleteNets);
            }
            else if (S.Phase == "paused")
            {
                S.Phase = "running";
            }
            RefreshAll();
        }

        public void TryCollect(int id)
        {
            Sim.TryCollect(S, id);
            RefreshAll();
        }

        public void CollectAll()
        {
            Sim.CollectAll(S);
            RefreshAll();
        }

        public void CollectSelected()
        {
            Sim.CollectSelected(S);
            RefreshAll();
        }

        public void CastNets(int count)
        {
            Sim.CastNets(S, count);
            RefreshAll();
        }

        public void CloseMarket(bool early)
        {
            Sim.CloseMarket(S, early);
            _gauge.Show(false);
            RefreshAll();
        }

        public void EndDay()
        {
            Sim.EndDay(S);
            RefreshAll();
        }

        public void SkipSession()
        {
            Sim.SkipSession(S);
            CloseModal();
            RefreshAll();
        }

        public void NextSession()
        {
            Sim.NextSession(S);
            CloseModal();
            RefreshAll();
        }

        public void ToEvening()
        {
            Sim.ToEvening(S);
            RefreshAll();
        }

        public void NextDay()
        {
            Sim.NextDay(S);
            CloseModal();
            RefreshAll();
        }

        // =====================================================================
        //  仪表盘
        // =====================================================================

        public void OpenGauge()
        {
            Sim.OpenGauge(S);
            RefreshGaugeView();
        }

        public void PressGauge()
        {
            if (!Sim.PressGauge(S)) return;
            RefreshAll();
        }

        /// <summary>
        /// 仪表盘完全由 GameState 驱动。这样定时自动弹出、超时判差、逐帧指针、
        /// 提前收市和重开都走同一条同步路径，不会再出现“逻辑在转但 UI 没显示”。
        /// </summary>
        private void RefreshGaugeView()
        {
            var g = S.Gauge;
            if (g == null)
            {
                _gauge.Show(false);
                _gaugeShown = null;
                _gaugeVerdictShown = null;
                return;
            }

            bool newlyOpened = !object.ReferenceEquals(_gaugeShown, g);
            if (newlyOpened)
            {
                _gaugeShown = g;
                _gaugeVerdictShown = null;
                _gauge.Show(true);
                _gauge.BuildSegments(S.GaugeLayout);
                _gauge.ResetVerdictAlpha();
                _gauge.SetTitle("按 Q 停下指针", Palette.Muted);
            }

            _gauge.SetNeedle(g.Pos);
            _gauge.SetTimerFill(1f - g.U / Cfg.GaugeWindow);

            if (g.Verdict != _gaugeVerdictShown)
            {
                _gaugeVerdictShown = g.Verdict;
                if (g.Verdict == null)
                {
                    _gauge.ResetVerdictAlpha();
                    _gauge.SetTitle("按 Q 停下指针", Palette.Muted);
                }
                else
                {
                    _gauge.ShowVerdict(S.GaugeLayout, g.Verdict);
                    string kind = g.Verdict;
                    _gauge.SetTitle(GaugeKit.Text.ContainsKey(kind) ? GaugeKit.Text[kind] : kind,
                                    Palette.GaugeColor(kind));
                }
            }
        }

        public void CloseGauge()
        {
            S.Gauge = null;
            RefreshGaugeView();
        }

        // =====================================================================
        //  调试面板 / 重开
        // =====================================================================

        public void ResetGame()
        {
            S.ResetAll();
            S.Gauge = null;
            _gauge.Show(false);
            _layout.Overlay.gameObject.SetActive(false);
            CloseModal();
            RefreshAll();
            Toast("Demo 已重置");
        }

        public void DevAction(string kind)
        {
            switch (kind)
            {
                case "nets":
                    S.CompleteNets += 2;
                    Toast("完整网 +2");
                    break;
                case "worms":
                    S.Worms += 20;
                    Toast("线虫 +20");
                    break;
                case "fish":
                    {
                        var bag = Loot.Decompose(200);
                        Loot.AwardBag(S, bag, false);
                        Toast("渔获 +200 价值");
                        break;
                    }
                case "damage":
                    Sim.DamageOneNet(S);
                    break;
                case "gauge":
                    if (S.Phase != "running" && S.Phase != "paused") { Toast("先开始捕捞"); break; }
                    if (S.Gauge != null) { Toast("仪表盘已经在转了"); break; }
                    OpenGauge();
                    break;
                case "end":
                    if (S.Phase == "running" || S.Phase == "paused") CloseMarket(true);
                    else Toast("当前不在捕捞中");
                    break;
                case "reset":
                    ResetGame();
                    break;
            }
            RefreshAll();
        }

        // =====================================================================
        //  二级窗口
        // =====================================================================

        public void OpenFishWindow()
        {
            _posWindow.Hide();
            _fishWindow.Show(_layout.Modal, S);
        }

        public void OpenPosWindow()
        {
            _fishWindow.Hide();
            _posWindow.Show(_layout.Modal, S);
            RefreshAll();
        }

        public void CloseModal()
        {
            _fishWindow.Hide();
            _posWindow.Hide();
        }
    }
}
