using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WhaleFall;
using Object=UnityEngine.Object;

namespace WhaleFallEditor
{
    public static partial class OceanExpeditionSetup
    {
        const string HuntingArt=Art+"/Harpoon";
        [MenuItem("鲸落/海底探索/配置聪明鲨鱼、第一人称和鱼叉（不试玩）")]
        public static void ConfigureHunting()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Stop Play mode and save your changes first.");
            Directory.CreateDirectory(HuntingArt);AssetDatabase.Refresh();
            var gunPrefab=BuildHarpoonModel();var spearPrefab=BuildHarpoonProjectile();
            var report=new StringBuilder("Scene authoring and compilation only. No Play-mode / gameplay validation run.\n");
            foreach(string path in new[]{Underwater3DPrototypeBuilder.Destination,Third})
            {
                string backup=Work+"/"+Path.GetFileNameWithoutExtension(path)+"-before-hunting.unity";
                if(!File.Exists(backup)) File.Copy(path,backup);
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                var session=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DiveExpedition>()).Single();
                var diver=session.diver;var shark=session.boss;
                bool initial=!diver.GetComponent<HarpoonGun>();
                var gun=diver.GetComponent<HarpoonGun>()??diver.gameObject.AddComponent<HarpoonGun>();
                gun.diver=diver;gun.session=session;gun.gunPrefab=gunPrefab;gun.projectilePrefab=spearPrefab;
                if(initial)
                {
                    // Migrate only the responsiveness requested here; retain authored damage, oxygen and patrol area.
                    shark.horizontalFov=Mathf.Max(shark.horizontalFov,140);shark.verticalFov=Mathf.Max(shark.verticalFov,155);
                    shark.acquisitionSeconds=Mathf.Min(shark.acquisitionSeconds,.65f);shark.sensorInterval=Mathf.Min(shark.sensorInterval,.08f);
                    shark.chargeTurnSpeed=Mathf.Max(shark.chargeTurnSpeed,100);shark.chaseTurnSpeed=Mathf.Max(shark.chaseTurnSpeed,165);
                    shark.patrolTurnSpeed=Mathf.Max(shark.patrolTurnSpeed,95);shark.chaseSpeed=Mathf.Max(shark.chaseSpeed,6.2f);
                    shark.chargeSeconds=Mathf.Max(shark.chargeSeconds,3.2f);shark.loseSightSeconds=Mathf.Max(shark.loseSightSeconds,4.5f);
                    shark.avoidanceLookahead=Mathf.Max(shark.avoidanceLookahead,5);
                    shark.closeAwarenessRadius=8;shark.collisionStunChance=.35f;
                }
                diver.followCamera.nearClipPlane=.04f;
                BindHarpoonArms(gun);
                AddHuntingFish(scene,session);
                int count=0;
                foreach(var swimmer in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MineralFaunaSwimmer>()))
                {
                    if(swimmer.GetComponentInParent<SharkBossAI>()) continue;
                    AddCatchable(swimmer.gameObject);count++;
                }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                report.AppendLine("SAVED "+path+"; catchable mesh fish="+count+"; right arm="+(gun.rightHand?gun.rightHand.name:"unbound")+"; left arm="+(gun.leftHand?gun.leftHand.name:"unbound"));
                report.AppendLine("Bones: "+string.Join(",",diver.visual.GetComponentsInChildren<Transform>(true).Select(t=>t.name)));
            }
            AssetDatabase.SaveAssets();
            var first=EditorSceneManager.OpenScene(Underwater3DPrototypeBuilder.Destination,OpenSceneMode.Single);
            Selection.activeGameObject=first.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<HarpoonGun>()).Single().gameObject;
            File.WriteAllText(Work+"/hunting-setup.txt",report.ToString());
        }
        static void BindHarpoonArms(HarpoonGun gun)
        {
            var animator=gun.diver.animator;
            if(animator&&animator.isHuman)
            {
                gun.rightUpperArm=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);gun.rightForearm=animator.GetBoneTransform(HumanBodyBones.RightLowerArm);gun.rightHand=animator.GetBoneTransform(HumanBodyBones.RightHand);
                gun.leftUpperArm=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);gun.leftForearm=animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);gun.leftHand=animator.GetBoneTransform(HumanBodyBones.LeftHand);return;
            }
            var bones=gun.diver.visual.GetComponentsInChildren<Transform>(true);
            gun.rightUpperArm=FindArmBone(bones,"rightupperarm","rightarm","upperarmr");gun.rightForearm=FindArmBone(bones,"rightlowerarm","rightforearm","lowerarmr");gun.rightHand=FindArmBone(bones,"righthand","handr");
            gun.leftUpperArm=FindArmBone(bones,"leftupperarm","leftarm","upperarml");gun.leftForearm=FindArmBone(bones,"leftlowerarm","leftforearm","lowerarml");gun.leftHand=FindArmBone(bones,"lefthand","handl");
        }
        static Transform FindArmBone(Transform[] bones,params string[] names)
        {return bones.FirstOrDefault(t=>names.Any(n=>t.name.ToLowerInvariant().Replace("_","").Replace(" ","").EndsWith(n)));}
        static void AddHuntingFish(Scene scene,DiveExpedition session)
        {
            if(scene.GetRootGameObjects().Any(r=>r.name=="鱼叉渔场 · 中型游鱼")) return;
            var root=new GameObject("鱼叉渔场 · 中型游鱼").transform;
            var focus=new GameObject("鱼群活动中心").transform;focus.SetParent(root);focus.position=session.mineral.Center;
            string[] prefabs={"Mahi_mahi","Barracuda","Atlantic_cod"};string[] species={"鲯鳅","梭鱼","鳕鱼"};
            var floor=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Collider>()).FirstOrDefault(c=>c.name=="Terrain");
            for(int i=0;i<15;i++)
            {
                var fish=new GameObject(species[i%3]+" · 可捕获 "+(i+1));fish.transform.SetParent(root);
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AdditionalOceanScenes.DeluxeRoot+"/Prefabs/Animals/"+prefabs[i%3]+"_prefab.prefab"),fish.transform);
                FitVisual(model,1.4f+i%4*.3f,false);model.transform.localRotation=Quaternion.Euler(0,90,0);
                foreach(var collider in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                var swim=fish.AddComponent<MineralFaunaSwimmer>();swim.attraction=focus;swim.speed=1.3f+i%3*.25f;swim.preferredRadius=8;swim.maximumRadius=20;
                swim.yLimits=new Vector2(session.diver.yLimits.x+1,session.seaLevel-2);
                float angle=i*2.39996f;Vector3 desired=session.mineral.Center+new Vector3(Mathf.Cos(angle)*(6+i%4*2),6+i%3,Mathf.Sin(angle)*(6+i%4*2));
                fish.transform.position=floor?FirstOpenWater(scene,floor,desired,.7f,session.diver):FindOpenWater(scene,desired,.7f);
                var capture=fish.AddComponent<CatchableFish>();capture.species=species[i%3];
            }
        }
        static void AddCatchable(GameObject fish)
        {
            var catchable=fish.GetComponent<CatchableFish>()??fish.AddComponent<CatchableFish>();
            if(fish.transform.Find("鱼叉命中区域")) return;
            var renderers=fish.GetComponentsInChildren<Renderer>().Where(r=>!(r is ParticleSystemRenderer)).ToArray();if(renderers.Length==0) return;
            Bounds local=new Bounds();bool first=true;
            foreach(var renderer in renderers)
            {
                var b=renderer.bounds;
                for(int x=-1;x<=1;x+=2) for(int y=-1;y<=1;y+=2) for(int z=-1;z<=1;z+=2)
                {
                    Vector3 point=fish.transform.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));
                    if(first) {local=new Bounds(point,Vector3.zero);first=false;}else local.Encapsulate(point);
                }
            }
            var hitbox=new GameObject("鱼叉命中区域");hitbox.transform.SetParent(fish.transform,false);
            var box=hitbox.AddComponent<BoxCollider>();box.isTrigger=true;box.center=local.center;
            box.size=new Vector3(Mathf.Max(.2f,local.size.x*.85f),Mathf.Max(.2f,local.size.y*.85f),Mathf.Max(.25f,local.size.z*.9f));
        }
        static Material HarpoonMaterial(string name,Color color,float metallic)
        {
            string path=HuntingArt+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat) return mat;
            mat=new Material(Shader.Find("Standard")){color=color};mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Glossiness",.55f);AssetDatabase.CreateAsset(mat,path);return mat;
        }
        static Transform GunPart(Transform parent,string name,PrimitiveType shape,Vector3 position,Vector3 scale,Material mat,Vector3 angles=default(Vector3))
        {
            var go=GameObject.CreatePrimitive(shape);go.name=name;Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.transform.localRotation=Quaternion.Euler(angles);go.GetComponent<Renderer>().sharedMaterial=mat;return go.transform;
        }
        static void GunRod(Transform parent,string name,Vector3 from,Vector3 to,float width,Material mat)
        {var part=GunPart(parent,name,PrimitiveType.Cylinder,(from+to)*.5f,new Vector3(width,(to-from).magnitude*.5f,width),mat);part.localRotation=Quaternion.FromToRotation(Vector3.up,(to-from).normalized);}
        static GameObject BuildHarpoonModel()
        {
            string path=HuntingArt+"/PneumaticHarpoon.prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing) return existing;
            var steel=HarpoonMaterial("钛合金",new Color(.5f,.65f,.69f),.8f);var blue=HarpoonMaterial("深蓝枪身",new Color(.025f,.13f,.19f),.45f);
            var orange=HarpoonMaterial("橙色安全标识",new Color(1,.32f,.05f),.2f);var rubber=HarpoonMaterial("黑色握把",new Color(.025f,.032f,.038f),0);
            var root=new GameObject("鲸落 · 气动鱼叉枪");var t=root.transform;
            GunPart(t,"钛合金枪管",PrimitiveType.Cylinder,new Vector3(0,0,.2f),new Vector3(.11f,.46f,.11f),steel,new Vector3(90,0,0));
            GunPart(t,"深蓝压力舱",PrimitiveType.Capsule,new Vector3(0,-.05f,-.18f),new Vector3(.2f,.22f,.17f),blue,new Vector3(90,0,0));
            GunPart(t,"握把",PrimitiveType.Cube,new Vector3(0,-.16f,-.23f),new Vector3(.09f,.27f,.12f),rubber,new Vector3(-18,0,0));
            GunPart(t,"护手",PrimitiveType.Cube,new Vector3(0,-.2f,-.08f),new Vector3(.09f,.035f,.21f),orange);
            GunPart(t,"压力表",PrimitiveType.Cylinder,new Vector3(.085f,.03f,-.23f),new Vector3(.08f,.018f,.08f),orange,new Vector3(0,0,90));
            GunPart(t,"前握持",PrimitiveType.Cube,new Vector3(0,-.055f,.25f),new Vector3(.12f,.085f,.24f),rubber);
            GunPart(t,"Bolt",PrimitiveType.Cube,new Vector3(0,.075f,-.08f),new Vector3(.07f,.04f,.16f),orange);
            foreach(float z in new[]{-.04f,.42f,.64f}) GunPart(t,"锁紧环",PrimitiveType.Cylinder,new Vector3(0,0,z),new Vector3(.135f,.025f,.135f),blue,new Vector3(90,0,0));
            GunRod(t,"橙色瞄准导轨",new Vector3(-.075f,.055f,-.12f),new Vector3(-.075f,.055f,.63f),.018f,orange);
            var muzzle=new GameObject("Muzzle").transform;muzzle.SetParent(t,false);muzzle.localPosition=new Vector3(0,.065f,.95f);
            var loaded=new GameObject("LoadedSpear").transform;loaded.SetParent(t,false);loaded.localPosition=new Vector3(0,.065f,.87f);BuildSpearGeometry(loaded,steel,orange);
            var arms=new GameObject("ViewArms").transform;arms.SetParent(t,false);
            GunRod(arms,"右臂潜水服",new Vector3(.25f,-.32f,-.68f),new Vector3(.04f,-.15f,-.24f),.13f,blue);
            GunPart(arms,"右手手套",PrimitiveType.Sphere,new Vector3(.02f,-.12f,-.23f),new Vector3(.115f,.14f,.13f),rubber);
            GunRod(arms,"左臂潜水服",new Vector3(-.38f,-.35f,-.52f),new Vector3(-.04f,-.12f,.23f),.12f,blue);
            GunPart(arms,"左手手套",PrimitiveType.Sphere,new Vector3(-.015f,-.105f,.25f),new Vector3(.16f,.1f,.17f),rubber);
            existing=PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);return existing;
        }
        static void BuildSpearGeometry(Transform root,Material steel,Material orange)
        {
            GunRod(root,"鱼叉长杆",new Vector3(0,0,-1.05f),new Vector3(0,0,-.08f),.025f,steel);
            for(int i=-1;i<=1;i++)
            {
                GunRod(root,"叉齿",new Vector3(0,0,-.18f),new Vector3(i*.055f,0,-.055f),.016f,steel);
                var tip=new GameObject("锋尖",typeof(MeshFilter),typeof(MeshRenderer));tip.transform.SetParent(root,false);tip.transform.localPosition=new Vector3(i*.055f,0,0);
                tip.GetComponent<MeshFilter>().sharedMesh=SpearTip();tip.GetComponent<MeshRenderer>().sharedMaterial=steel;
                if(i!=0) GunRod(root,"倒钩",new Vector3(i*.055f,0,-.04f),new Vector3(i*.08f,0,-.09f),.012f,steel);
            }
            GunPart(root,"尾部导向翼",PrimitiveType.Cube,new Vector3(0,0,-.94f),new Vector3(.12f,.014f,.13f),orange);
        }
        static Mesh SpearTip()
        {
            string path=HuntingArt+"/HarpoonTip.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh) return mesh;
            var vertices=new Vector3[10];vertices[0]=Vector3.zero;vertices[9]=new Vector3(0,0,-.08f);
            for(int i=0;i<8;i++) {float a=i*Mathf.PI/4;vertices[i+1]=new Vector3(Mathf.Cos(a)*.018f,Mathf.Sin(a)*.018f,-.08f);}
            var triangles=new int[48];for(int i=0;i<8;i++) {int a=i+1,b=(i+1)%8+1;int j=i*6;triangles[j]=0;triangles[j+1]=a;triangles[j+2]=b;triangles[j+3]=9;triangles[j+4]=b;triangles[j+5]=a;}
            mesh=new Mesh{name="三棱鱼叉尖"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static GameObject BuildHarpoonProjectile()
        {
            string path=HuntingArt+"/HarpoonProjectile.prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab) return prefab;
            var root=new GameObject("发射鱼叉",typeof(HarpoonProjectile));
            BuildSpearGeometry(root.transform,HarpoonMaterial("钛合金",new Color(.5f,.65f,.69f),.8f),HarpoonMaterial("橙色安全标识",new Color(1,.32f,.05f),.2f));
            prefab=PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);return prefab;
        }
    }
}
