using UnityEngine;

namespace WhaleFall
{
    /// <summary>
    /// 搜打撤场景宿主：场景里常驻相机 / Canvas / EventSystem，
    /// 进入 Play 后由它创建 DiveGameController 并搭建灰盒与 HUD。
    /// 对应捕捞游戏的 SceneGameHost 模式。
    /// </summary>
    [AddComponentMenu("WhaleFall/Dive Game Host")]
    public class DiveGameHost : MonoBehaviour
    {
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private Camera worldCamera;

        private void Awake()
        {
            var controller = Object.FindObjectOfType<DiveGameController>();
            if (controller == null)
            {
                var go = new GameObject("DiveGameController");
                go.transform.SetParent(transform, false);
                controller = go.AddComponent<DiveGameController>();
            }

            if (targetCanvas == null) targetCanvas = Object.FindObjectOfType<Canvas>();
            if (worldCamera == null) worldCamera = Camera.main;
            if (targetCanvas == null) { Debug.LogError("DiveGameHost 找不到 Canvas", this); return; }
            if (worldCamera == null) { Debug.LogError("DiveGameHost 找不到相机", this); return; }

            controller.Build(targetCanvas, worldCamera);
        }
    }
}
