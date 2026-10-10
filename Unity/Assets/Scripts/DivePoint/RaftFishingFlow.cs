using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Fishing
{
    /// <summary>
    /// 木筏场景与 Main 捕捞玩法之间的无切场景桥接。
    /// Main 的原始玩法 UI 作为透明叠层生成，海洋场景继续充当静态背景。
    /// </summary>
    [AddComponentMenu("鲸落/雨天早晨捕捞流程")]
    [DefaultExecutionOrder(300)]
    public sealed class RaftFishingFlow : MonoBehaviour
    {
        public DivePointController DivePoint;
        public RaftWeatherController Weather;
        public Raft3DPlayerBridge PlayerBridge;
        public Camera OceanCamera;
        public BoxCollider FishingZone;
        public Canvas InteractionCanvas;

        [Header("触发")]
        [Tooltip("优先根据 DivePointController 的当前右侧移动上限计算触发点，避免手调角色坐标后触发器留在旧位置。")]
        public bool FollowPlayerRightLimit = true;
        [Min(0f)] public float RightLimitInset = 35f;
        public float RightEdgeTriggerX = 500f;
        public float RearmDistance = 70f;

        [Header("镜头")]
        public float CameraMoveRight = 8f;
        public float CameraMoveSeconds = 1.35f;

        public static bool FishingActive { get; private set; }
        public bool CanInspectMarketEquipment { get { return _state==FlowState.Waiting||_state==FlowState.Playing; } }

        private enum FlowState { Waiting, Asking, Transitioning, Playing }
        private FlowState _state;
        private bool _armed = true;
        private bool _blockedByWeather;
        private GameObject _questionRoot;
        private GameObject _gateHintRoot;
        private CanvasGroup _gameplayGroup;
        private Scene _gameplayScene;
        private Vector3 _cameraBeforeFishing;
        private bool _hasCameraBeforeFishing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstallInUnifiedRaftScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.name.Equals("DivePointRealisticOceanSky_Unified", System.StringComparison.OrdinalIgnoreCase)) return;
            if (FindObjectOfType<RaftFishingFlow>()) return;
            new GameObject("雨天早晨捕捞流程_运行时").AddComponent<RaftFishingFlow>();
        }

        public static bool CanStartFishing(bool rainy, RaftTimeOfDay timeOfDay)
        {
            return rainy && timeOfDay == RaftTimeOfDay.Day;
        }

        private void Awake()
        {
            FishingActive = false;
            if (!DivePoint) DivePoint = FindObjectOfType<DivePointController>();
            if (!Weather) Weather = FindObjectOfType<RaftWeatherController>();
            if (!PlayerBridge) PlayerBridge = FindObjectOfType<Raft3DPlayerBridge>();
            if (!OceanCamera)
            {
                var cameraObject = GameObject.Find("海洋与天空摄像机");
                if (cameraObject) OceanCamera = cameraObject.GetComponent<Camera>();
                if (!OceanCamera) OceanCamera = Camera.main;
            }
        }

        private void OnDestroy()
        {
            FishingActive = false;
        }

        private void LateUpdate()
        {
            if(Fishing.Player.RaftMarketStation.DialogVisible)return;
            if (_state != FlowState.Waiting || !DivePoint || !PlayerBridge || !FishingZone) return;
            // Transform-driven player has no Rigidbody. Test its feet against the actual
            // oriented trigger volume, independent of physics callbacks and UI coordinates.
            Vector3 feet = FishingZone.transform.InverseTransformPoint(PlayerBridge.transform.position);
            bool inside = FishingZone.enabled && FishingZone.gameObject.activeInHierarchy &&
                new Bounds(FishingZone.center, FishingZone.size).Contains(feet);
            if (!inside)
            {
                _armed = true;
                _blockedByWeather = false;
                return;
            }

            bool gateOpen = Weather && CanStartFishing(Weather.Rainy, Weather.TimeOfDay);
            // 玩家可能先在晴天抵达右端、再原地切换到雨天；条件一旦满足应立即触发，
            // 不要求为了“重新进入触发区”先往左走再回来。
            if (!_armed && _blockedByWeather && gateOpen) _armed = true;
            if (!_armed) return;

            _armed = false;
            if (!gateOpen)
            {
                _blockedByWeather = true;
                string current = Weather
                    ? (Weather.Rainy ? "雨天" : "晴天") + " · " +
                      RaftSkyPresets.TimeNames[Mathf.Clamp((int)Weather.TimeOfDay, 0, RaftSkyPresets.TimeNames.Length - 1)]
                    : "天气未知";
                string message = "只有雨天早上才能开始捕捞（当前：" + current + "）";
                if (DivePoint.Status)
                    DivePoint.Status.text = message;
                else
                    ShowGateHint(message);
                return;
            }

            _blockedByWeather = false;
            ShowQuestion();
        }

        private Canvas ResolveCanvas()
        {
            if (InteractionCanvas) return InteractionCanvas;
            if (DivePoint && DivePoint.Player)
                InteractionCanvas = DivePoint.Player.GetComponentInParent<Canvas>();
            if (!InteractionCanvas && Weather) InteractionCanvas = Weather.GetComponent<Canvas>();
            return InteractionCanvas;
        }

        private void OnDrawGizmosSelected()
        {
            if (!FishingZone) return;
            Gizmos.matrix = FishingZone.transform.localToWorldMatrix;
            Gizmos.color = new Color(0f, 1f, 0.6f, 0.25f);
            Gizmos.DrawCube(FishingZone.center, FishingZone.size);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(FishingZone.center, FishingZone.size);
        }

        private void ShowGateHint(string message)
        {
            Canvas canvas = ResolveCanvas();
            if (!canvas) return;
            if (_gateHintRoot) Destroy(_gateHintRoot);

            var panel = Panel(canvas.transform, "捕捞条件提示", new Vector2(640f, 58f), new Color(0.04f, 0.10f, 0.14f, 0.94f));
            UiSkinTheme.ApplyPanel(panel.GetComponent<Image>(), detailed: true);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = new Vector2(0f, -78f);
            panel.SetAsLastSibling();
            MakeText(panel, "文字", message, 17, FontStyle.Bold, Vector2.zero, panel.sizeDelta,
                new Color(1f, 0.80f, 0.48f));
            _gateHintRoot = panel.gameObject;
            StartCoroutine(HideGateHintAfterDelay());
        }

        private IEnumerator HideGateHintAfterDelay()
        {
            yield return new WaitForSecondsRealtime(2.4f);
            if (_gateHintRoot) Destroy(_gateHintRoot);
        }

        private void ShowQuestion()
        {
            _state = FlowState.Asking;
            if (DivePoint) DivePoint.enabled = false;

            Canvas canvas = ResolveCanvas();
            if (!canvas)
            {
                if (DivePoint) DivePoint.enabled = true;
                _state = FlowState.Waiting;
                return;
            }

            _questionRoot = new GameObject("是否开始捕捞", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var root = (RectTransform)_questionRoot.transform;
            root.SetParent(canvas.transform, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            _questionRoot.GetComponent<Image>().color = new Color(0.01f, 0.035f, 0.055f, 0.64f);
            root.SetAsLastSibling();

            var panel = Panel(root, "确认面板", new Vector2(520f, 238f), new Color(0.055f, 0.12f, 0.16f, 0.97f));
            UiSkinTheme.ApplyPanel(panel.GetComponent<Image>(), detailed: true);
            MakeText(panel, "标题", "是否开始捕捞？", 28, FontStyle.Bold, new Vector2(0f, 68f), new Vector2(460f, 46f), Palette.Text);
            MakeText(panel, "条件", "雨天早上 · 已抵达木筏右侧", 16, FontStyle.Normal, new Vector2(0f, 22f), new Vector2(460f, 34f), Palette.Muted);
            MakeButton(panel, "暂不", new Vector2(-118f, -65f), Widgets.Hue.Normal, CancelQuestion);
            MakeButton(panel, "开始捕捞", new Vector2(118f, -65f), Widgets.Hue.Primary, ConfirmFishing);
        }

        private void CancelQuestion()
        {
            if (_questionRoot) Destroy(_questionRoot);
            if (DivePoint) DivePoint.enabled = true;
            _blockedByWeather = false;
            _state = FlowState.Waiting;
        }

        private void ConfirmFishing()
        {
            if (_state != FlowState.Asking) return;
            if (!Weather || !CanStartFishing(Weather.Rainy, Weather.TimeOfDay))
            {
                CancelQuestion();
                _armed = true;
                return;
            }
            if (_questionRoot) Destroy(_questionRoot);
            StartCoroutine(BeginFishing());
        }

        private IEnumerator BeginFishing()
        {
            _state = FlowState.Transitioning;
            FishingActive = true;

            if (DivePoint)
            {
                DivePoint.CloseChest();
                if (DivePoint.ChestPanel) DivePoint.ChestPanel.SetActive(false);
                if (DivePoint.BackpackPanel) DivePoint.BackpackPanel.SetActive(false);
                if (DivePoint.Prompt) DivePoint.Prompt.gameObject.SetActive(false);
                DivePoint.enabled = false;
            }
            if (Weather) Weather.enabled = false;
            if (PlayerBridge) PlayerBridge.SetFishingPose(true);

            Vector3 cameraStart = OceanCamera ? OceanCamera.transform.position : Vector3.zero;
            _cameraBeforeFishing = cameraStart;
            _hasCameraBeforeFishing = OceanCamera != null;
            Vector3 cameraEnd = OceanCamera
                // The unified ocean RenderTexture is displayed with a horizontal mirror.
                // Moving the physical camera along -right therefore reads as screen-right.
                ? cameraStart - OceanCamera.transform.right * Mathf.Abs(CameraMoveRight)
                : cameraStart;
            float elapsed = 0f;
            while (elapsed < CameraMoveSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.05f, CameraMoveSeconds)));
                if (OceanCamera) OceanCamera.transform.position = Vector3.LerpUnclamped(cameraStart, cameraEnd, t);
                yield return null;
            }

            yield return LoadMainGameplayScene();
            if (!_gameplayGroup)
            {
                FishingActive = false;
                _state = FlowState.Waiting;
                if (DivePoint) DivePoint.enabled = true;
                if (Weather) Weather.enabled = true;
                if (PlayerBridge) PlayerBridge.SetFishingPose(false);
                if (OceanCamera && _hasCameraBeforeFishing) OceanCamera.transform.position = _cameraBeforeFishing;
                ShowGateHint("Main 捕捞玩法加载失败，请查看 Console");
                yield break;
            }
            elapsed = 0f;
            while (elapsed < 0.45f)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_gameplayGroup) _gameplayGroup.alpha = Mathf.Clamp01(elapsed / 0.45f);
                yield return null;
            }
            if (_gameplayGroup) _gameplayGroup.alpha = 1f;
            _state = FlowState.Playing;
        }

        private IEnumerator LoadMainGameplayScene()
        {
            _gameplayScene = SceneManager.GetSceneByName("Main");
            if (!_gameplayScene.IsValid() || !_gameplayScene.isLoaded)
            {
                AsyncOperation load = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Additive);
                if (load == null)
                {
                    Debug.LogError("[鲸落] 无法开始加载 Main 场景。请确认它已加入 Build Settings。", this);
                    yield break;
                }
                while (!load.isDone) yield return null;
                _gameplayScene = SceneManager.GetSceneByName("Main");
            }
            if (!_gameplayScene.IsValid() || !_gameplayScene.isLoaded)
            {
                Debug.LogError("[鲸落] Main 场景 Additive 加载完成后仍不可用。", this);
                yield break;
            }

            Canvas canvas = null;
            foreach (GameObject root in _gameplayScene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
                foreach (EventSystem eventSystem in root.GetComponentsInChildren<EventSystem>(true)) eventSystem.enabled = false;
                if (!canvas) canvas = root.GetComponentInChildren<Canvas>(true);
            }
            if (!canvas)
            {
                Debug.LogError("[鲸落] Main 场景已加载，但找不到 UICanvas。", this);
                yield break;
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 200;
            _gameplayGroup = canvas.GetComponent<CanvasGroup>();
            if (!_gameplayGroup) _gameplayGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            _gameplayGroup.alpha = 0f;
            _gameplayGroup.interactable = true;
            _gameplayGroup.blocksRaycasts = true;
            canvas.gameObject.SetActive(true);
            Debug.Log("[鲸落] Main 捕捞玩法已叠加，木筏场景继续作为背景。", this);
        }

        /// <summary>Player Main exits through this bridge; no raft scene object or weather setting is rebuilt.</summary>
        public void ReturnFromPlayerMarket()
        {
            if (_state != FlowState.Playing) return;
            StartCoroutine(ReturnToRaft());
        }

        private IEnumerator ReturnToRaft()
        {
            _state = FlowState.Transitioning;
            var playerMarket = FindObjectOfType<Fishing.Player.PlayerMarketController>();
            if (playerMarket) playerMarket.PrepareForLeave();
            if (_gameplayGroup)
            {
                _gameplayGroup.interactable = false;
                _gameplayGroup.blocksRaycasts = false;
                _gameplayGroup.alpha = 0f;
            }
            if (_gameplayScene.IsValid() && _gameplayScene.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(_gameplayScene);
                if (unload != null) while (!unload.isDone) yield return null;
            }
            Vector3 from = OceanCamera ? OceanCamera.transform.position : Vector3.zero;
            float elapsed = 0f;
            while (_hasCameraBeforeFishing && OceanCamera && elapsed < CameraMoveSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(.05f, CameraMoveSeconds)));
                OceanCamera.transform.position = Vector3.LerpUnclamped(from, _cameraBeforeFishing, t);
                yield return null;
            }
            if (OceanCamera && _hasCameraBeforeFishing) OceanCamera.transform.position = _cameraBeforeFishing;
            if (PlayerBridge) PlayerBridge.SetFishingPose(false);
            if (DivePoint) DivePoint.enabled = true;
            if (Weather) Weather.enabled = true;
            _gameplayGroup = null;
            FishingActive = false;
            _armed = false; // Leave the trigger before offering another trip.
            _blockedByWeather = false;
            _state = FlowState.Waiting;
        }

        private static RectTransform Panel(Transform parent, string name, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return rt;
        }

        private static Text MakeText(Transform parent, string name, string value, int size, FontStyle style, Vector2 pos, Vector2 box, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var text = go.GetComponent<Text>();
            text.font = FontUtil.Get();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            return text;
        }

        private static void MakeButton(Transform parent, string label, Vector2 pos, Widgets.Hue hue, UnityEngine.Events.UnityAction action)
        {
            var panel = Panel(parent, label, new Vector2(190f, 52f), Color.white);
            panel.anchoredPosition = pos;
            UiSkinTheme.ApplyButton(panel.GetComponent<Image>(), hue);
            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = panel.GetComponent<Image>();
            button.onClick.AddListener(action);
            Color textColor = hue == Widgets.Hue.Primary ? Palette.BtnPrimaryText : Palette.BtnCyanText;
            MakeText(panel, "文字", label, 18, FontStyle.Bold, Vector2.zero, panel.sizeDelta, textColor);
        }
    }
}
