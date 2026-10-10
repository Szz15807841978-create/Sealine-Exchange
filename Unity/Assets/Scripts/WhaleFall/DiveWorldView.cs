using System.Collections.Generic;
using UnityEngine;

namespace WhaleFall
{
    /// <summary>
    /// 搜打撤表现层：用《鲸落》现有美术资产（Assets/Resources/WhaleFall）渲染世界。
    /// 规则与碰撞仍在 DiveSim，这里只管「看得见」。
    /// 地形与潜水员都是 3D；生物/敌人/掉落物仍是 2D 精灵。
    /// </summary>
    public class DiveWorldView
    {
        // ---- 层级（z 越小越靠近相机）----
        private const float ZBackdrop = 8f;
        private const float ZCreature = 0f;
        private const float ZEnemy = 0f;
        private const float ZFood = -1f;
        private const float ZPlayer = -2f;
        private const float ZProj = -3f;

        private static Mesh _quad;
        private static Texture2D _white;

        private readonly Dictionary<Texture2D, Material> _mats = new Dictionary<Texture2D, Material>();
        private Material _fallbackMat;
        private Material _projMat;

        private readonly Transform _root;
        private readonly Transform _backdropRoot;
        private readonly DiveTerrain3DView _terrain3D;

        private GameObject _playerGo;
        private GameObject _backdropGo;

        private readonly List<GameObject> _enemyPool = new List<GameObject>();
        private readonly List<GameObject> _resourcePool = new List<GameObject>();
        private readonly List<GameObject> _projectilePool = new List<GameObject>();
        private readonly List<GameObject> _foodPool = new List<GameObject>();
        private int _enemyCursor, _resourceCursor, _projectileCursor, _foodCursor;

        // ---- 2D 潜水员贴图（3D 不可用时的回退）----
        private Texture2D _diverSwim, _diverDash, _diverSpear, _diverPick, _diverGather, _diverLoad;

        // ---- 3D 潜水员（人形骨骼 + BasicMotions 游泳动作）----
        private const string DiverPrefabPath = "WhaleFall/Diver/DiverAnimator";
        private const float DiverHeight = 5.0f;   // 统一缩放到目标身高（米）
        private const float DiverZ = -1.5f;       // 画在地形（z≈0）前面
        private const float DiverYaw = 90f;       // 模型朝 +Z 建，绕 Y 转 90° 变成侧视
        private static readonly int PidSpeed = Animator.StringToHash("Speed");
        private static readonly int PidDirY = Animator.StringToHash("DirY");
        private static readonly int PidDead = Animator.StringToHash("Dead");
        private Transform _diverRoot;   // 只管位置与朝向（左右翻转）
        private Transform _diverModel;  // 模型本体，负责缩放与居中
        private Animator _diverAnim;
        private float _diverFace = 1f;

        // ---- 贴图 ----
        private readonly Texture2D[] _backdrop = new Texture2D[4];
        private readonly Dictionary<string, Texture2D> _creature = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, Texture2D> _enemy = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, Texture2D> _enemyWindup = new Dictionary<string, Texture2D>();
        private Texture2D _boss, _bossWindup;
        private readonly Dictionary<string, Texture2D> _food = new Dictionary<string, Texture2D>();

        private int _builtSeed = int.MinValue;
        private int _shownLayer = -1;

        public DiveWorldView()
        {
            _root = new GameObject("搜打撤世界").transform;
            _backdropRoot = new GameObject("背景层").transform; _backdropRoot.SetParent(_root, false);
            _terrain3D = new DiveTerrain3DView(_root);

            LoadTextures();

            // 背景（跟随相机，按深度层切图）
            _backdropGo = Quad("背景", MatFor(_backdrop[0]), 140f, 80f, ZBackdrop);
            _backdropGo.transform.SetParent(_backdropRoot, false);

            // 潜水员：优先用 3D 模型（人形 + BasicMotions 游泳动作），拿不到才退回 2D 精灵
            Build3DDiver();
            if (_diverRoot == null)
            {
                _playerGo = Quad("潜水员", MatFor(_diverSwim), 5f, 5f, ZPlayer);
                _playerGo.transform.SetParent(_root, false);
            }

            _projMat = Solid(Hex("#F2E7CB"));
        }

        // =====================================================================
        //  资源加载
        // =====================================================================

