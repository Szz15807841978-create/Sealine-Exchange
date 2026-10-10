using UnityEngine;
using UnityEngine.SceneManagement;

namespace WhaleFall
{
    public sealed class OceanSceneSelector : MonoBehaviour
    {
        static readonly string[] Scenes = { "UnderwaterExplore3D", "Underwater_LowPolyReef_3D", "Underwater_LifeDeluxe_3D", "Underwater_Corals2_3D", "Underwater_SunkenShip_3D" };
        static readonly string[] Names = { "F1  原始海底 · 搜打撤", "F2  低多边形沉船礁", "F3  生物矿禁区", "F4  珊瑚贝壳海底", "F5  沉船遗迹 · 搜打撤" };
        public string sceneTitle;
        public Vector3[] verificationWaypoints;
        GUIStyle buttonStyle;
        Font font;
        bool loading;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Connect()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
            Attach();
        }
        static void SceneLoaded(Scene scene, LoadSceneMode mode) { Attach(); }
        static void Attach()
        {
            var diver = FindObjectOfType<Underwater3DDiver>();
            if (diver && !FindObjectOfType<OceanSceneSelector>()) diver.gameObject.AddComponent<OceanSceneSelector>();
        }
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) Switch(0);
            if (Input.GetKeyDown(KeyCode.F2)) Switch(1);
            if (Input.GetKeyDown(KeyCode.F3)) Switch(2);
            if (Input.GetKeyDown(KeyCode.F4)) Switch(3);
            if (Input.GetKeyDown(KeyCode.F5)) Switch(4);
        }
        void Switch(int index)
        {
            if (loading || SceneManager.GetActiveScene().name == Scenes[index]) return;
            if (!Application.CanStreamedLevelBeLoaded(Scenes[index])) { Debug.LogWarning("Scene is not in Build Settings: " + Scenes[index]); return; }
            var expedition=GetComponent<DiveExpedition>();
            if(expedition&&expedition.ActiveRound) {expedition.RequestSceneSwitch(Scenes[index]);return;}
            loading = true;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            SceneManager.LoadSceneAsync(Scenes[index], LoadSceneMode.Single);
        }
        void OnGUI()
        {
            if (buttonStyle == null)
            {
                font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 16);
                buttonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 16 };
            }
            float scale = Mathf.Max(.65f, Screen.height / 900f);
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float right = Screen.width / scale - 254;
            for (int i = 0; i < Scenes.Length; i++)
            {
                GUI.enabled = !loading && SceneManager.GetActiveScene().name != Scenes[i];
                if (GUI.Button(new Rect(right, 24 + i * 43, 230, 36), Names[i], buttonStyle)) Switch(i);
            }
            GUI.enabled = true;
            GUI.matrix = old;
        }
        void OnDestroy() { if (font) Destroy(font); }
    }
}
