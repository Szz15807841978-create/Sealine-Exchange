using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fishing;
using Fishing.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace FishingEditor
{
    /// <summary>Editor-only authoring. All UI objects and bindings are saved into Main.unity before Play.</summary>
    public static class PlayerMarketSceneBuilder
    {
        const string MainPath="Assets/Scenes/Main.unity";
        const string RaftPath="Assets/Scenes/Current/DivePointRealisticOceanSky_Unified.unity";
        const string SettingsPath="Assets/PlayerFishing/PlayerMarketSettings.asset";
        static readonly Color Bg=new Color(.035f,.075f,.105f),Panel=new Color(.062f,.14f,.18f,.97f),Field=new Color(.035f,.09f,.125f),TextColor=new Color(.9f,.95f,.96f),Muted=new Color(.55f,.68f,.74f),Green=new Color(.45f,.88f,.72f);
        static readonly List<PlayerTextBinding> Texts=new List<PlayerTextBinding>();
        static readonly List<PlayerButtonBinding> Buttons=new List<PlayerButtonBinding>();
        static Font font;
        static PlayerMarketView view;
        static RectTransform canvas;
        [MenuItem("Fishing/玩家版/替换 Main 为可编辑玩家版（先备份）")]
        public static void BuildFromMenu()
        {
            if(!EditorUtility.DisplayDialog("替换 Main", "将替换 Main 中的旧捕捞 UI，并备份原场景。木筏场景不会被保存或改写。后续调布局不要重复生成。", "生成", "取消"))return;
            Build();
        }
        public static void BuildFromCommandLine()
        {
            try{Build();File.WriteAllText("tools/PlayerMainBuild.success.txt","PASS: Main scene built and validated. Raft scene unchanged.\n");}
            catch(Exception e){File.WriteAllText("tools/PlayerMainBuild.failed.txt",e.ToString());Debug.LogException(e);throw;}
        }
        static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;string parent=path.Substring(0,path.LastIndexOf('/'));Folder(parent);AssetDatabase.CreateFolder(parent,path.Substring(path.LastIndexOf('/')+1));
        }
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
            byte[] raftBefore=File.ReadAllBytes(RaftPath);var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(MainPath);bool opened=!scene.IsValid()||!scene.isLoaded;
            if(!opened&&scene.isDirty)throw new InvalidOperationException("Main has unsaved edits. Save or resolve them before replacing it.");
            string backup="tools/backups/player-main-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(MainPath,backup+"/Main.unity");File.Copy(MainPath+".meta",backup+"/Main.unity.meta");
            Folder("Assets/PlayerFishing");var settings=AssetDatabase.LoadAssetAtPath<PlayerMarketSettings>(SettingsPath);if(!settings){settings=ScriptableObject.CreateInstance<PlayerMarketSettings>();AssetDatabase.CreateAsset(settings,SettingsPath);}
            const string fontPath="Assets/PlayerFishing/PlayerChinese.fontsettings";font=AssetDatabase.LoadAssetAtPath<Font>(fontPath);if(!font){font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",20);font.name="PlayerChinese";AssetDatabase.CreateAsset(font,fontPath);}
            if(opened)scene=EditorSceneManager.OpenScene(MainPath,OpenSceneMode.Additive);
            try{
                SceneManager.SetActiveScene(scene);foreach(var root in scene.GetRootGameObjects())Object.DestroyImmediate(root);
                Texts.Clear();Buttons.Clear();var rootObject=new GameObject("Player Main · 玩家捕捞");SceneManager.MoveGameObjectToScene(rootObject,scene);
                var cameraObject=new GameObject("Main Camera",typeof(Camera));cameraObject.transform.SetParent(rootObject.transform,false);cameraObject.tag="MainCamera";cameraObject.transform.localPosition=new Vector3(0,0,-10);var cam=cameraObject.GetComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Bg;cam.orthographic=true;
                var eventObject=new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));eventObject.transform.SetParent(rootObject.transform,false);
                canvas=Rect(rootObject.transform,"Player Canvas · 可编辑界面",0,0,1920,1080);var c=canvas.gameObject.AddComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=200;canvas.gameObject.AddComponent<CanvasGroup>();canvas.gameObject.AddComponent<GraphicRaycaster>();var scaler=canvas.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
                view=canvas.gameObject.AddComponent<PlayerMarketView>();view.Canvas=c;view.Background=Box(canvas,"00_Backdrop · 背景（木筏进入时透明）",0,0,1920,1080,Bg);view.Background.raycastTarget=true;
                var controller=rootObject.AddComponent<PlayerMarketController>();controller.View=view;controller.Settings=settings;
                Header();Left();Center();Right();Modals();
                view.Texts=Texts.ToArray();view.Buttons=Buttons.ToArray();
                EditorUtility.SetDirty(view);EditorUtility.SetDirty(controller);EditorSceneManager.MarkSceneDirty(scene);
                Validate(scene);
                if(!EditorSceneManager.SaveScene(scene,MainPath,false))throw new IOException("Could not save Main");
                var builds=EditorBuildSettings.scenes.ToList();EnsureBuild(builds,MainPath);EnsureBuild(builds,RaftPath);EditorBuildSettings.scenes=builds.ToArray();AssetDatabase.SaveAssets();
                Preview(scene,cam,"tools/PlayerMain-preview.png");
                if(!File.ReadAllBytes(RaftPath).SequenceEqual(raftBefore))throw new InvalidOperationException("Raft unexpectedly changed");
                File.WriteAllText("tools/PlayerMainScene-report.txt","Main="+MainPath+"\nObjects="+scene.GetRootGameObjects().Sum(r=>r.GetComponentsInChildren<Transform>(true).Length)+"\nTexts="+view.Texts.Length+"\nButtons="+view.Buttons.Length+"\nRaftUnchanged=True\nRuntimeConstruction=False\nSettings="+SettingsPath+"\nBackup="+backup+"\n");
                Debug.Log("[Player Main] 可编辑玩家版已保存。木筏场景未改动。",controller);
            }finally{if(opened&&scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);}
        }
        static void EnsureBuild(List<EditorBuildSettingsScene> scenes,string path) { int i=scenes.FindIndex(s=>s.path==path);if(i<0)scenes.Add(new EditorBuildSettingsScene(path,true));else scenes[i].enabled=true; }
        static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var go=new GameObject(name,typeof(RectTransform));var rt=(RectTransform)go.transform;rt.SetParent(parent,false);rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);return rt;
        }
        static Image Box(Transform parent,string name,float x,float y,float w,float h,Color color)
        {
            var rt=Rect(parent,name,x,y,w,h);var im=rt.gameObject.AddComponent<Image>();im.color=color;im.raycastTarget=false;return im;
        }
        static Text Label(Transform parent,string name,string text,float x,float y,float w,float h,int size=20,string key=null,Color? color=null)
        {
            var rt=Rect(parent,name,x,y,w,h);var t=rt.gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.color=color??TextColor;t.text=text;t.alignment=TextAnchor.MiddleLeft;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;t.raycastTarget=false;t.supportRichText=false;
            if(key!=null)Texts.Add(new PlayerTextBinding{Key=key,Target=t});return t;
        }
        static Button Button(Transform parent,string name,string label,string action,float x,float y,float w,float h,bool primary=false)
        {
            var im=Box(parent,name,x,y,w,h,primary?Green:new Color(.095f,.21f,.27f));im.raycastTarget=true;var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;var colors=b.colors;colors.highlightedColor=new Color(.83f,1,1);colors.pressedColor=new Color(.65f,.8f,.85f);colors.disabledColor=new Color(.5f,.55f,.6f,.6f);b.colors=colors;var nav=b.navigation;nav.mode=Navigation.Mode.None;b.navigation=nav;
            Label(im.transform,"Label · 文案",label,8,2,w-16,h-4,18,null,primary?Bg:TextColor).alignment=TextAnchor.MiddleCenter;Buttons.Add(new PlayerButtonBinding{Action=action,Target=b});return b;
        }
        static InputField InputBox(Transform parent,string name,string initial,float x,float y,float w,float h)
        {
            var im=Box(parent,name,x,y,w,h,Field);im.raycastTarget=true;var input=im.gameObject.AddComponent<InputField>();input.targetGraphic=im;input.contentType=InputField.ContentType.IntegerNumber;input.characterLimit=9;input.textComponent=Label(im.transform,"Value · 数值",initial,12,0,w-24,h,20);input.text=initial;return input;
        }
        static Image FillBar(Transform parent,string name,float x,float y,float w,float h,Color color,bool vertical=false)
        {
            var back=Box(parent,name,x,y,w,h,new Color(.13f,.24f,.29f));var fill=Box(back.transform,"Fill · 动态填充",0,0,w,h,color);fill.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");fill.type=Image.Type.Filled;fill.fillMethod=vertical?Image.FillMethod.Vertical:Image.FillMethod.Horizontal;fill.fillOrigin=0;fill.fillAmount=0;return fill;
        }
        static RectTransform PanelAt(string name,float x,float y,float w,float h) { return Box(canvas,name,x,y,w,h,Panel).rectTransform; }
        static void Header()
        {
            var root=Rect(canvas,"01_Header · 标题与资源",30,20,1860,144);
            Box(root,"Backdrop · 标题可读性底板",-30,-20,1920,164,Bg);
            Button(root,"HiddenEntry · 连点五次", "潮", "logo",0,0,54,54,true);Label(root,"Title","潮汐交易所",72,0,470,36,30);Label(root,"Edition","FISHING MARKET  /  PLAYER",74,37,450,22,13,null,Muted);
            Label(root,"Save","本机自动存档",1350,4,210,44,16,"save",Muted);Button(root,"Help","怎么玩","help",1570,3,130,46);Button(root,"ReturnRaft","返回木筏","return",1714,3,146,46);
            string[] titles={"可用活鱼","独立诱饵","捕捞网","累计净增（收市计费）"},keys={"walletFish","walletBait","net","lifetimePL"},values={"100 kg","60 份","尚未带回","+0 kg"};
            for(int i=0;i<4;i++){var card=Box(root,"Wallet_"+keys[i],i*468,72,456,72,Panel);Label(card.transform,"Title",titles[i],16,5,420,24,16,null,Muted);Label(card.transform,"Value",values[i],16,29,420,38,28,keys[i]);}
        }
        static void Left()
        {
            var root=PanelAt("02_Exploration · 调查与海况",30,184,330,782);Label(root,"Title","出航准备",18,10,290,36,22);Label(root,"Day","第 1 天 · 第 1 场",18,45,290,28,16,"day",Muted);
            view.MapEstimates=new Text[3];for(int i=0;i<3;i++){var b=Button(root,"Map_"+i+" · "+PlayerMarket.MapNames[i],"","map"+i,18,87+i*100,294,88);view.MapEstimates[i]=Label(b.transform,"FishEstimate",PlayerMarket.MapNames[i]+"\n尚未调查",12,5,270,78,17);}
            Button(root,"Survey","下海调查","survey",18,397,294,44);Label(root,"SurveyHint","下潜采样并返回，带回情报与捕捞网。",18,446,294,50,16,"surveyNote",Muted);
            Label(root,"Station","海况观测站",18,507,170,32,20);Button(root,"Observe","读取仪器","observe",193,506,119,34);Label(root,"Weather","晴天",18,549,294,30,22,"weather");
            var temp=Box(root,"Temperature · 温度计",18,590,141,160,Field);Label(temp.transform,"Title","水温",14,8,115,28,16,null,Muted);view.TemperatureFill=FillBar(temp.transform,"Thermometer",64,43,14,66,new Color(.38f,.78f,.93f),true);Label(temp.transform,"Reading","— ℃",14,118,115,30,22,"temperature");
            var wind=Box(root,"Wind · 观风仪",171,590,141,160,Field);Label(wind.transform,"Title","风速",14,8,115,28,16,null,Muted);view.WindNeedle=Label(wind.transform,"Needle · 风向指示","↑",49,40,45,67,43,null,Green).rectTransform;view.WindNeedle.pivot=new Vector2(.5f,.5f);view.WindNeedle.anchoredPosition=new Vector2(71,-74);Label(wind.transform,"Reading","— m/s",14,118,115,30,20,"wind");
            Box(canvas,"08_Footer · 快捷键底板",30,1006,1860,48,Bg);
            Label(canvas,"Footer","Space 暂停 / 继续    A 收网    Q 停针    挑战中交替 A / D",48,1014,1150,30,16,null,Muted);
        }
        static void Center()
        {
            var root=PanelAt("03_Market · 行情曲线",380,184,1158,410);Label(root,"MapName","珊瑚浅湾",20,10,520,30,20,"marketTitle",Muted);Label(root,"Price","100.00",20,46,480,60,45,"price");Label(root,"Change","+0.0% 相对开盘",22,107,420,29,18,"priceChange",Green);Label(root,"Phase","准备出航",932,14,200,30,18,"phase",Muted);Label(root,"Time","90.0 s",931,53,206,52,35,"remaining");
            var chartRt=Rect(root,"Chart · 拖动缩放曲线区域",20,148,1050,215);view.Chart=chartRt.gameObject.AddComponent<PlayerMarketChart>();view.Chart.raycastTarget=false;view.Chart.YLabels=new Text[5];
            for(int i=0;i<5;i++)view.Chart.YLabels[i]=Label(root,"AxisY_"+i,"",1082,350-i*54,70,26,14,null,Muted);
            for(int i=0;i<7;i++)Label(root,"AxisTime_"+i,(i*15)+"s",20+i*175,374,64,23,14,null,Muted);
            var trade=PanelAt("04_Trading · 下网追加与分批收网",380,612,1158,354);Label(trade,"Title","配置本次捕捞",20,8,610,34,22);
            Label(trade,"WeightLabel","下网股本 · 整数 kg",20,49,470,27,17,"weightLabel",Muted);view.WeightInput=InputBox(trade,"WeightInput", "50",20,82,315,43);Button(trade,"Max","使用可用上限","max",348,82,177,43);
            Label(trade,"BaitLabel","本场诱饵 · 份（开市扣一次）",562,49,565,27,17,null,Muted);view.BaitInput=InputBox(trade,"BaitInput","10",562,82,576,43);
            string[] captions={"当前网内重量","本批预计到账","收后留网余量"},keys={"position","return","remainder"};for(int i=0;i<3;i++){Label(trade,"Caption_"+keys[i],captions[i],20+i*374,135,355,24,16,null,Muted);Label(trade,"Value_"+keys[i],"—",20+i*374,162,355,34,28,keys[i]);}
            Label(trade,"PercentLabel","收当前重量的",20,207,143,35,17,null,Muted);view.PercentInput=InputBox(trade,"PercentInput","100",170,207,80,35);Label(trade,"PercentSign","%",257,207,25,35,17);Button(trade,"Collect25","25%","percent25",290,207,81,35);Button(trade,"Collect50","50%","percent50",383,207,81,35);Button(trade,"CollectAll","全部","percent100",476,207,81,35);Label(trade,"Fee","暂记手续费 0 kg · 收市统一交",584,207,550,35,17,"fee",new Color(.97f,.78f,.47f));
            Button(trade,"Start","开市并下网","start",20,253,1118,45,true);Button(trade,"Pause","暂停 Space","pause",20,253,255,45);Button(trade,"Cast","下网","cast",295,253,255,45);Button(trade,"Add","追加重量","add",295,253,255,45);Button(trade,"Collect","收 100% · A","collect",570,253,274,45,true);Button(trade,"Finish","提前收市","finish",864,253,274,45);
            foreach(var key in new[]{"pause","cast","add","collect","finish"})Buttons.Last(b=>b.Action==key).Target.gameObject.SetActive(false);
            view.CooldownFill=FillBar(trade,"Cooldown",20,306,1118,4,new Color(.38f,.78f,.93f));Label(trade,"Hint","下网仅扣本金；双边 0.5% 收市交。",20,316,1118,30,16,"tradeHint",Muted);
            BuildChallenge(root);
        }
        static void BuildChallenge(RectTransform root)
        {
            view.ChallengePanel=Box(root,"Challenge · 局内小游戏（默认隐藏）",70,139,1018,252,new Color(.028f,.07f,.11f,.98f)).gameObject;var r=view.ChallengePanel.transform;
            Label(r,"Title","收网挑战",20,9,790,30,22,"gaugeTitle");Label(r,"Number","1 / 2",919,9,80,30,17,"gaugeNumber",Muted);
            view.TimingPanel=Rect(r,"Timing · 停针仪表盘",20,61,978,104).gameObject;view.TimingTrack=Rect(view.TimingPanel.transform,"Track · 区间轨道",0,0,978,46);view.TimingSegments=new Image[7];double[] widths={8,12,26,26,12,8,8};Color[] colors={new Color(.97f,.78f,.47f),new Color(.38f,.78f,.93f),Muted,Muted,new Color(.38f,.78f,.93f),new Color(1,.55f,.57f),new Color(1,.55f,.57f)};float x=0;
            for(int i=0;i<7;i++){view.TimingSegments[i]=Box(view.TimingTrack,"Segment_"+i,x,0,(float)widths[i]*9.78f,46,colors[i]);x+=(float)widths[i]*9.78f;}
            view.TimingNeedle=Box(view.TimingTrack,"Needle · 指针",0,0,4,46,Color.white).rectTransform;view.TimingNeedle.pivot=new Vector2(.5f,.5f);view.TimingNeedle.anchorMin=new Vector2(0,0);view.TimingNeedle.anchorMax=new Vector2(0,1);view.TimingNeedle.sizeDelta=new Vector2(4,0);
            Button(r,"Stop","停针 Q","gaugeStop",810,198,188,38,true);
            view.SpeedPanel=Rect(r,"Speed · AD 拼手速",20,49,978,130).gameObject;var speed=view.SpeedPanel.transform;
            Label(speed,"Count","0 次有效交替",0,0,650,35,28,"speedCount");Label(speed,"Time","6.0 s",842,0,136,35,26,"speedTime");Label(speed,"Grade","力道不足",656,37,322,28,18,"speedGrade");Label(speed,"Hint","交替越快，收网越有力",0,37,600,28,17,null,Muted);view.SpeedFill=FillBar(speed,"Effort",0,72,978,6,Green);Button(speed,"TapA","A","speedA",0,88,475,46);Button(speed,"TapD","D","speedD",495,88,483,46);
            view.ChallengeTimeFill=FillBar(r,"TimeBar",20,189,978,4,Green);Label(r,"Hint","5 秒内停下指针",20,203,773,34,17,"gaugeHint",Muted);view.SpeedPanel.SetActive(false);view.ChallengePanel.SetActive(false);
        }
        static void Right()
        {
            var root=PanelAt("05_Island · 生态培育",1558,184,332,227);Label(root,"Title","岛屿生态",18,10,296,30,22);Label(root,"Caption","海域环境",18,54,155,27,17,null,Muted);Label(root,"Environment","清澈",191,52,124,32,22,"clean",Green);Label(root,"CultCaption","鱼群培育",18,100,296,24,17,null,Muted);Label(root,"CultState","尚未开始",18,130,132,32,19,"cultivation");Button(root,"Cultivate","投入 5 份诱饵","cultivate",158,127,156,38);Label(root,"Hint","在休渔时投料，养护这片海。",18,181,296,28,16,null,Muted);
            root=PanelAt("06_Rumor · NPC 情报",1558,429,332,163);Label(root,"Title","鱼汛情报",18,9,172,31,22);Button(root,"AskNPC","询问渔民","npc",193,10,120,33);Label(root,"Rumor","老渔民似乎知道一些鱼群动向。",18,53,296,67,18,"npc");Label(root,"Warning","海上的消息，不一定都准。",18,129,296,26,15,null,Muted);
            root=PanelAt("07_Radio · 海上电台",1558,612,332,354);Label(root,"Title","海上电台",18,9,296,31,22);var content=Scroll(root,"Messages",18,51,296,282,6*86);view.RadioRows=new Text[6];for(int i=0;i<6;i++)view.RadioRows[i]=Label(content,"Message_"+i,"",0,i*86,280,81,16);
            // Full ledger uses a scene-authored scroll; not runtime-instantiated rows.
            var ledger=PanelAt("08_Ledger · 收网账本",30,1080,1860,270);Label(ledger,"Title","分批收网账本",20,10,700,31,22);Label(ledger,"Fee","每批记录为费前；手续费收市统一缴纳。",790,10,1040,31,17,"ledgerFee",Muted);var rows=Scroll(ledger,"Rows",20,52,1816,190,30*36);view.LedgerRows=new Text[30];for(int i=0;i<30;i++)view.LedgerRows[i]=Label(rows,"Trade_"+i,"",0,i*36,1760,34,18);
            // Ledger is a toggleable, editable modal rather than placing it off-screen in the final layout.
            ledger.anchoredPosition=new Vector2(200,-770);ledger.sizeDelta=new Vector2(1520,270);ledger.gameObject.SetActive(false);
            // Expose the same serialized ledger as an auxiliary page inside help (no runtime creation).
            ledger.name="Ledger · 固定账本面板";
            pendingLedger=ledger;
            view.ResultPanel=Box(canvas,"09_Result · 结算（默认隐藏）",520,278,880,372,new Color(.035f,.105f,.135f,.99f)).gameObject;var result=view.ResultPanel.transform;Label(result,"Title","本场结算",30,18,820,46,29);Label(result,"NetGain","+0 kg",30,79,820,65,43,"resultPL",Green);Label(result,"Costs","",30,158,820,83,24,"resultDetail");Button(result,"Next","准备第二场","next",30,274,820,60,true);view.ResultPanel.SetActive(false);
        }
        static RectTransform pendingLedger;
        static RectTransform Scroll(Transform parent,string name,float x,float y,float w,float h,float contentHeight)
        {
            var rt=Rect(parent,name,x,y,w,h);var im=rt.gameObject.AddComponent<Image>();im.color=new Color(0,0,0,.015f);im.raycastTarget=true;rt.gameObject.AddComponent<RectMask2D>();var content=Rect(rt,"Content · 预建行",0,0,w-16,contentHeight);var scroll=rt.gameObject.AddComponent<ScrollRect>();scroll.viewport=rt;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;return content;
        }
        static GameObject Modal(string name,float w,float h)
        {
            var backdrop=Box(canvas,name,0,0,1920,1080,new Color(.005f,.025f,.04f,.84f));backdrop.raycastTarget=true;Box(backdrop.transform,"Panel · 可调整位置尺寸",(1920-w)/2,(1080-h)/2,w,h,Panel);return backdrop.gameObject;
        }
        static Transform Body(GameObject modal) { return modal.transform.GetChild(0); }
        static void Modals()
        {
            view.HelpPanel=Modal("10_Help · 操作说明与账本",1660,860);var help=Body(view.HelpPanel);Label(help,"Title","怎么玩",26,14,1440,45,28);Button(help,"Close","关闭","helpClose",1504,15,130,42);
            Label(help,"Guide","调查三处水域并返回，比较鱼量、观测水温风速，再开市下网。\n活鱼是本金；诱饵单独消耗，每场开市扣一次。\nSpace 暂停后可追加重量，或按 1–100% 的整数比例分批收网。收回的鱼可以立即再次投入。\n收网有短暂冷却，只限制再次收网；下网和追加不受影响。下网、收网各记 0.5%，收市统一缴费。\n停针按 Q，金色最好。拼手速交替按 A / D，或点屏幕按钮；次数不足有损失，长按不计。\n每天两场，每场 90 秒。返回木筏会暂停并保存当前进度，回来接着玩。",26,76,1608,350,22);
            pendingLedger.SetParent(help,false);pendingLedger.anchoredPosition=new Vector2(26,-465);pendingLedger.sizeDelta=new Vector2(1608,362);pendingLedger.gameObject.SetActive(true);
            // Match the authored ledger children to its final panel, once in the editor only.
            var fee=pendingLedger.Find("Fee") as RectTransform;fee.anchoredPosition=new Vector2(540,-10);fee.sizeDelta=new Vector2(1030,31);var rowArea=pendingLedger.Find("Rows") as RectTransform;rowArea.sizeDelta=new Vector2(1568,290);var rowContent=rowArea.Find("Content · 预建行") as RectTransform;rowContent.sizeDelta=new Vector2(1552,1080);foreach(var row in view.LedgerRows)row.rectTransform.sizeDelta=new Vector2(1520,34);
            view.HelpPanel.SetActive(false);
            view.DivePanel=Modal("11_Survey · 下水调查",960,760);var dive=Body(view.DivePanel);Label(dive,"Title","下海调查",28,16,725,46,29);Button(dive,"ReturnTop","返回水面","diveReturn",761,20,171,44);Label(dive,"Guide","下潜后采样。多找几份样本，情报更可靠；记得返回带出情报。",28,76,904,66,20,null,Muted);var sea=Box(dive,"Underwater · 调查区域",28,161,904,259,new Color(.035f,.18f,.24f));for(int i=0;i<3;i++)Label(sea.transform,"Depth_"+i,(i*10+10)+" m",20,50+i*74,100,30,18,null,Muted);view.Diver=Label(sea.transform,"Diver · 可换图像","潜水员",390,30,145,42,25,null,Green).rectTransform;
            string[] keys={"diveDepth","diveStamina","diveSamples"},names={"下潜深度","体力","调查样本"},values={"0 m","100","0 / 3"};for(int i=0;i<3;i++){Label(dive,"Caption_"+keys[i],names[i],28+i*304,441,284,28,18,null,Muted);Label(dive,"Value_"+keys[i],values[i],28+i*304,480,284,48,30,keys[i]);}view.StaminaFill=FillBar(dive,"Stamina",28,547,904,7,Green);Button(dive,"Down","下潜 10 m","diveDown",28,584,274,50);Button(dive,"Scan","搜索 / 采样","diveScan",329,584,274,50);Button(dive,"Return","带回情报并返回","diveReturn",631,584,301,50,true);Label(dive,"Hint","先下潜至 10 m，再搜索鱼群痕迹。",28,665,904,43,20,"diveHint",Muted);view.DivePanel.SetActive(false);
            BuildDebug();
            view.ReturnPanel=Modal("13_Return · 返回确认",750,310);var ret=Body(view.ReturnPanel);Label(ret,"Title","返回木筏？",28,17,694,50,28);Label(ret,"Message","当前捕捞与挑战进度将暂停保存。\n再次进入 Main 时继续，不会重抽行情。",28,88,694,90,22);Button(ret,"Cancel","继续捕鱼","returnCancel",28,218,325,54);Button(ret,"Confirm","保存并返回","returnConfirm",375,218,347,54,true);view.ReturnPanel.SetActive(false);
            view.ResetPanel=Modal("14_Reset · 调试重置确认",750,310);var reset=Body(view.ResetPanel);Label(reset,"Title","重置玩家捕捞存档？",28,17,694,50,28);Label(reset,"Message","清空本玩家版资源、培育与交易记录。\n不会删除木筏、旧捕捞或 HTML 存档。",28,88,694,90,22);Button(reset,"Cancel","取消","resetCancel",28,218,325,54);Button(reset,"Confirm","确认重置","resetConfirm",375,218,347,54);view.ResetPanel.SetActive(false);
            Label(canvas,"15_Toast · 操作提示","",435,983,1050,57,23,"toast",new Color(.98f,.8f,.48f)).alignment=TextAnchor.MiddleCenter;
        }
        static void BuildDebug()
        {
            view.DebugPanel=Modal("12_Debug · 隐藏调试（Ctrl+Shift+D）",1100,920);var root=Body(view.DebugPanel);Label(root,"Title","调试补给与实验控制",28,14,870,43,27);Button(root,"Close","关闭","debugClose",914,14,158,43);Label(root,"Info","调试补给不计盈利。面板打开时暂停，关闭后需手动继续。",28,68,1044,36,18,null,Muted);
            Label(root,"FishLabel","活鱼重量 · 整数 kg",28,121,480,29,18);view.DebugFishInput=InputBox(root,"FishAmount","1000",28,163,317,42);Button(root,"GiveFish","获得活鱼","giveFish",360,163,164,42,true);
            Label(root,"BaitLabel","独立诱饵 · 份",573,121,480,29,18);view.DebugBaitInput=InputBox(root,"BaitAmount","100",573,163,319,42);Button(root,"GiveBait","获得诱饵","giveBait",908,163,164,42,true);
            Label(root,"EnvironmentLabel","模拟岛屿清洁度（局间）",28,229,766,30,20);Label(root,"CleanValue","80",910,229,162,30,20,"debugClean");var sliderRt=Rect(root,"CleanSlider",28,280,808,27);view.CleanSlider=sliderRt.gameObject.AddComponent<Slider>();view.CleanSlider.minValue=0;view.CleanSlider.maxValue=100;view.CleanSlider.wholeNumbers=true;view.CleanSlider.value=80;var sb=Box(sliderRt,"Background",0,8,808,10,Field);sb.raycastTarget=true;var handle=Box(sliderRt,"Handle",0,0,25,27,Green);handle.raycastTarget=true;view.CleanSlider.handleRect=handle.rectTransform;view.CleanSlider.targetGraphic=handle;Button(root,"ApplyEnvironment","应用环境","applyClean",861,272,211,42);
            Label(root,"ShapeLabel","下一场形态（开市前）",28,338,1044,30,20);view.ShapeDropdown=Dropdown(root,28,382,1044,42);
            Button(root,"Survey","完成当前调查","debugSurvey",28,450,330,43);Button(root,"Challenge","触发下一挑战","debugGauge",385,450,330,43);Button(root,"End","立即收市","debugEnd",742,450,330,43);Button(root,"Shark","鲨鱼袭击","debugShark",28,507,507,43);Button(root,"Weather","天气突变","debugWeather",562,507,510,43);
            var toggleRt=Rect(root,"RevealToggle",28,580,1044,35);view.RevealToggle=toggleRt.gameObject.AddComponent<Toggle>();var tbg=Box(toggleRt,"Background",0,0,30,30,Field);tbg.raycastTarget=true;var check=Box(tbg.transform,"Checkmark",6,6,18,18,Green);view.RevealToggle.targetGraphic=tbg;view.RevealToggle.graphic=check;Label(toggleRt,"Label","显示后台参数（只在此面板显示）",45,0,999,35,20);view.RevealToggle.isOn=false;
            Label(root,"Secrets","",28,630,1044,190,18,"debugSecrets",Muted);Button(root,"Reset","重置玩家版存档","reset",28,849,1044,42);
            view.DebugPanel.SetActive(false);
        }
        static Dropdown Dropdown(Transform parent,float x,float y,float w,float h)
        {
            var im=Box(parent,"ShapeDropdown",x,y,w,h,Field);im.raycastTarget=true;var dd=im.gameObject.AddComponent<Dropdown>();dd.targetGraphic=im;dd.captionText=Label(im.transform,"Caption","随机",12,0,w-50,h,20);Label(im.transform,"Arrow","▼",w-36,0,30,h,18);dd.options=new List<Dropdown.OptionData>{new Dropdown.OptionData("随机")};foreach(string name in PlayerMarket.ShapeNames)dd.options.Add(new Dropdown.OptionData(name));
            var template=Box(im.transform,"Template",0,h,w,254,Panel).rectTransform;var viewport=Rect(template,"Viewport",0,0,w,254);viewport.gameObject.AddComponent<RectMask2D>();var content=Rect(viewport,"Content",0,0,w,42);var item=Rect(content,"Item",0,0,w,42);var toggle=item.gameObject.AddComponent<Toggle>();var background=Box(item,"Background",0,0,w,42,Field);background.raycastTarget=true;var selection=Box(item,"Checkmark",4,4,4,34,Green);toggle.targetGraphic=background;toggle.graphic=selection;dd.itemText=Label(item,"ItemLabel","随机",16,0,w-32,42,20);dd.template=template;var scroll=template.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;template.gameObject.SetActive(false);return dd;
        }
        public static void Validate(Scene scene)
        {
            var roots=scene.GetRootGameObjects();var views=roots.SelectMany(r=>r.GetComponentsInChildren<PlayerMarketView>(true)).ToArray();if(views.Length!=1)throw new Exception("Expected one player view");var v=views[0];
            if(roots.SelectMany(r=>r.GetComponentsInChildren<SceneGameHost>(true)).Any()||roots.SelectMany(r=>r.GetComponentsInChildren<GameController>(true)).Any())throw new Exception("Legacy hosts remain in Main");
            if(v.Texts.Any(b=>!b.Target)||v.Buttons.Any(b=>!b.Target)||!v.Chart||!v.Canvas||!v.WeightInput||!v.ShapeDropdown||v.TimingSegments.Length!=7||v.MapEstimates.Length!=3||v.LedgerRows.Length!=30||v.DebugPanel.activeSelf||v.ChallengePanel.activeSelf)throw new Exception("Invalid scene bindings");
            if(v.Texts.GroupBy(t=>t.Key).Any(g=>g.Count()!=1))throw new Exception("Duplicate text key");
        }
        static void Preview(Scene scene,Camera cam,string path)
        {
            var v=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PlayerMarketView>(true)).Single();var render=new RenderTexture(1920,1080,24);var oldMode=v.Canvas.renderMode;var oldCamera=v.Canvas.worldCamera;var oldTarget=cam.targetTexture;var oldActive=RenderTexture.active;Texture2D image=null;
            try{v.Canvas.renderMode=RenderMode.ScreenSpaceCamera;v.Canvas.worldCamera=cam;v.Canvas.planeDistance=10;cam.targetTexture=render;Canvas.ForceUpdateCanvases();cam.Render();RenderTexture.active=render;image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{v.Canvas.renderMode=oldMode;v.Canvas.worldCamera=oldCamera;cam.targetTexture=oldTarget;RenderTexture.active=oldActive;Object.DestroyImmediate(render);if(image)Object.DestroyImmediate(image);}
        }
    }
}