        private void LoadTextures()
        {
            _diverSwim = Tex("Diver/swim");
            _diverDash = Tex("Diver/dash");
            _diverSpear = Tex("Diver/spear");
            _diverPick = Tex("Diver/pick");
            _diverGather = Tex("Diver/gather");
            _diverLoad = Tex("Diver/load");

            _backdrop[0] = Tex("Backdrop/shallow");
            _backdrop[1] = Tex("Backdrop/mid");
            _backdrop[2] = Tex("Backdrop/deep");
            _backdrop[3] = Tex("Backdrop/abyss");

            string[] spc = { "brach", "ortho", "mysid", "ostra", "placo", "amiif", "coela", "jelly", "leeds" };
            for (int i = 0; i < spc.Length; i++) _creature[spc[i]] = Tex("Creatures/" + spc[i]);

            _enemy["ammo"] = Tex("Enemies/ammo");
            _enemy["dunk"] = Tex("Enemies/dunk");
            _enemyWindup["ammo"] = Tex("Enemies/ammo_windup");
            _enemyWindup["dunk"] = Tex("Enemies/dunk_windup");
            _boss = Tex("Boss/guardian");
            _bossWindup = Tex("Boss/guardian_windup");

            string[] icons = {
                "ITM_FOOD_FISH_S_01", "ITM_FOOD_FISH_M_01", "ITM_FOOD_FISH_L_01",
                "ITM_FOOD_SHELL_S_01", "ITM_FOOD_SHELL_M_01", "ITM_FOOD_SHRIMP_S_01",
                "ITM_FOOD_JELLY_M_01", "ITM_MAT_ORE_01", "ITM_MAT_ANCIENT_01",
            };
            string[] keys = {
                "FOOD_FISH_S", "FOOD_FISH_M", "FOOD_FISH_L",
                "FOOD_SHELL_S", "FOOD_SHELL_M", "FOOD_SHRIMP_S",
                "FOOD_JELLY_M", "MAT_ORE", "MAT_ANCIENT",
            };
            for (int i = 0; i < icons.Length; i++) _food[keys[i]] = Tex("Food/" + icons[i]);
        }

        private static Texture2D Tex(string path)
        {
            var t = Resources.Load<Texture2D>("WhaleFall/" + path);
            if (t == null) Debug.LogWarning("[鲸落] 缺贴图：Resources/WhaleFall/" + path);
            return t;
        }

        // =====================================================================
        //  图元与材质
        // =====================================================================

        private static Mesh QuadMesh()
        {
            if (_quad != null) return _quad;
            var m = new Mesh();
            // 1x1 四边形，法线朝 -Z（相机在 z=-10 朝 +Z 看）
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
            };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            // 顶点色必须给白：着色器会乘顶点色，缺省值不一定是白
            m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            m.RecalculateNormals();
            _quad = m;
            return _quad;
        }

        private static Texture2D White()
        {
            if (_white != null) return _white;
            _white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
            return _white;
        }

        private Material MatFor(Texture2D tex)
        {
            if (tex == null) return _fallbackMat ?? (_fallbackMat = Solid(Hex("#DDA64F")));
            Material m;
            if (_mats.TryGetValue(tex, out m) && m != null) return m;
            m = MakeMat();
            m.mainTexture = tex;
            if (m.HasProperty("_Color")) m.color = Color.white;
            _mats[tex] = m;
            return m;
        }

        private Material Solid(Color c)
        {
            var m = MakeMat();
            m.mainTexture = White();
            if (m.HasProperty("_Color")) m.color = c;
            return m;
        }

        /// <summary>
        /// 2D 精灵统一用项目自带的 WhaleFall/Sprite（直通 alpha + _Color）。
        /// 不能依赖内置 shader：Sprites/Default 是预乘 alpha，会让精灵周围出现浅色方块；
        /// Unlit/Transparent 又没有 _Color 属性（纯色块会整片变白）。两个坑都踩过。
        /// </summary>
        private static Material MakeMat()
        {
            var sh = Shader.Find("WhaleFall/Sprite");
            if (sh == null) sh = Shader.Find("Unlit/Transparent");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            return new Material(sh);
        }

