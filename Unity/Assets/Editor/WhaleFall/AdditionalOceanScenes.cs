using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WhaleFallEditor
{
    [InitializeOnLoad]
    public static partial class AdditionalOceanScenes
    {
        public const string LowSource = "Assets/Ventuar/UnderwaterPack/Scenes/Demo.unity";
        public const string DeluxeRoot = "Assets/Underwater life deluxe";
        public const string LowScene = "Assets/Scenes/Underwater_LowPolyReef_3D.unity";
        public const string DeluxeScene = "Assets/Scenes/Underwater_LifeDeluxe_3D.unity";
        public const string Reports = "../_local/packages/AdditionalReview";
        const string Request = "../_local/packages/AdditionalOcean.request";
        static double nextPoll;
        static AdditionalOceanScenes() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 2;
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string command = File.ReadAllText(Request).Trim();
            File.Move(Request, Request + ".done-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(Reports);
            try
            {
                if (command == "probe") Probe();
                else Dispatch(command);
            }
            catch (Exception e) { File.WriteAllText(Reports + "/error.txt", e.ToString()); Debug.LogException(e); }
        }
        static partial void Dispatch(string command);

        [MenuItem("鲸落/海底探索/检查另外两套海洋资产")]
        public static void Probe()
        {
            Directory.CreateDirectory(Reports);
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(LowSource, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var report = new StringBuilder();
                foreach (var root in scene.GetRootGameObjects())
                {
                    report.AppendLine("ROOT " + root.name + " pos=" + root.transform.position + " transforms=" + root.GetComponentsInChildren<Transform>(true).Length);
                    foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                        report.AppendLine("CAMERA " + camera.name + " pos=" + camera.transform.position + " rot=" + camera.transform.eulerAngles);
                    foreach (var child in root.GetComponentsInChildren<Transform>(true).Where(t => t.name.ToLowerInvariant().Contains("ship") || t.name == "Terrain" || t.name == "Water"))
                        report.AppendLine("LANDMARK " + child.name + " pos=" + child.position + " bounds=" + BoundsOf(child.gameObject));
                }
                var renderers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).ToArray();
                report.AppendLine("RENDERERS=" + renderers.Length);
                foreach (var mat in renderers.SelectMany(r => r.sharedMaterials).Where(m => m).Distinct())
                    report.AppendLine("MAT " + AssetDatabase.GetAssetPath(mat) + " shader=" + mat.shader.name + " supported=" + mat.shader.isSupported);
                foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { DeluxeRoot + "/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    report.AppendLine("DELUXE " + path + " bounds=" + BoundsOf(prefab));
                }
                File.WriteAllText(Reports + "/probe.txt", report.ToString());
            }
            finally { EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(previous); }
        }

        public static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var result = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) result.Encapsulate(renderer.bounds);
            return result;
        }
    }
}
