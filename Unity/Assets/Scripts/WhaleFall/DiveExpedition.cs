using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WhaleFall
{
    [DefaultExecutionOrder(-50)]
    public sealed class DiveExpedition : MonoBehaviour
    {
        [Header("场景绑定")]
        public Underwater3DDiver diver;
        public DiveInventory inventory;
        public BioMineralNode mineral;
        public SharkBossAI boss;
        public GameObject chestPrefab;
        public Vector3[] chestSites;
        [Header("氧气与水面撤离")]
        [Min(30)] public float maximumOxygen=240;
        [Min(.01f)] public float oxygenPerSecond=1;
        public float seaLevel=1;
        public float headAboveBody=.7f;
        [Min(.1f)] public float surfaceHoldSeconds=1.2f;
        [Min(0)] public float hitInvulnerability=.8f;
        [Header("吸引鲨鱼的动静半径")]
        public float chestNoiseRadius=32;
        public float miningNoiseRadius=25;
        public float harpoonNoiseRadius=38;
        [Header("随机宝箱（种子0 = 每次入场不同）")]
        public int seed;
        [Range(4,5)] public int minimumChests=4;
        [Range(4,5)] public int maximumChests=5;
        [SerializeField] int activeSeed;
        [Header("运行时状态")]
        [SerializeField] float oxygen;
        [SerializeField] bool ended;
        [SerializeField] bool extracted;
        public bool ActiveRound=>!ended;
        public bool BackpackOpen {get;private set;}
        public bool IsMining {get;private set;}
        public float Oxygen=>oxygen;
        public bool AcceptsAction=>!ended&&!BackpackOpen&&pendingScene==null;
        readonly List<DiveLootChest> chests=new List<DiveLootChest>();
        float surfaceTimer,lastHit=-100,toastUntil,flashUntil,nextMiningNoise;
        string toast="",prompt="",pendingScene;
        DiveLootChest nearbyChest;
        GUIStyle label,title,small,button;
        Font font;
        Vector2 scroll;
        const string BankPrefix="WhaleFall.Expedition.Bank.";

        void Awake()
        {
            if(!diver) diver=GetComponent<Underwater3DDiver>();
            if(!inventory) inventory=GetComponent<DiveInventory>();
            inventory.Clear();oxygen=maximumOxygen;ended=false;extracted=false;
            diver.allowReset=false;diver.showPrototypeHud=false;
            activeSeed=seed==0?unchecked(Environment.TickCount ^ (int)DateTime.UtcNow.Ticks):seed;
        }
        void Start()
        {
            SpawnChests();
            Notify("搜寻宝箱获取镐子 → 躲避鲨鱼开采生物矿 → 氧气耗尽前上浮撤离");
        }
        void SpawnChests()
        {
            if(!chestPrefab||chestSites==null||chestSites.Length<4) { Debug.LogError("Expedition needs a chest prefab and at least four spawn sites.",this);return; }
            var rng=new System.Random(activeSeed);
            var sites=new List<Vector3>(chestSites);
            for(int i=sites.Count-1;i>0;i--) { int j=rng.Next(i+1);var p=sites[i];sites[i]=sites[j];sites[j]=p; }
            int count=Mathf.Min(sites.Count,rng.Next(Mathf.Clamp(minimumChests,4,5),Mathf.Clamp(maximumChests,minimumChests,5)+1));
            var root=new GameObject("本次随机宝箱 · 种子 "+activeSeed).transform;
            var chosen=new List<Vector3>();
            // Prefer widely separated sites; the authored sites are already on clear seabed.
            for(int pass=0;pass<2&&chosen.Count<count;pass++)
                foreach(var point in sites)
                {
                    if(chosen.Contains(point)||pass==0&&chosen.Exists(p=>Vector3.Distance(p,point)<12)) continue;
                    chosen.Add(point);if(chosen.Count==count) break;
                }
            for(int i=0;i<chosen.Count;i++)
            {
                var go=Instantiate(chestPrefab,chosen[i],Quaternion.Euler(0,rng.Next(360),0),root);
                go.name="随机宝箱 "+(i+1);var chest=go.GetComponent<DiveLootChest>();chest.Roll(rng,i);chests.Add(chest);
            }
        }
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.E)) { BackpackOpen=!BackpackOpen;Cursor.lockState=CursorLockMode.None;Cursor.visible=true; }
            diver.inputSuppressed=ended||BackpackOpen||pendingScene!=null;
            IsMining=false;prompt="";
            if(ended) return;
            // Inventory is not a pause: oxygen and the predator keep running.
            oxygen=Mathf.Max(0,oxygen-Time.deltaTime*oxygenPerSecond);
            if(oxygen<=0) { End(false);return; }
            if(diver.transform.position.y+headAboveBody>=seaLevel)
            {
                surfaceTimer+=Time.deltaTime;prompt="正在上浮撤离… "+Mathf.RoundToInt(surfaceTimer/surfaceHoldSeconds*100)+"%";
                if(surfaceTimer>=surfaceHoldSeconds) End(true);
                return;
            }
            surfaceTimer=0;
            if(BackpackOpen||pendingScene!=null) return;
            nearbyChest=null;float distance=float.MaxValue;
            foreach(var chest in chests)
            {
                if(!chest||!chest.CanReach(diver)) continue;
                float d=(chest.transform.position-diver.transform.position).sqrMagnitude;
                if(d<distance) {distance=d;nearbyChest=chest;}
            }
            if(nearbyChest)
            { prompt="按 F 打开宝箱";if(Input.GetKeyDown(KeyCode.F)) nearbyChest.Open(this); }
            else if(mineral&&mineral.CanReach(diver))
            {
                prompt="长按 F 开采 · "+(inventory.equippedPickaxe==0?"徒手":"镐子 "+inventory.equippedPickaxe+"级")+" · "+(mineral.progress*100).ToString("0")+"%";
                if(Input.GetKey(KeyCode.F))
                {
                    IsMining=true;mineral.Mine(this,Time.deltaTime);
                    if(Time.time>=nextMiningNoise) {EmitNoise(diver.transform.position,miningNoiseRadius);nextMiningNoise=Time.time+1.1f;}
                }
            }
        }
        public void TakeOxygenDamage(float amount,Vector3 away,float knockback)
        {
            if(ended||Time.time-lastHit<hitInvulnerability) return;
            lastHit=Time.time;oxygen=Mathf.Max(0,oxygen-Mathf.Max(0,amount));flashUntil=Time.time+.5f;
            diver.ApplyImpact(away*knockback,.45f);
            Notify("被鲨鱼咬中！氧气 -"+amount.ToString("0")+"；绕过礁石甩开它！");
            if(oxygen<=0) End(false);
        }
        void End(bool success)
        {
            if(ended) return;
            ended=true;extracted=success;BackpackOpen=false;IsMining=false;diver.inputSuppressed=true;
            if(success)
            {
                foreach(var stack in inventory.items)
                {
                    string key=BankPrefix+stack.item;
                    PlayerPrefs.SetInt(key,PlayerPrefs.GetInt(key,0)+stack.count);
                }
                PlayerPrefs.SetInt(BankPrefix+"FishCount",PlayerPrefs.GetInt(BankPrefix+"FishCount",0)+inventory.fish.Count);
                PlayerPrefs.SetFloat(BankPrefix+"FishKg",PlayerPrefs.GetFloat(BankPrefix+"FishKg",0)+inventory.FishWeight);
                PlayerPrefs.Save();
            }
            else inventory.Clear();
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        public void Notify(string message) { toast=message;toastUntil=Time.time+5.5f; }
        public void EmitNoise(Vector3 point,float radius) {if(boss) boss.HearNoise(point,radius);}
        public void RequestSceneSwitch(string scene)
        { if(ended) SceneManager.LoadSceneAsync(scene);else pendingScene=scene; }
        void OnGUI()
        {
            if(label==null)
            {
                font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Arial"},18);
                label=new GUIStyle(GUI.skin.label){font=font,fontSize=18,wordWrap=true};label.normal.textColor=new Color(.85f,.96f,1);
                title=new GUIStyle(label){fontSize=26,fontStyle=FontStyle.Bold};
                small=new GUIStyle(label){fontSize=15};button=new GUIStyle(GUI.skin.button){font=font,fontSize=17};
            }
            var previous=GUI.matrix;float scale=Mathf.Max(.45f,Screen.height/900f);GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            float w=Screen.width/scale,h=Screen.height/scale;
            Panel(new Rect(24,24,370,160));GUI.Label(new Rect(40,32,335,36),"鲸落 · 生物矿禁区",title);
            GUI.Label(new Rect(40,75,330,28),"氧气  "+oxygen.ToString("0.0")+" / "+maximumOxygen.ToString("0")+" 秒",label);
            Bar(new Rect(40,112,330,12),oxygen/maximumOxygen,oxygen<45?new Color(1,.28f,.2f):new Color(.2f,.8f,.9f));
            GUI.Label(new Rect(40,138,335,32),"E 背包    F 交互 / 长按开采",small);
            if(mineral)
            {
                Panel(new Rect(24,194,370,106));
                GUI.Label(new Rect(40,201,335,30),"生物矿 · 形态 "+mineral.Stage+"/4   "+(mineral.progress*100).ToString("0")+"%",label);
                GUI.Label(new Rect(40,231,335,52),mineral.Depleted?"开采完成 · 请上浮撤离":"距离 "+Vector3.Distance(diver.transform.position,mineral.Center).ToString("0")+"m · 沿标记靠近",small);
                DrawMarker(mineral.Center,"生物矿",new Color(.55f,1,.82f),w,h,scale);
            }
            if(boss&&ActiveRound)
            {
                Panel(new Rect(w/2-185,24,370,79));
                GUI.Label(new Rect(w/2-170,30,340,28),"鲨鱼 · "+boss.StatusLabel,label);
                Bar(new Rect(w/2-170,65,340,10),boss.Suspicion,boss.IsHunting?new Color(1,.3f,.2f):new Color(1,.78f,.25f));
            }
            if(!string.IsNullOrEmpty(prompt)) {Panel(new Rect(w/2-260,h-155,520,44));GUI.Label(new Rect(w/2-245,h-150,490,38),prompt,label);}
            if(Time.time<toastUntil) {Panel(new Rect(w/2-390,h-230,780,61));GUI.Label(new Rect(w/2-372,h-222,744,54),toast,small);}
            GUI.Label(new Rect(28,h-48,w-55,40),"WASD 游动 · Space 上浮 · Ctrl 下潜 · Shift 加速 · V 切视角 · 右键瞄准 / 左键鱼叉 · E 背包（不暂停）",small);
            if(Time.time<flashUntil) {GUI.color=new Color(1,.1f,.05f,.18f);GUI.DrawTexture(new Rect(0,0,w,h),Texture2D.whiteTexture);GUI.color=Color.white;}
            if(ended) DrawResult(w,h);
            if(BackpackOpen) DrawInventory(w,h);
            if(pendingScene!=null)
            {
                Panel(new Rect(w/2-250,h/2-95,500,190));GUI.Label(new Rect(w/2-225,h/2-78,450,70),"离开本次潜水？\n未撤离的背包物品会全部丢失。",label);
                if(GUI.Button(new Rect(w/2-215,h/2+15,205,45),"留下继续潜水",button)) pendingScene=null;
                if(GUI.Button(new Rect(w/2+10,h/2+15,205,45),"放弃并切换地图",button)) {string next=pendingScene;pendingScene=null;End(false);SceneManager.LoadSceneAsync(next);}
            }
            GUI.matrix=previous;
        }
        void DrawInventory(float w,float h)
        {
            Rect panel=new Rect(w/2-260,h/2-295,520,590);Panel(panel);
            GUI.Label(new Rect(panel.x+24,panel.y+16,400,40),"背包 · 本次携带",title);
            if(GUI.Button(new Rect(panel.x+420,panel.y+18,75,32),"关闭 E",button)) BackpackOpen=false;
            GUI.Label(new Rect(panel.x+24,panel.y+62,475,30),"当前工具："+(inventory.equippedPickaxe==0?"徒手":"镐子 "+inventory.equippedPickaxe+"级"),label);
            for(int tier=0;tier<=3;tier++)
            {
                GUI.enabled=tier==0||inventory.Count((DiveItem)((int)DiveItem.Pickaxe1+tier-1))>0;
                if(GUI.Button(new Rect(panel.x+24+tier*120,panel.y+100,110,35),tier==0?"徒手":"装备 "+tier+"级镐",button)) inventory.Equip(tier);
            }
            GUI.enabled=true;
            GUI.Label(new Rect(panel.x+24,panel.y+148,470,28),"物品                         携带 / 已撤离仓库",small);
            scroll=GUI.BeginScrollView(new Rect(panel.x+24,panel.y+182,475,338),scroll,new Rect(0,0,450,465+inventory.fish.Count*37));
            int row=0;foreach(DiveItem item in Enum.GetValues(typeof(DiveItem)))
            {GUI.Label(new Rect(0,row++*37,445,35),DiveInventory.Label(item)+"      "+inventory.Count(item)+" / "+PlayerPrefs.GetInt(BankPrefix+item,0),label);}
            GUI.Label(new Rect(0,row++*37,445,35),"本次鱼获 "+inventory.fish.Count+" 条 / "+inventory.FishWeight.ToString("0.0")+" kg",label);
            GUI.Label(new Rect(0,row++*37,445,35),"鱼获仓库 "+PlayerPrefs.GetInt(BankPrefix+"FishCount",0)+" 条 / "+PlayerPrefs.GetFloat(BankPrefix+"FishKg",0).ToString("0.0")+" kg",small);
            foreach(var caught in inventory.fish) GUI.Label(new Rect(0,row++*37,445,35),caught.species+"    "+caught.kilograms.ToString("0.0")+" kg",small);
            GUI.EndScrollView();GUI.Label(new Rect(panel.x+24,panel.y+540,475,38),"镐子自动装备最高级；氧气耗尽只清空本次携带。",small);
        }
        void DrawResult(float w,float h)
        {
            Panel(new Rect(w/2-290,h/2-180,580,360));
            GUI.Label(new Rect(w/2-260,h/2-155,520,50),extracted?"撤离成功 · 物资已保存":"氧气耗尽 · 潜水失败",title);
            GUI.Label(new Rect(w/2-260,h/2-90,520,145),extracted?"本次物资已存入撤离仓库。\n生物矿开采进度："+(mineral.progress*100).ToString("0")+"%\n按 E 查看背包和仓库。":"本次背包物品已全部丢失。\n利用礁石遮挡视线，寻找镐子提高效率，\n并给上浮撤离预留氧气。",label);
            if(GUI.Button(new Rect(w/2-245,h/2+93,490,46),"重新潜水 · 新的随机宝箱",button)) SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);
        }
        void DrawMarker(Vector3 point,string text,Color color,float w,float h,float scale)
        {
            if(!diver.followCamera) return;
            Vector3 p=diver.followCamera.WorldToScreenPoint(point);if(p.z<=0) return;
            float x=Mathf.Clamp(p.x/scale,100,w-140),y=Mathf.Clamp((Screen.height-p.y)/scale,130,h-260);
            GUI.color=color;GUI.Label(new Rect(x-50,y-22,165,30),"◆ "+text,small);GUI.color=Color.white;
        }
        static void Panel(Rect rect) {GUI.color=new Color(.012f,.045f,.07f,.93f);GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=Color.white;}
        static void Bar(Rect rect,float fraction,Color color) {GUI.color=new Color(.08f,.13f,.16f);GUI.DrawTexture(rect,Texture2D.whiteTexture);rect.width*=Mathf.Clamp01(fraction);GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=Color.white;}
        void OnDestroy() {if(font) Destroy(font);}
    }
}
