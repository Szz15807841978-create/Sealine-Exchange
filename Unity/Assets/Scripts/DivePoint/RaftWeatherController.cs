using UnityEngine;
using UnityEngine.UI;

namespace Fishing
{
    public class RaftWeatherController : MonoBehaviour
    {
        [Header("晴雨切换：R 切换，1 晴天，2 雨天")]
        public bool Rainy;
        [Min(.1f)] public float TransitionSeconds=5f;
        [Range(0,1)] public float RainAmount;
        public RaftWeatherGraphic Sky, Clouds, Rain;
        public RawImage Ocean;
        public RectTransform Raft;
        public Text WeatherLabel;
        public Button SunnyButton, RainyButton;
        public Graphic[] Devices;
        [Header("木筏起伏改由 RaftBuoyancy 从 Crest 海面采样驱动时勾选：关掉下面原来的正弦假起伏，避免两种起伏叠加")]
        public bool DrivenByOcean;
        [Header("时段（白天/下午/傍晚/夜晚）。正式界面不暴露，由天空调试面板 F1 切换")]
        public RaftTimeOfDay TimeOfDay = RaftTimeOfDay.Day;
        private Vector2 raftOrigin;
        private Color[] deviceColors;
        private void Awake()
        {
            if(Raft) raftOrigin=Raft.anchoredPosition;
            deviceColors=new Color[Devices.Length];
            for(int i=0;i<Devices.Length;i++) if(Devices[i]) deviceColors[i]=Devices[i].color;
            if(SunnyButton) SunnyButton.onClick.AddListener(()=>SetRain(false));
            if(RainyButton) RainyButton.onClick.AddListener(()=>SetRain(true));
            // Fonts are resolved at runtime so the editor scene does not serialize an OS font instance.
            foreach(var text in GetComponentInParent<Canvas>().GetComponentsInChildren<Text>(true)) text.font=FontUtil.Get();
            Apply();
        }
        public void SetRain(bool value) { Rainy=value; }
        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.R)) Rainy=!Rainy;
            if(Input.GetKeyDown(KeyCode.Alpha1)) Rainy=false;
            if(Input.GetKeyDown(KeyCode.Alpha2)) Rainy=true;
            RainAmount=Mathf.MoveTowards(RainAmount,Rainy?1:0,Time.deltaTime/Mathf.Max(.1f,TransitionSeconds));
            Apply();
        }
        private void Apply()
        {
            float t=Time.time;
            // 时段色调：夜晚的雨幕/云层要跟着压暗，否则会像白天一样亮
            var look=RaftSkyPresets.Mix(TimeOfDay,RainAmount);
            foreach(var layer in new[]{Sky,Clouds,Rain}) if(layer) { layer.RainAmount=RainAmount; layer.MotionTime=t; layer.Tint=look.OverlayTint; layer.SetVerticesDirty(); }
            if(Ocean)
            {
                // Pan inside an oversized crop: no visible wrap seam, even for imperfect generated edges.
                Ocean.uvRect=new Rect(.06f+Mathf.Sin(t*.06f)*.035f,.06f+Mathf.Sin(t*.12f)*.018f,.88f,.88f);
                Ocean.color=Color.Lerp(Color.white,new Color(.48f,.59f,.68f),RainAmount);
            }
            if(Raft && !DrivenByOcean) Raft.anchoredPosition=raftOrigin+Vector2.up*Mathf.Sin(t*(1f+RainAmount*.6f))*(2+RainAmount*4);
            for(int i=0;i<Devices.Length;i++) if(Devices[i]) Devices[i].color=deviceColors[i]*Color.Lerp(Color.white,new Color(.67f,.72f,.78f),RainAmount);
            if(WeatherLabel) WeatherLabel.text=(Rainy?"雨天":"晴天")+" · "+RaftSkyPresets.TimeNames[Mathf.Clamp((int)TimeOfDay,0,RaftSkyPresets.TimeCount-1)]
                +" · 雨量 "+Mathf.RoundToInt(RainAmount*100)+"%";
        }
    }
}