        private static GameObject Quad(string name, Material mat, float w, float h, float z)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = QuadMesh();
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.transform.localScale = new Vector3(w, h, 1f);
            go.transform.position = new Vector3(0f, 0f, z);
            return go;
        }

        private static Color Hex(string v)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(v, out c) ? c : Color.magenta;
        }

        // =====================================================================
        //  地形 / 背景
        // =====================================================================

        /// <summary>地形由 DiveTerrain3DView 装配（场景里手工摆的 3D 模块）。</summary>
        public void RebuildTerrain(DiveState s)
        {
            if (s == null || s.World == null) return;
            if (s.Seed == _builtSeed) return;
            _builtSeed = s.Seed;
            _terrain3D.Rebuild(s.World, s.Seed);
        }

        private void UpdateBackdrop(DiveState s)
        {
            var p = s.Player.pos;
            int layer = Mathf.Clamp((int)DiveSim.LayerAtDepth(DiveSim.DepthM(s)), 0, 3);
            if (layer != _shownLayer)
            {
                _shownLayer = layer;
                _backdropGo.GetComponent<MeshRenderer>().sharedMaterial = MatFor(_backdrop[layer]);
            }
            _backdropGo.transform.position = new Vector3(p.x, p.y, ZBackdrop);
        }

        // =====================================================================
        //  实体
        // =====================================================================

        private Texture2D DiverTex(DiveState s)
        {
            if (s.Player.dashing) return _diverDash ?? _diverSwim;
            if (s.MiningIndex >= 0) return _diverPick ?? _diverSwim;
            if (s.GatheringIndex >= 0) return _diverGather ?? _diverSwim;
            if (s.Mode == ControlMode.Weapon) return _diverSpear ?? _diverSwim;
            if (s.Bag.UsedSlots() >= DiveConfig.BagSlots - 1) return _diverLoad ?? _diverSwim;
            return _diverSwim ?? _diverSpear;
        }

        public void Refresh(DiveState s)
        {
            if (s == null || s.World == null) return;
            var p = s.Player;
            UpdateBackdrop(s);

            // ---- 潜水员 ----
            Vector2 vel = p.vel;
            if (Mathf.Abs(vel.x) > 0.15f) _diverFace = vel.x >= 0f ? 1f : -1f;
            else if (Mathf.Abs(p.aim) > 0.01f) _diverFace = Mathf.Cos(p.aim) >= 0f ? 1f : -1f;

            if (_diverRoot != null)
            {
                // 角色自己身上的碰撞箱不算地形，否则会把自己挡住
                s.World.SelfRoot = _diverRoot;
                _diverRoot.position = new Vector3(p.pos.x, p.pos.y, DiverZ);
                _diverRoot.rotation = Quaternion.Euler(0f, DiverYaw * _diverFace, 0f);
                if (_diverAnim != null)
                {
                    float spd = vel.magnitude;
                    _diverAnim.SetFloat(PidSpeed, spd);
                    _diverAnim.SetFloat(PidDirY, spd > 0.01f ? vel.y / spd : 0f);
                    _diverAnim.SetBool(PidDead, s.Over);
                }
            }
            else if (_playerGo != null)
            {
                var mr0 = _playerGo.GetComponent<MeshRenderer>();
                var want = DiverTex(s);
                if (mr0.sharedMaterial == null || mr0.sharedMaterial.mainTexture != want) mr0.sharedMaterial = MatFor(want);
                _playerGo.transform.position = new Vector3(p.pos.x, p.pos.y, ZPlayer);
                _playerGo.transform.localScale = new Vector3(5f * _diverFace, 5f, 1f);
            }

            // 每帧先把所有池槽关掉，用到哪个才开哪个。
            // 之前是「关掉旧的、按总数全开」，但渲染循环只处理存活实体，
            // 中间的空槽就带着创建时的纯白 1x1 材质留在场上（表现为莫名其妙的白色方块）。
            Sync(_enemyPool, s.World.Enemies.Count);
            Sync(_resourcePool, s.World.Resources.Count);
            Sync(_projectilePool, s.Projectiles.Count);
            Sync(_foodPool, s.FreeFoods.Count);

            int ei = 0;
            for (int i = 0; i < s.World.Enemies.Count; i++)
            {
                var e = s.World.Enemies[i];
                if (e.dead) continue;
                var go = _enemyPool[ei++];
                go.SetActive(true);
                var def = DiveDefs.EnemyOf(e.key);
                bool winding = e.state == EnemyState.Attack;
                Texture2D tex = def.IsBoss
                    ? (winding ? _bossWindup : _boss)
                    : (winding ? Get(_enemyWindup, e.key) : Get(_enemy, e.key));
                var mr = go.GetComponent<MeshRenderer>();
                var m = MatFor(tex);
                if (mr.sharedMaterial != m) mr.sharedMaterial = m;
                float size = def.IsBoss ? 6.4f : (e.key == "dunk" ? 5.4f : 3.2f);
                float f = e.dir >= 0f ? -1f : 1f;   // 素材朝右，向左游时翻转
                go.transform.localScale = new Vector3(size * f, size, 1f);
                go.transform.position = new Vector3(e.pos.x, e.pos.y, ZEnemy);
            }
            _enemyCursor = ei;

            int ri = 0;
            for (int i = 0; i < s.World.Resources.Count; i++)
            {
                var r = s.World.Resources[i];
                if (!r.alive) continue;
                var go = _resourcePool[ri++];
                go.SetActive(true);
                var m = MatFor(Get(_creature, r.speciesKey));
                var mr = go.GetComponent<MeshRenderer>();
                if (mr.sharedMaterial != m) mr.sharedMaterial = m;
                float size = SizeOf(r.speciesKey);
                float f = r.dir >= 0f ? -1f : 1f;
                go.transform.localScale = new Vector3(size * f, size, 1f);
                go.transform.position = new Vector3(r.pos.x, r.pos.y, ZCreature);
            }
            _resourceCursor = ri;

            int pi = 0;
            for (int i = 0; i < s.Projectiles.Count; i++)
            {
                var pr = s.Projectiles[i];
                if (pr.dead) continue;
                var go = _projectilePool[pi++];
                go.SetActive(true);
                var mr = go.GetComponent<MeshRenderer>();
                if (mr.sharedMaterial != _projMat) mr.sharedMaterial = _projMat;
                go.transform.position = new Vector3(pr.pos.x, pr.pos.y, ZProj);
                go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(pr.dir.y, pr.dir.x) * Mathf.Rad2Deg);
                go.transform.localScale = new Vector3(1.4f, 0.32f, 1f);
            }
            _projectileCursor = pi;

            int fi = 0;
            for (int i = 0; i < s.FreeFoods.Count; i++)
            {
                var f = s.FreeFoods[i];
                if (f.dead) continue;
                var go = _foodPool[fi++];
                go.SetActive(true);
                var m = MatFor(Get(_food, f.foodKey));
                var mr = go.GetComponent<MeshRenderer>();
                if (mr.sharedMaterial != m) mr.sharedMaterial = m;
                go.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
                go.transform.position = new Vector3(f.pos.x, f.pos.y, ZFood);
            }
            _foodCursor = fi;
        }

        private static float SizeOf(string speciesKey)
        {
            var sp = DiveDefs.SpeciesOf(speciesKey);
            switch (sp.Size)
            {
                case SizeClass.Small: return 1.8f;
                case SizeClass.Large: return 5.6f;
                default: return 3.0f;
            }
        }

        private static Texture2D Get(Dictionary<string, Texture2D> map, string key)
        {
            Texture2D t;
            return (key != null && map.TryGetValue(key, out t)) ? t : null;
        }

        // =====================================================================
        //  卡通化
        // =====================================================================

        private static readonly Dictionary<Material, Material> _toonCache = new Dictionary<Material, Material>();

        /// <summary>
        /// 把一棵渲染树上的材质整体换成 WhaleFall/Toon（明暗量化 + 轮廓光）。
        /// 内置 Standard 是物理光照，落在低多边形写实模型上会出现大片死黑与生硬高光。
        /// 按源材质缓存，避免每个 Renderer 生成一份新材质。
        /// forceBase 不为空时忽略材质自带的颜色与贴图，统一套这个色调 ——
        /// 地形由很多块模型拼成，各自贴图明暗不一，不统一就会花。
        /// </summary>
        public static void ApplyToon(GameObject go, Color? forceBase = null)
        {
            if (go == null) return;
            var sh = Shader.Find("WhaleFall/Toon");
            if (sh == null) { Debug.LogWarning("[鲸落] 找不到 WhaleFall/Toon 着色器，跳过卡通化"); return; }
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    if (forceBase.HasValue && src.shader == sh) continue;   // 已经是统一材质
                    Material dst;
                    if (!_toonCache.TryGetValue(src, out dst) || dst == null)
                    {
                        dst = new Material(sh);
                        dst.name = src.name + "_Toon";
                        if (forceBase.HasValue)
                        {
                            dst.SetColor("_BaseColor", forceBase.Value);
                        }
                        else
                        {
                            if (src.HasProperty("_MainTex")) dst.mainTexture = src.mainTexture;
                            if (src.HasProperty("_BaseColor")) dst.SetColor("_BaseColor", src.GetColor("_BaseColor"));
                            else if (src.HasProperty("_Color")) dst.SetColor("_BaseColor", src.color);
                            else dst.SetColor("_BaseColor", Color.white);
                        }
                        _toonCache[src] = dst;
                    }
                    mats[i] = dst;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        // =====================================================================
        //  3D 潜水员
        // =====================================================================

        /// <summary>加载并摆放 3D 潜水员：统一身高，并把包围盒中心对齐到根节点原点。</summary>
        private void Build3DDiver()
        {
            var prefab = Resources.Load<GameObject>(DiverPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[鲸落] 没有 3D 潜水员预制体，退回 2D 精灵：" + DiverPrefabPath);
                return;
            }

            var rootGo = new GameObject("潜水员3D");
            _diverRoot = rootGo.transform;
            _diverRoot.SetParent(_root, false);

            var inst = Object.Instantiate(prefab, _diverRoot, false);
            inst.name = "DiverModel";
            _diverModel = inst.transform;
            _diverAnim = inst.GetComponentInChildren<Animator>();
            if (_diverAnim != null) _diverAnim.applyRootMotion = false;
            ApplyToon(inst);
            // 位移是代码直接写 transform 的：模型上留着力刚体会和物理去穿透打架，
            // 表现为「黏在碰撞箱上」动不了。
            foreach (var rb in inst.GetComponentsInChildren<Rigidbody>(true)) Object.Destroy(rb);

            _diverModel.localPosition = Vector3.zero;
            _diverModel.localRotation = Quaternion.identity;
            _diverModel.localScale = Vector3.one;

            // 先让 Animator 求值一次，否则 SkinnedMeshRenderer.bounds 还是 T-pose 的旧值
            if (_diverAnim != null) { _diverAnim.Update(0f); Physics.SyncTransforms(); }

            Bounds b0 = RenderBounds(inst);
            float scale = DiverHeight / Mathf.Max(0.001f, b0.size.y);
            _diverModel.localScale = Vector3.one * scale;

            if (_diverAnim != null) { _diverAnim.Update(0f); Physics.SyncTransforms(); }
            // 玩家坐标代表「身体中心」，而人形模型的轴心通常在脚底 —— 平移到中心
            Bounds b1 = RenderBounds(inst);
            _diverModel.localPosition = -_diverRoot.InverseTransformPoint(b1.center);

            Debug.Log("[鲸落] 3D 潜水员就位：原始高 " + b0.size.y.ToString("F3") + " m，缩放 "
                + scale.ToString("F3") + "；Avatar="
                + (_diverAnim != null && _diverAnim.avatar != null ? _diverAnim.avatar.name : "无"));
        }

        /// <summary>渲染包围盒（世界空间）。用 Renderer.bounds，Animator 求值后才是当前姿势的真实尺寸。</summary>
        private static Bounds RenderBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            bool has = false;
            Bounds total = default;
            foreach (var r in rs)
            {
                if (r == null) continue;
                if (!has) { total = r.bounds; has = true; } else total.Encapsulate(r.bounds);
            }
            return has ? total : new Bounds(Vector3.zero, Vector3.one);
        }

        // =====================================================================
        //  对象池
        // =====================================================================

        private void Sync(List<GameObject> pool, int need)
        {
            while (pool.Count < need)
            {
                var go = Quad("pool", Solid(Color.white), 1f, 1f, ZCreature);
                go.transform.SetParent(_root, false);
                pool.Add(go);
            }
            // 全部关掉；调用方渲染到哪个才 SetActive(true)
            for (int i = 0; i < pool.Count; i++) pool[i].SetActive(false);
        }
    }
}
