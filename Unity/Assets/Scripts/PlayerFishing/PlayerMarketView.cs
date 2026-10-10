using System;
using UnityEngine;
using UnityEngine.UI;

namespace Fishing.Player
{
    [Serializable] public class PlayerTextBinding { public string Key; public Text Target; }
    [Serializable] public class PlayerButtonBinding { public string Action; public Button Target; }
    [AddComponentMenu("Fishing/Player/Scene UI References")]
    [ExecuteAlways]
    public sealed class PlayerMarketView : MonoBehaviour
    {
        [Header("场景对象引用；运行时不生成、不重新排版")]
        public Canvas Canvas;
        public Image Background;
        public PlayerMarketChart Chart;
        public PlayerTextBinding[] Texts;
        public PlayerButtonBinding[] Buttons;
        public InputField WeightInput,BaitInput,PercentInput,DebugFishInput,DebugBaitInput;
        public Slider CleanSlider;
        public Dropdown ShapeDropdown;
        public Toggle RevealToggle;
        public GameObject DebugPanel,HelpPanel,DivePanel,ResultPanel,ReturnPanel,ResetPanel,ChallengePanel,TimingPanel,SpeedPanel;
        public GameObject RadioExpanded;
        public RectTransform[] RaftInputBlockers;
        public RectTransform TimingTrack,TimingNeedle;
        public Image[] TimingSegments;
        public Image CooldownFill,ChallengeTimeFill,SpeedFill,TemperatureFill,StaminaFill;
        public RectTransform WindNeedle,Diver;
        public Text[] MapEstimates,RadioRows,LedgerRows;
        public Color Positive=new Color(.45f,.88f,.72f),Negative=new Color(1,.55f,.57f),Muted=new Color(.55f,.66f,.72f);
        [Header("仪表盘动态色彩；只影响显示，不改变区间和增益")]
        public Color GaugeBest=new Color(.97f,.78f,.47f),GaugeGood=new Color(.38f,.78f,.93f),GaugeNeutral=new Color(.38f,.47f,.54f);
        [Range(0,1)] public float RaftBackdropOpacity=.18f;
        void OnEnable() { EnsureFonts(); }
        public void EnsureFonts()
        {
            // The generated .fontsettings is an authoring alias, not an embedded font file.
            // Rehydrate it from the OS in edit/play mode; explicitly assigned custom font assets are kept.
            foreach(var t in GetComponentsInChildren<Text>(true))if(!t.font||t.font.name=="PlayerChinese")t.font=FontUtil.Get();
        }
    }
}
