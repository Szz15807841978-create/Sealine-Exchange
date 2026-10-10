using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WhaleFallEditor
{
    [InitializeOnLoad]
    internal static class UnderwaterExploreAutoBuild
    {
        const string Request = "Assets/Editor/UnderwaterExplore.request";
        static UnderwaterExploreAutoBuild() { EditorApplication.delayCall += Run; }
        static void Run()
        {
            if (!File.Exists(Request)) return;
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += Run;
                return;
            }
            try
            {
                UnderwaterExploreSceneBuilder.Build();
                AssetDatabase.DeleteAsset(Request);
            }
            catch (System.Exception e) { Debug.LogException(e); }
        }
    }

    public static class UnderwaterExploreSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/UnderwaterExplore.unity";
        const string ModelRoot = "Assets/ThirdParty/UnderwaterExplore/Models/";
        const string Output = "Assets/WhaleFall/UnderwaterExplore";
        static Transform environment;
        static Material sand, rock, rockDark, coralA, coralB, plant, wreck, fish, water;

        [MenuItem("鲸落/海底探索/生成下潜地图")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureFolder("Assets/WhaleFall");
            EnsureFolder(Output);
            BuildMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .012f;
            RenderSettings.fogColor = new Color(.025f, .17f, .24f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.20f,.48f,.55f);
            RenderSettings.ambientEquatorColor = new Color(.07f,.25f,.31f);
            RenderSettings.ambientGroundColor = new Color(.02f,.07f,.11f);

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 12f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f,.25f,.32f);
            cameraGo.transform.position = new Vector3(-15,-10,-45);

            var key = new GameObject("水面主光", typeof(Light));
            key.transform.rotation = Quaternion.Euler(24,-32,0);
            var keyLight = key.GetComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(.58f,.88f,1f);
            keyLight.intensity = 1.15f;
            keyLight.shadows = LightShadows.Soft;
            var fill = new GameObject("深水补光", typeof(Light));
            fill.transform.position = new Vector3(5,-55,-8);
            var fillLight = fill.GetComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.range = 45;
            fillLight.intensity = 1.7f;
            fillLight.color = new Color(.08f,.48f,.58f);

            environment = new GameObject("海底地图（可编辑）").transform;
            BuildBackdrop();
            BuildBoundaries();
            BuildShallowReef();
            BuildKelpCanyon();
            BuildWreckBay();
            BuildParticles();

            var player = new GameObject("玩家_潜水员", typeof(Rigidbody), typeof(SphereCollider));
            player.transform.position = new Vector3(-15,-7,0);
            var sphere = player.GetComponent<SphereCollider>();
            sphere.radius = 1.15f;
            var controller = player.AddComponent<WhaleFall.UnderwaterExploreController>();
            controller.followCamera = camera;
            var diverAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/WhaleFall/Diver/DiverAnimator.prefab");
            if (diverAsset != null)
            {
                var diver = (GameObject)PrefabUtility.InstantiatePrefab(diverAsset, player.transform);
                diver.name = "潜水员模型";
                Fit(diver, 3.2f);
                Center(diver);
                controller.visual = diver.transform;
                controller.animator = diver.GetComponentInChildren<Animator>();
            }
            else
            {
                var fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                fallback.name = "临时潜水员";
                fallback.transform.SetParent(player.transform, false);
                fallback.transform.localScale = new Vector3(.8f,1.4f,.6f);
                fallback.GetComponent<Renderer>().sharedMaterial = coralB;
                Object.DestroyImmediate(fallback.GetComponent<Collider>());
                controller.visual = fallback.transform;
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            RenderPreview(camera);
            AssetDatabase.Refresh();
            Debug.Log("[鲸落] 海底探索场景已生成：" + ScenePath + "；预览：" + Output + "/UnderwaterExplorePreview.png");
        }

        static void BuildBackdrop()
        {
            Cube("远景海水", new Vector3(0,-42,18), new Vector3(116,94,2), water, false);
            for (int i=0;i<10;i++)
            {
                float x = -48 + i*11;
                AddModel("远景岩柱_"+i, "Cliff0"+(i%4+1)+".fbx", new Vector3(x,-73+(i%3)*7,11), new Vector3(8,18+(i%3)*5,6), rockDark, new Vector3(0,i*37,0), false);
            }
        }

        static void BuildBoundaries()
        {
            Cube("左侧边界", new Vector3(-55,-42,0), new Vector3(12,96,12), rock, true);
            Cube("右侧边界", new Vector3(55,-42,0), new Vector3(12,96,12), rock, true);
            Cube("海床边界", new Vector3(0,-90,0), new Vector3(116,10,12), sand, true);
            Cube("水面边界", new Vector3(0,8,0), new Vector3(116,8,12), water, true);
            for (int i=0;i<11;i++)
            {
                float x=-47+i*9.5f;
                AddModel("海床_"+i,"LargeGround0"+(i%2+1)+".fbx",new Vector3(x,-84.5f,(i%3-1)*1.8f),new Vector3(12,5,8),sand,new Vector3(0,i*31,0),false);
            }
        }

        static void BuildShallowReef()
        {
            var root = NewGroup("01_浅海珊瑚礁");
            AddRock(root,"入口左礁",new Vector3(-43,-13,0),new Vector3(13,16,8),"LargeTerrain01.fbx");
            AddRock(root,"入口右礁",new Vector3(31,-15,0),new Vector3(17,13,8),"LargeTerrain04.fbx");
            AddRock(root,"中间礁台",new Vector3(5,-22,1),new Vector3(13,7,7),"LargeTerrain07.fbx");
            AddDecorations(root, new [] { new Vector3(-31,-17,-2),new Vector3(-23,-21,1),new Vector3(18,-12,-1),new Vector3(37,-24,2),new Vector3(0,-27,-1) }, false);
            AddFishSchool(root,"浅海鱼群","Clownfish.fbx",new Vector3(-6,-13,-1),5,6,.35f);
        }

        static void BuildKelpCanyon()
        {
            var root = NewGroup("02_海草峡谷");
            AddRock(root,"左峡壁上",new Vector3(-39,-36,1),new Vector3(18,14,9),"LargeTerrain02.fbx");
            AddRock(root,"右峡壁上",new Vector3(38,-34,0),new Vector3(17,16,9),"LargeTerrain06.fbx");
            AddRock(root,"左峡壁下",new Vector3(-43,-52,0),new Vector3(15,13,9),"Cliff03.fbx");
            AddRock(root,"右峡壁下",new Vector3(41,-53,1),new Vector3(15,14,9),"Cliff02.fbx");
            for(int i=0;i<16;i++)
            {
                float x=-32+i*4.3f;
                float y=-55+Mathf.Sin(i*1.7f)*2;
                AddModel("海草_"+i,"Kelp0"+(i%3+1)+".fbx",new Vector3(x,y,2+(i%3)),new Vector3(1.7f,5+(i%4),1.7f),plant,new Vector3(0,i*43,0),false,root);
            }
            AddDecorations(root, new [] { new Vector3(-28,-34,-2),new Vector3(26,-41,0),new Vector3(-17,-49,1),new Vector3(14,-54,-1) }, true);
            AddFishSchool(root,"峡谷鱼群","Blue Tang.fbx",new Vector3(4,-39,-1),7,8,.22f);
        }

        static void BuildWreckBay()
        {
            var root=NewGroup("03_深水沉船湾");
            AddRock(root,"深水左壁",new Vector3(-42,-69,1),new Vector3(15,19,9),"LargeTerrain08.fbx");
            AddRock(root,"深水右壁",new Vector3(43,-69,0),new Vector3(14,20,9),"LargeTerrain03.fbx");
            AddModel("沉船", "SunkenShipFront.fbx", new Vector3(9,-77,1), new Vector3(24,13,9), wreck, new Vector3(0,-12,-5), false, root);
            AddModel("船锚", "Anchor.fbx", new Vector3(-13,-79,-1), new Vector3(4,6,3), rockDark, new Vector3(0,20,-20), false, root);
            AddModel("宝箱", "Treasure Chest.fbx", new Vector3(25,-81,-2), new Vector3(3.6f,2.4f,2.4f), coralA, new Vector3(0,-30,0), false, root);
            AddDecorations(root,new [] {new Vector3(-29,-74,1),new Vector3(-20,-81,-1),new Vector3(32,-72,2),new Vector3(37,-81,0)},false);
            AddFishSchool(root,"深水鱼群","Yellow Tang.fbx",new Vector3(-4,-68,-1),8,7,.18f);
        }

        static void AddRock(Transform parent,string name,Vector3 pos,Vector3 size,string model)
        {
            AddModel(name,model,pos,size,rock,new Vector3(0,pos.x*3,0),false,parent);
            var proxy=Cube(name+"_碰撞",pos,new Vector3(size.x*.78f,size.y*.72f,8),rockDark,true,parent);
            proxy.GetComponent<Renderer>().enabled=false;
        }

        static void AddDecorations(Transform parent, Vector3[] points, bool kelp)
        {
            for(int i=0;i<points.Length;i++)
            {
                AddModel("珊瑚_"+i,"Coral"+(i*3%16+1).ToString("00")+".fbx",points[i],new Vector3(2.8f,3.5f,2.8f),i%2==0?coralA:coralB,new Vector3(0,i*71,0),false,parent);
                if(kelp) AddModel("伴生海草_"+i,"Grass0"+(i%2+1)+".fbx",points[i]+new Vector3(2,-.4f,1),new Vector3(2,3,2),plant,Vector3.zero,false,parent);
            }
        }

        static void AddFishSchool(Transform parent,string name,string model,Vector3 center,int count,float radius,float speed)
        {
            var group=NewGroup(name,parent);
            for(int i=0;i<count;i++)
            {
                var go=AddModel("鱼_"+i,model,center,new Vector3(1.3f,.75f,.5f),fish,Vector3.zero,false,group);
                if(!go) continue;
                var swim=go.AddComponent<WhaleFall.UnderwaterAmbientFish>();
                swim.center=center+new Vector3((i-count/2)*.65f,(i%3)*.45f,(i%2)*.5f);
                swim.radius=radius*(.65f+(i%3)*.12f);
                swim.speed=speed*(.8f+(i%4)*.09f);
                swim.phase=i*.83f;
            }
        }

        static void BuildParticles()
        {
            var go=new GameObject("漂浮微粒",typeof(ParticleSystem));
            go.transform.SetParent(environment,false);
            go.transform.position=new Vector3(0,-42,4);
            var ps=go.GetComponent<ParticleSystem>();
            var main=ps.main; main.startLifetime=18; main.startSpeed=.35f; main.startSize=.12f; main.maxParticles=280; main.startColor=new Color(.55f,.9f,1,.35f);
            var emission=ps.emission; emission.rateOverTime=12;
            var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Box; shape.scale=new Vector3(95,82,7);
            var velocity=ps.velocityOverLifetime; velocity.enabled=true; velocity.y=.18f;
            var renderer=go.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        }

        static GameObject AddModel(string name,string file,Vector3 pos,Vector3 targetSize,Material material,Vector3 euler,bool collider,Transform parent=null)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(ModelRoot+file);
            if(!asset) { Debug.LogWarning("缺模型："+file); return null; }
            var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent?parent:environment);
            go.name=name; go.transform.position=pos; go.transform.rotation=Quaternion.Euler(euler);
            Fit(go,targetSize);
            foreach(var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial=material;
            if(collider) go.AddComponent<BoxCollider>();
            return go;
        }

        static void Fit(GameObject go, Vector3 target)
        {
            go.transform.localScale=Vector3.one;
            var b=BoundsOf(go);
            go.transform.localScale=new Vector3(target.x/Mathf.Max(.01f,b.size.x),target.y/Mathf.Max(.01f,b.size.y),target.z/Mathf.Max(.01f,b.size.z));
        }
        static void Fit(GameObject go,float height) { var b=BoundsOf(go); go.transform.localScale*=height/Mathf.Max(.01f,b.size.y); }
        static void Center(GameObject go) { var b=BoundsOf(go); go.transform.localPosition-=go.transform.parent.InverseTransformVector(b.center-go.transform.parent.position); }
        static Bounds BoundsOf(GameObject go)
        {
            var rs=go.GetComponentsInChildren<Renderer>();
            if(rs.Length==0) return new Bounds(go.transform.position,Vector3.one);
            var b=rs[0].bounds; for(int i=1;i<rs.Length;i++) b.Encapsulate(rs[i].bounds); return b;
        }

        static GameObject Cube(string name,Vector3 pos,Vector3 scale,Material mat,bool collision,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(parent?parent:environment); go.transform.position=pos; go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=mat;
            if(!collision) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        static Transform NewGroup(string name,Transform parent=null) { var go=new GameObject(name); go.transform.SetParent(parent?parent:environment); return go.transform; }

        static void BuildMaterials()
        {
            sand=MaterialAsset("Sand",new Color(.35f,.48f,.38f)); rock=MaterialAsset("Rock",new Color(.18f,.36f,.34f)); rockDark=MaterialAsset("RockDark",new Color(.08f,.20f,.23f));
            coralA=MaterialAsset("CoralOrange",new Color(.94f,.43f,.27f)); coralB=MaterialAsset("CoralPink",new Color(.8f,.25f,.47f)); plant=MaterialAsset("Kelp",new Color(.18f,.55f,.31f));
            wreck=MaterialAsset("Wreck",new Color(.31f,.19f,.12f)); fish=MaterialAsset("Fish",new Color(.95f,.73f,.22f)); water=MaterialAsset("Water",new Color(.025f,.19f,.27f));
        }
        static Material MaterialAsset(string name,Color color)
        {
            string path=Output+"/"+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m) { m=new Material(Shader.Find("Standard")); m.name=name; AssetDatabase.CreateAsset(m,path); }
            m.color=color; m.SetFloat("_Glossiness",.18f); m.SetFloat("_Metallic",0); EditorUtility.SetDirty(m); return m;
        }
        static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path)) return;
            string parent=Path.GetDirectoryName(path).Replace('\\','/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
        static void RenderPreview(Camera camera)
        {
            var old=camera.targetTexture; var rt=new RenderTexture(1280,720,24); camera.targetTexture=rt; camera.Render();
            RenderTexture.active=rt; var tex=new Texture2D(1280,720,TextureFormat.RGB24,false); tex.ReadPixels(new Rect(0,0,1280,720),0,0); tex.Apply();
            File.WriteAllBytes(Path.GetFullPath(Output+"/UnderwaterExplorePreview.png"),tex.EncodeToPNG());
            camera.targetTexture=old; RenderTexture.active=null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
        }
    }
}
