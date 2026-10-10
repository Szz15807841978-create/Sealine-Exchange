using Fishing;
using UnityEditor;
using UnityEngine;

namespace FishingEditor
{
    /// <summary>
    /// SkyDebugPanel 的 Inspector：把 8 个「时段 × 天气」做成按钮。
    ///
    /// 为什么需要它：编辑模式（不开 Play）下 uGUI 的 Button 点不动 —— Game 视图不处理输入事件。
    /// 所以"在编辑器里逐个看 8 套天空"必须走 Inspector。点一下即时生效（走 ApplyForPreview），
    /// 不用进 Play、不用记热键。
    /// </summary>
    [CustomEditor(typeof(SkyDebugPanel))]
    public sealed class SkyDebugPanelEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var panel = (SkyDebugPanel)target;

            EditorGUILayout.HelpBox(
                "编辑模式下请用下面的按钮切换（uGUI 按钮此刻点不动）。\n" +
                "运行时（Play / 打包）面板默认隐藏，按 F1 唤出。",
                MessageType.Info);

            if (!Application.isPlaying)
            {
                EditorGUILayout.Space();
                if (GUILayout.Button(panel.IsVisible ? "收起面板" : "展开面板", GUILayout.Height(24f)))
                {
                    if (!panel.IsVisible && !panel.gameObject.activeInHierarchy) panel.gameObject.SetActive(true);
                    panel.SetVisible(!panel.IsVisible);
                    SceneDirty();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("当前：" + CurrentLabel(panel), EditorStyles.boldLabel);
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                for (int t = 0; t < RaftSkyPresets.TimeCount; t++)
                {
                    var time = (RaftTimeOfDay)t;
                    bool cur = panel.Weather && panel.Weather.TimeOfDay == time && !panel.Weather.Rainy;
                    var prev = GUI.backgroundColor;
                    if (cur) GUI.backgroundColor = new Color(0.87f, 0.65f, 0.31f);
                    if (GUILayout.Button(RaftSkyPresets.TimeNames[t] + "\n晴", GUILayout.Height(40f)))
                    {
                        panel.Apply(t, false);
                        SceneDirty();
                    }
                    GUI.backgroundColor = prev;
                }
            }
            EditorGUILayout.Space(2f);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int t = 0; t < RaftSkyPresets.TimeCount; t++)
                {
                    var time = (RaftTimeOfDay)t;
                    bool cur = panel.Weather && panel.Weather.TimeOfDay == time && panel.Weather.Rainy;
                    var prev = GUI.backgroundColor;
                    if (cur) GUI.backgroundColor = new Color(0.36f, 0.57f, 0.60f);
                    if (GUILayout.Button(RaftSkyPresets.TimeNames[t] + "\n雨", GUILayout.Height(40f)))
                    {
                        panel.Apply(t, true);
                        SceneDirty();
                    }
                    GUI.backgroundColor = prev;
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("预设数值（RaftSkyPresets）", EditorStyles.boldLabel);
            for (int t = 0; t < RaftSkyPresets.TimeCount; t++)
            {
                var sunny = RaftSkyPresets.Get((RaftTimeOfDay)t, false);
                EditorGUILayout.LabelField(
                    string.Format("  {0}｜日照 {1:0.00}　环境光 {2:0.00}　雾 {3:0.0000}　天空曝光 {4:0.00}",
                        RaftSkyPresets.TimeNames[t], sunny.SunIntensity, sunny.AmbientIntensity,
                        sunny.FogDensity, sunny.SkyExposure));
            }

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        private static string CurrentLabel(SkyDebugPanel panel)
        {
            if (!panel.Weather) return "（未找到 RaftWeatherController）";
            return RaftSkyPresets.Label(panel.Weather.TimeOfDay, panel.Weather.Rainy);
        }

        private void SceneDirty()
        {
            var panel = (SkyDebugPanel)target;
            EditorUtility.SetDirty(panel);
            if (panel.Weather) EditorUtility.SetDirty(panel.Weather);
            if (panel.Environment) EditorUtility.SetDirty(panel.Environment);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
            SceneView.RepaintAll();
            Repaint();
        }
    }
}
