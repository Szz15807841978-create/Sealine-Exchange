using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace WhaleFallEditor
{
    /// <summary>通过 Assets/Editor/DiveDiver.request 自动生成 3D 潜水员（动画控制器 + 预制体）。</summary>
    [InitializeOnLoad]
    internal static class DiveDiverSetupAuto
    {
        private const string RequestPath = "Assets/Editor/DiveDiver.request";
        static DiveDiverSetupAuto() { EditorApplication.delayCall += RunOnce; }
        private static void RunOnce()
        {
            if (!File.Exists(RequestPath)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            try
            {
                DiveDiverSetup.Build();
                AssetDatabase.DeleteAsset(RequestPath);
                AssetDatabase.Refresh();
            }
            catch (System.Exception e) { Debug.LogException(e); }
        }
    }

    /// <summary>
    /// 把 Tripo 的潜水员模型接成可用的人形角色：
    ///   1. 把模型导入类型设为 Human 并生成自己的 Avatar
    ///   2. 用 BasicMotions 的游泳剪辑建一个 AnimatorController
    ///   3. 生成一个带 Animator 的预制体放进 Resources，运行时直接加载
    /// </summary>
    public static class DiveDiverSetup
    {
        public const string DiverFbx = "Assets/TripoModels/cartoon_character_3d_model/cartoon_character_3d_model.fbx";
        private const string BmRoot = "Assets/ThirdParty/BasicMotions/Animations/Male/Movement/Swim/";
        private const string OutDir = "Assets/Resources/WhaleFall/Diver";
        public const string ControllerPath = OutDir + "/DiverSwim.controller";
        public const string PrefabPath = OutDir + "/DiverAnimator.prefab";

        [MenuItem("鲸落/搜打撤/生成 3D 潜水员")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(OutDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources/WhaleFall"))
                    AssetDatabase.CreateFolder("Assets/Resources", "WhaleFall");
                AssetDatabase.CreateFolder("Assets/Resources/WhaleFall", "Diver");
            }

            // ---------- 1. 模型导入设置：人形 + 自建 Avatar ----------
            var importer = AssetImporter.GetAtPath(DiverFbx) as ModelImporter;
            if (importer == null) { Debug.LogError("[鲸落] 找不到潜水员模型：" + DiverFbx); return; }
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.importAnimation = true;
            importer.SaveAndReimport();

            Avatar avatar = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(DiverFbx))
                if (o is Avatar a) { avatar = a; break; }
            // FBX 的根 GameObject 直接按路径取（子资产里有 Mesh/Material/Avatar/Clip）
            GameObject modelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(DiverFbx);
            if (avatar == null)
                Debug.LogError("[鲸落] 潜水员模型没有生成 Avatar —— 人形骨骼映射失败，重定向会用不了。");
            else
                Debug.Log("[鲸落] 潜水员 Avatar：" + avatar.name + "  isValid=" + avatar.isValid + "  isHuman=" + avatar.isHuman);

            // ---------- 2. 动画控制器 ----------
            var clips = new Dictionary<string, AnimationClip>
            {
                { "Idle",    FindClip(BmRoot + "HumanM@SwimIdle01.fbx") },
                { "Forward", FindClip(BmRoot + "SwimMovement/HumanM@Swim01_Forward.fbx") },
                { "Up",      FindClip(BmRoot + "SwimMovement/HumanM@Swim01_Up.fbx") },
                { "Down",    FindClip(BmRoot + "SwimMovement/HumanM@Swim01_Down.fbx") },
                { "Drown",   FindClip(BmRoot + "HumanM@SwimDrowned01.fbx") },
            };
            foreach (var kv in clips)
                if (kv.Value == null) Debug.LogError("[鲸落] 缺动画剪辑：" + kv.Key);
            if (clips["Idle"] == null || clips["Forward"] == null) return;

            AssetDatabase.DeleteAsset(ControllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("DirY", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);

            var sm = ctrl.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle"); sIdle.motion = clips["Idle"];
            var sFwd = sm.AddState("Forward"); sFwd.motion = clips["Forward"];
            var sForward = sFwd;
            var sUp = sm.AddState("Up"); sUp.motion = clips["Up"];
            var sDown = sm.AddState("Down"); sDown.motion = clips["Down"];
            var sDrown = sm.AddState("Drown"); sDrown.motion = clips["Drown"];
            sm.defaultState = sIdle;

            // 全部用 AnyState 转移，条件互斥，避免逐对连线
            AddAny(sm, sDrown, "Dead", true, null);
            AddAny(sm, sIdle, "Dead", false, new[] { Cond("Speed", AnimatorConditionMode.Less, SwimSpeedThreshold) });
            AddAny(sm, sUp, "Dead", false, new[]
            {
                Cond("Speed", AnimatorConditionMode.Greater, SwimSpeedThreshold),
                Cond("DirY", AnimatorConditionMode.Greater, 0.45f),
            });
            AddAny(sm, sDown, "Dead", false, new[]
            {
                Cond("Speed", AnimatorConditionMode.Greater, SwimSpeedThreshold),
                Cond("DirY", AnimatorConditionMode.Less, -0.45f),
            });
            AddAny(sm, sForward, "Dead", false, new[]
            {
                Cond("Speed", AnimatorConditionMode.Greater, SwimSpeedThreshold),
                Cond("DirY", AnimatorConditionMode.Less, 0.45f),
                Cond("DirY", AnimatorConditionMode.Greater, -0.45f),
            });

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            // ---------- 3. 预制体 ----------
            if (modelRoot == null) { Debug.LogError("[鲸落] 潜水员 FBX 里没有根 GameObject。"); return; }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelRoot);
            instance.name = "DiverAnimator";
            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = ctrl;
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();

            Debug.Log("[鲸落] 3D 潜水员已生成：" + PrefabPath);
        }

        public const float SwimSpeedThreshold = 0.15f;

        private struct C { public string p; public AnimatorConditionMode m; public float t; }
        private static C Cond(string p, AnimatorConditionMode m, float t) { return new C { p = p, m = m, t = t }; }

        private static void AddAny(AnimatorStateMachine sm, AnimatorState target, string boolParam, bool value, C[] conds)
        {
            var t = sm.AddAnyStateTransition(target);
            t.hasExitTime = false;
            t.exitTime = 0f;
            t.duration = 0.18f;
            t.canTransitionToSelf = false;
            t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, boolParam);
            if (conds != null)
                foreach (var c in conds) t.AddCondition(c.m, c.t, c.p);
        }

        private static AnimationClip FindClip(string fbxPath)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                var c = o as AnimationClip;
                if (c != null && !c.name.StartsWith("__preview__")) return c;
            }
            return null;
        }
    }
}
