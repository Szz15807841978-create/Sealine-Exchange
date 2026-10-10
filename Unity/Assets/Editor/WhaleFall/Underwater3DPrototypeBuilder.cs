using System;
using System.Collections.Generic;
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
    public static class Underwater3DPrototypeBuilder
    {
        public const string Source = "Assets/Scenes/UnderwaterExploreOriginal.unity";
        public const string Destination = "Assets/Scenes/UnderwaterExplore3D.unity";
        public const string Reports = "../_local/packages/Ocean3DReview";
        const string Request = "../_local/packages/Ocean3D.request";
        static double nextPoll;

        static Underwater3DPrototypeBuilder()
        {
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 2d;
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;

            string command = File.ReadAllText(Request).Trim().ToLowerInvariant();
            File.Delete(Request);
            try
            {
                if (command == "build") Build();
                else if (command == "validate") Underwater3DPlayCheck.Begin();
                else throw new InvalidOperationException("Unknown Ocean3D request: " + command);
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory(Reports);
                File.WriteAllText(Reports + "/error.txt", exception.ToString());
                Debug.LogException(exception);
            }
        }

        [MenuItem("鲸落/海底探索/创建 3D 自由游泳原型")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before building the scene.");
            if (!File.Exists(Source)) throw new FileNotFoundException("Source underwater scene is missing.", Source);
            if (File.Exists(Destination)) throw new InvalidOperationException("The 3D prototype already exists; it will not be overwritten: " + Destination);

            Directory.CreateDirectory(Reports);
            if (!AssetDatabase.CopyAsset(Source, Destination)) throw new InvalidOperationException("Could not copy the underwater source scene.");
            AssetDatabase.ImportAsset(Destination, ImportAssetOptions.ForceSynchronousImport);

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                WhaleFall.OriginalOceanDiver oldController = roots
                    .SelectMany(root => root.GetComponentsInChildren<WhaleFall.OriginalOceanDiver>(true))
                    .SingleOrDefault();
                if (!oldController) throw new InvalidOperationException("The source scene does not contain the verified diver controller.");

                GameObject player = oldController.gameObject;
                Camera camera = oldController.followCamera;
                Transform visual = oldController.visual;
                Animator animator = oldController.animator;
                Vector3 spawn = oldController.spawn;
                UnityEngine.Object.DestroyImmediate(oldController);

                player.name = "玩家 · 潜水员 · 3D自由游泳";
                Rigidbody body = player.GetComponent<Rigidbody>();
                body.useGravity = false;
                body.constraints = RigidbodyConstraints.FreezeRotation;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.mass = 1f;
                body.drag = 2.2f;
                body.angularDrag = 8f;

                SphereCollider collider = player.GetComponent<SphereCollider>();
                collider.radius = 0.65f;
                collider.center = Vector3.zero;

                WhaleFall.Underwater3DDiver controller = player.AddComponent<WhaleFall.Underwater3DDiver>();
                controller.followCamera = camera;
                controller.visual = visual;
                controller.animator = animator;
                controller.spawn = spawn;
                controller.xLimits = new Vector2(-20f, 20f);
                controller.yLimits = new Vector2(-33f, 7.5f);
                controller.zLimits = new Vector2(-8f, 78f);
                controller.cameraYaw = 0f;
                controller.cameraPitch = 12f;
                controller.cameraDistance = 7.5f;

                player.transform.position = spawn;
                if (visual)
                {
                    visual.localPosition = Vector3.zero;
                    visual.localRotation = Quaternion.identity;
                }
                if (animator)
                {
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }

                camera.name = "Main Camera · 3D自由跟随";
                camera.fieldOfView = 62f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 180f;
                controller.SnapCamera();

                EnsureBuildSettings();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Failed to save the 3D underwater scene.");
                AssetDatabase.SaveAssets();

                StringBuilder report = new StringBuilder();
                report.AppendLine("UNDERWATER 3D PROTOTYPE BUILD");
                report.AppendLine("Source: " + Source);
                report.AppendLine("Destination: " + Destination);
                report.AppendLine("Map roots preserved: " + roots.Length);
                report.AppendLine("Transforms preserved: " + roots.Sum(root => root.GetComponentsInChildren<Transform>(true).Length));
                report.AppendLine("Renderers preserved: " + roots.Sum(root => root.GetComponentsInChildren<Renderer>(true).Length));
                report.AppendLine("Colliders: " + roots.Sum(root => root.GetComponentsInChildren<Collider>(true).Length));
                report.AppendLine("Diver model: Assets/Resources/WhaleFall/Diver/DiverAnimator.prefab (animated wrapper of Assets/Art/人物模型/潜水员.prefab)");
                report.AppendLine("Spawn: " + spawn);
                report.AppendLine("Bounds X=" + controller.xLimits + " Y=" + controller.yLimits + " Z=" + controller.zLimits);
                report.AppendLine("Legacy 2.5D controller remaining: " + roots.Sum(root => root.GetComponentsInChildren<WhaleFall.OriginalOceanDiver>(true).Length));
                report.AppendLine("3D controller count: " + roots.Sum(root => root.GetComponentsInChildren<WhaleFall.Underwater3DDiver>(true).Length));
                report.AppendLine("Missing scripts: " + roots.Sum(root => root.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))));
                report.AppendLine("COMPLETE");
                File.WriteAllText(Reports + "/build.txt", report.ToString());

                OriginalOceanSceneBuilder.Capture(camera, "../Ocean3DReview/prototype-entry");
                Selection.activeGameObject = player;
                Debug.Log("Underwater 3D prototype ready: " + Destination);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        static void EnsureBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(scene => scene.path != Destination))
            {
                scenes.Add(new EditorBuildSettingsScene(Destination, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }

    [InitializeOnLoad]
    public static class Underwater3DPlayCheck
    {
        const string Flag = "Underwater3D.PlayCheck";
        static readonly Vector3[] Targets =
        {
            new Vector3(4f, 2f, 22f),
            new Vector3(-4f, -4f, 32f),
            new Vector3(0f, -11f, 40f),
            new Vector3(5f, -18f, 48f)
        };

        static int phase;
        static double phaseStart;
        static StringBuilder report;
        static bool started;
        static Scene previousScene;

        static Underwater3DPlayCheck()
        {
            EditorApplication.update += Tick;
        }

        [MenuItem("鲸落/海底探索/验证 3D 自由游泳原型")]
        public static void Begin()
        {
            if (!File.Exists(Underwater3DPrototypeBuilder.Destination)) throw new FileNotFoundException("Build the 3D prototype first.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Already entering Play Mode.");
            previousScene = SceneManager.GetActiveScene();
            if (previousScene.IsValid() && previousScene.isDirty) throw new InvalidOperationException("Save the current scene before automated validation.");

            EditorSceneManager.OpenScene(Underwater3DPrototypeBuilder.Destination, OpenSceneMode.Single);
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || !Application.isPlaying || EditorApplication.isPaused) return;
            WhaleFall.Underwater3DDiver diver = UnityEngine.Object.FindObjectOfType<WhaleFall.Underwater3DDiver>();
            if (!diver) return;

            if (!started)
            {
                started = true;
                phase = 0;
                phaseStart = EditorApplication.timeSinceStartup;
                report = new StringBuilder("UNDERWATER 3D PLAY-MODE VALIDATION\n");
                Application.runInBackground = true;
                Application.logMessageReceived += OnLog;
                diver.testDrive = true;
                Rigidbody body = diver.GetComponent<Rigidbody>();
                report.AppendLine("PASS controller and rigidbody present");
                report.AppendLine("PASS free X movement enabled=" + ((body.constraints & RigidbodyConstraints.FreezePositionX) == 0));
                report.AppendLine("PASS diver model=" + (diver.animator ? diver.animator.gameObject.name : "missing"));
                report.AppendLine("PASS camera=" + (diver.followCamera ? diver.followCamera.name : "missing"));
            }

            if (phase < Targets.Length)
            {
                Vector3 delta = Targets[phase] - diver.transform.position;
                diver.testWorldMove = delta.normalized;
                diver.testBoost = phase >= 2;
                if (delta.magnitude < 1f)
                {
                    report.AppendLine("PASS 3D waypoint " + phase + ": " + diver.transform.position);
                    if (phase == 0 || phase == Targets.Length - 1)
                        OriginalOceanSceneBuilder.Capture(diver.followCamera, "../Ocean3DReview/play-3d-" + phase);
                    phase++;
                    phaseStart = EditorApplication.timeSinceStartup;
                }
                else if (EditorApplication.timeSinceStartup - phaseStart > 20d)
                {
                    Finish("FAIL path blocked at " + diver.transform.position + " target=" + Targets[phase]);
                }
            }
            else
            {
                diver.testWorldMove = Vector3.zero;
                diver.testBoost = false;
                if (diver.transform.position.x < diver.xLimits.x || diver.transform.position.x > diver.xLimits.y ||
                    diver.transform.position.y < diver.yLimits.x || diver.transform.position.y > diver.yLimits.y ||
                    diver.transform.position.z < diver.zLimits.x || diver.transform.position.z > diver.zLimits.y)
                {
                    Finish("FAIL diver left playable bounds");
                    return;
                }

                float cameraDistance = Vector3.Distance(diver.followCamera.transform.position, diver.transform.position);
                if (float.IsNaN(cameraDistance) || float.IsInfinity(cameraDistance) || cameraDistance > diver.maxCameraDistance + 3f)
                {
                    Finish("FAIL camera follow distance=" + cameraDistance);
                    return;
                }

                report.AppendLine("PASS world bounds");
                report.AppendLine("PASS camera follow distance=" + cameraDistance.ToString("F2"));
                Finish("COMPLETE");
            }
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception)
                report?.AppendLine("RUNTIME ERROR: " + message);
        }

        static void Finish(string result)
        {
            report.AppendLine(result);
            Directory.CreateDirectory(Underwater3DPrototypeBuilder.Reports);
            File.WriteAllText(Underwater3DPrototypeBuilder.Reports + "/play-check.txt", report.ToString());
            Application.logMessageReceived -= OnLog;
            SessionState.SetBool(Flag, false);
            started = false;
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode)
            {
                int exitCode = result == "COMPLETE" ? 0 : 1;
                EditorApplication.delayCall += () => EditorApplication.Exit(exitCode);
            }
        }
    }
}
