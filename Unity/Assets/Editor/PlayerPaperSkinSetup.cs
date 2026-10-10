using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Fishing;
using Fishing.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace FishingEditor
{
    // Appearance-only migration. No scene builders, transform edits, asset generation or backups.
    [InitializeOnLoad]
    public static class PlayerPaperSkinSetup
    {
        const string MainPath="Assets/Scenes/Main.unity",RaftPath="Assets/Scenes/Current/DivePointRealisticOceanSky_Unified.unity";
        const string Request="tools/PlayerPaperSkin.request",Report="tools/PlayerPaperSkin-report.txt",Skin="Assets/Art/UI/Skin/";
        public static readonly Color Ink=Hex("283B3C"),Muted=Hex("565C50"),Teal=Hex("236257"),Rust=Hex("963C2F"),Ochre=Hex("805514"),Paper=Hex("F2E7CB");
        static Sprite panel,inset,button,amber;
        static bool busy;
        static string waitingFor;
        static PlayerPaperSkinSetup() { EditorApplication.update+=Watch; }
        static void Watch()
        {
            if(busy||!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            var dirty=DirtyTarget();
            if(dirty!=null){if(waitingFor!=dirty){waitingFor=dirty;File.WriteAllText(Report,"WAITING: save the open scene first; nothing overwritten.\n"+dirty+"\n");}return;}
            waitingFor=null;
            busy=true;
            try{Apply();File.Move(Request,Request+".done-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));}
            catch(Exception e){File.WriteAllText(Report,"NOT APPLIED / VERIFY DETAILS\n"+e);Debug.LogException(e);EditorApplication.update-=Watch;}
            finally{busy=false;}
        }
        static Color Hex(string hex) { Color c;ColorUtility.TryParseHtmlString("#"+hex,out c);return c; }
        [MenuItem("Fishing/玩家版/仅替换新增 UI 为实心纸墨皮肤（不备份、不改布局）")]
        public static void FromMenu()
        {
            try{Apply();if(File.Exists(Request))File.Move(Request,Request+".done-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));}
            catch(Exception e){File.WriteAllText(Report,"NOT APPLIED / VERIFY DETAILS\n"+e);Debug.LogException(e);}
        }
        static string DirtyTarget()
        {
            foreach(var path in new[]{MainPath,RaftPath}){var s=SceneManager.GetSceneByPath(path);if(s.IsValid()&&s.isLoaded&&s.isDirty)return path;}
            return null;
        }
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请先退出 Play 模式。");
            var dirty=DirtyTarget();if(dirty!=null)throw new InvalidOperationException("请先保存场景；没有覆盖未保存的调整："+dirty);
            panel=LoadSolid("panel_paper");inset=LoadSolid("panel_inset");button=LoadSolid("button_paper");amber=LoadSolid("button_amber");
            var previous=SceneManager.GetActiveScene();var main=SceneManager.GetSceneByPath(MainPath);var raft=SceneManager.GetSceneByPath(RaftPath);
            bool openM=!main.IsValid()||!main.isLoaded,openR=!raft.IsValid()||!raft.isLoaded;
            if(openM)main=EditorSceneManager.OpenScene(MainPath,OpenSceneMode.Additive);
            if(openR)raft=EditorSceneManager.OpenScene(RaftPath,OpenSceneMode.Additive);
            bool valid=false;
            try{
                var view=All<PlayerMarketView>(main).Single();var station=All<RaftMarketStation>(raft).Single();
                if(!view.Canvas||!station.Dialog)throw new InvalidOperationException("Missing existing UI references; no hierarchy was guessed.");
                var roots=new[]{view.Canvas.transform,station.Dialog.transform};
                var beforeM=Snapshot(main,roots);var beforeR=Snapshot(raft,roots);
                var dimmers=new HashSet<Image>(new[]{view.HelpPanel,view.DebugPanel,view.ReturnPanel,view.ResetPanel,station.Dialog}.Where(g=>g).Select(g=>g.GetComponent<Image>()).Where(i=>i));
                var segments=new HashSet<Image>(view.TimingSegments.Where(i=>i));
                foreach(var root in roots){
                    if(!root.GetComponent<PaperInkUiScope>())root.gameObject.AddComponent<PaperInkUiScope>();
                    foreach(var text in root.GetComponentsInChildren<Text>(true)){var c=TextInk(text.color);c.a=text.color.a;text.color=c;EditorUtility.SetDirty(text);}
                    foreach(var im in root.GetComponentsInChildren<Image>(true)){
                        if(im==view.Background){im.sprite=null;im.color=new Color(Paper.r,Paper.g,Paper.b,im.color.a);}
                        else if(dimmers.Contains(im)){im.sprite=null;im.color=new Color(.13f,.12f,.10f,im.color.a);}
                        else if(im.color.a<=.02f||im.GetComponent<Mask>()&&!im.GetComponent<Mask>().showMaskGraphic){ /* invisible hit area / mask, not a paper panel */ }
                        else if(segments.Contains(im)){im.sprite=null;im.color=TextInk(im.color);}
                        else if(im.type==Image.Type.Filled){im.color=Teal; /* preserve fill type, method and amount */ }
                        else if(im.rectTransform.rect.width<=8||im.rectTransform.rect.height<=10||im.name=="Checkmark"||im.rectTransform==view.TimingNeedle){im.sprite=null;im.color=im.rectTransform==view.TimingNeedle?Ink:im.name=="Checkmark"?Teal:Hex("AAA38C");}
                        else if(im.GetComponent<InputField>()){Sliced(im,inset);}
                        else if(im.GetComponent<Button>()||im.GetComponent<Dropdown>()||im.GetComponentInParent<Toggle>(true)||im.name=="Handle"){Sliced(im,button);}
                        else {Sliced(im,im.name.StartsWith("Wallet_")||im.name.StartsWith("Ledger")?inset:panel);}
                        EditorUtility.SetDirty(im);
                    }
                    foreach(var selectable in root.GetComponentsInChildren<Selectable>(true)){
                        var colors=selectable.colors;colors.normalColor=Color.white;colors.highlightedColor=Hex("FFF7DE");colors.selectedColor=Hex("F0D9A8");colors.pressedColor=Hex("D9BD88");colors.disabledColor=Hex("C8C5B9");selectable.colors=colors;
                        var input=selectable as InputField;if(input){input.customCaretColor=true;input.caretColor=Ink;input.selectionColor=new Color(Teal.r,Teal.g,Teal.b,.24f);}
                        EditorUtility.SetDirty(selectable);
                    }
                    foreach(var b in root.GetComponentsInChildren<Button>(true))foreach(var t in b.GetComponentsInChildren<Text>(true)){var ink=Ink;ink.a=t.color.a;t.color=ink;EditorUtility.SetDirty(t);}
                }
                foreach(var binding in view.Buttons){if(new[]{"start","cast","add","collect","next","giveFish","giveBait","returnConfirm","gaugeStop"}.Contains(binding.Action))Sliced(binding.Target.targetGraphic as Image,amber);}
                view.Positive=Teal;view.Negative=Rust;view.Muted=Muted;view.GaugeBest=Hex("CEA344");view.GaugeGood=Hex("6D9D89");view.GaugeNeutral=Hex("AAA38C");
                view.Chart.CurveColor=Teal;view.Chart.ZeroColor=Ink;view.Chart.CostColor=Ochre;view.Chart.EventColor=Rust;view.Chart.GridColor=new Color(Ink.r,Ink.g,Ink.b,.20f);
                EditorUtility.SetDirty(view);EditorUtility.SetDirty(view.Chart);
                AssertSame(beforeM,Snapshot(main,roots),"Main");AssertSame(beforeR,Snapshot(raft,roots),"Raft");
                Validate(view.Canvas.transform);Validate(station.Dialog.transform);
                valid=true;
                EditorSceneManager.MarkSceneDirty(main);EditorSceneManager.MarkSceneDirty(raft);
                if(!EditorSceneManager.SaveScene(main)||!EditorSceneManager.SaveScene(raft))throw new IOException("Could not save styled scenes.");
                File.WriteAllText(Report,"PASS: appearance-only paper/ink migration\nBackupCreated=False\nMainProtectedObjects="+beforeM.Count+"\nRaftProtectedObjects="+beforeR.Count+"\nAllTransformsHierarchyActiveFlagsAndTextContentUnchanged=True\nAllNonUiComponentsAndReferencesUnchanged=True\nInactiveSecondaryPanelsIncluded=True\nSolidSprites=panel_paper,panel_inset,button_paper,button_amber\nSpriteInnerAreaAlphaVerified=True\nLegacyRuntimeThemeCannotOverrideOwnedScopes=True\nStyledImages="+roots.Sum(r=>r.GetComponentsInChildren<Image>(true).Length)+"\nStyledTexts="+roots.Sum(r=>r.GetComponentsInChildren<Text>(true).Length)+"\n");
                Debug.Log("[Paper UI] 实心纸墨皮肤已保存；木筏物件、布局、文本与玩法参数未改，没有创建备份。");
            }finally{
                // On an audit failure leave an originally open scene unsaved for inspection, never overwrite disk.
                if(openM&&main.IsValid()&&main.isLoaded)EditorSceneManager.CloseScene(main,true);
                if(openR&&raft.IsValid()&&raft.isLoaded)EditorSceneManager.CloseScene(raft,true);
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
                if(!valid)Debug.LogWarning("Paper UI audit did not finish; scene files were not saved.");
            }
        }
        static Sprite LoadSolid(string name)
        {
            var path=Skin+name+".png";var s=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(!s)throw new Exception("Missing sprite "+path);
            // Read a temporary texture instead of changing shared importer settings.
            var t=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try{if(!t.LoadImage(File.ReadAllBytes(path)))throw new Exception("Cannot read "+path);for(int y=t.height/3;y<t.height*2/3;y++)for(int x=t.width/3;x<t.width*2/3;x++)if(t.GetPixel(x,y).a<.999f)throw new Exception("Hollow/translucent sprite rejected: "+path);}
            finally{Object.DestroyImmediate(t);}
            return s;
        }
        static void Sliced(Image im,Sprite sprite) { if(!im)return;im.sprite=sprite;im.type=Image.Type.Sliced;im.fillCenter=true;im.pixelsPerUnitMultiplier=1;im.color=Color.white;EditorUtility.SetDirty(im); }
        static Color TextInk(Color c)
        {
            if(c.r>c.g+.15f&&c.g<c.b+.1f)return Rust;
            if(c.r>c.b+.18f&&c.g>c.b+.12f)return Ochre;
            if(c.g>c.r+.18f)return Teal;
            return Mathf.Max(c.r,c.g,c.b)<.82f?Muted:Ink;
        }
        public static void Validate(Transform root)
        {
            if(!root.GetComponent<PaperInkUiScope>())throw new Exception("Missing scope marker");
            foreach(var im in root.GetComponentsInChildren<Image>(true)){
                var path=AssetDatabase.GetAssetPath(im.sprite);
                if(path.StartsWith(Skin)&&!new[]{"panel_paper","panel_inset","button_paper","button_amber"}.Contains(Path.GetFileNameWithoutExtension(path)))throw new Exception("Unexpected / hollow skin: "+im.name+" "+path);
                if(im.type==Image.Type.Sliced&&(!im.fillCenter||im.color.a<.999f))throw new Exception("Unfilled paper panel: "+im.name);
            }
        }
        static T[] All<T>(Scene scene) where T:Component { return scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray(); }
        internal static Dictionary<int,string> Snapshot(Scene scene,Transform[] roots)
        {
            var result=new Dictionary<int,string>();
            foreach(var t in All<Transform>(scene)){
                var g=t.gameObject;result.Add(g.GetInstanceID(),g.name+"|"+g.layer+"|"+g.tag+"|"+g.activeSelf+"|"+g.isStatic+"|"+g.hideFlags+"|"+t.GetSiblingIndex());
                bool owned=roots.Any(r=>t==r||t.IsChildOf(r));
                foreach(var c in t.GetComponents<Component>()){
                    if(!c||c is PaperInkUiScope)continue;
                    var skip=new HashSet<string>();
                    if(owned){
                        if(c is Image)skip.UnionWith(new[]{"m_Color","m_Sprite","m_Type","m_FillCenter","m_PixelsPerUnitMultiplier"});
                        if(c is Text)skip.Add("m_Color");
                        if(c is Selectable)skip.Add("m_Colors");
                        if(c is InputField)skip.UnionWith(new[]{"m_CustomCaretColor","m_CaretColor","m_SelectionColor"});
                        if(c is PlayerMarketView)skip.UnionWith(new[]{"Positive","Negative","Muted","GaugeBest","GaugeGood","GaugeNeutral"});
                        if(c is PlayerMarketChart)skip.UnionWith(new[]{"CurveColor","GridColor","ZeroColor","CostColor","EventColor"});
                    }
                    result.Add(c.GetInstanceID(),ProtectedJson(EditorJsonUtility.ToJson(c),skip)+ReferenceSnapshot(c,skip));
                }
            }
            return result;
        }
        static string ReferenceSnapshot(Component component,HashSet<string> skip)
        {
            // EditorJsonUtility can write instanceID:0 for live scene references. Compare the actual
            // serialized references as well, so a reference change cannot pass the appearance audit.
            var result=new StringBuilder();var serialized=new SerializedObject(component);var p=serialized.GetIterator();
            while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&!skip.Contains(p.propertyPath.Split('.')[0]))result.Append('|').Append(p.propertyPath).Append('=').Append(p.objectReferenceInstanceIDValue);
            return result.ToString();
        }
        // Preserve every serialized field except the explicit top-level appearance whitelist.
        static string ProtectedJson(string json,HashSet<string> skip)
        {
            if(skip.Count==0)return json;
            // EditorJsonUtility wraps script fields in MonoBehaviour, unlike JsonUtility.
            const string wrapper="{\"MonoBehaviour\":";
            if(json.StartsWith(wrapper,StringComparison.Ordinal))return wrapper+ProtectedJson(json.Substring(wrapper.Length,json.Length-wrapper.Length-1),skip)+"}";
            var parts=new List<string>();int start=1,depth=1;bool quoted=false,escaped=false;
            for(int i=1;i<json.Length;i++){
                char ch=json[i];if(quoted){if(escaped)escaped=false;else if(ch=='\\')escaped=true;else if(ch=='"')quoted=false;continue;}
                if(ch=='"'){quoted=true;continue;}
                if(ch=='{'||ch=='[')depth++;else if(ch=='}'||ch==']')depth--;
                if((ch==','&&depth==1)||depth==0){var part=json.Substring(start,i-start);int end=part.IndexOf('"',1);string key=end>0?part.Substring(1,end-1):"";if(!skip.Contains(key))parts.Add(part);start=i+1;}
            }
            return "{"+string.Join(",",parts)+"}";
        }
        internal static void AssertSame(Dictionary<int,string> before,Dictionary<int,string> after,string scene)
        {
            if(before.Count!=after.Count)throw new Exception(scene+" object/component count changed outside the skin marker");
            foreach(var pair in before)if(!after.ContainsKey(pair.Key)||pair.Value!=after[pair.Key])throw new Exception(scene+" protected content changed: "+EditorUtility.InstanceIDToObject(pair.Key)+"\nBefore: "+pair.Value+"\nAfter: "+(after.ContainsKey(pair.Key)?after[pair.Key]:"MISSING"));
        }
    }
}
