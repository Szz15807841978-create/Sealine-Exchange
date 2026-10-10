using UnityEngine;
using UnityEngine.UI;

namespace Fishing
{
    /// <summary>
    /// 第一阶段场景化宿主：相机、Canvas、EventSystem 和预览曲线常驻场景，
    /// 进入 Play 后暂时仍复用原有 BuildUI，后续可逐块替换为序列化场景对象。
    /// </summary>
    [AddComponentMenu("Fishing/Scene Game Host")]
    public class SceneGameHost : MonoBehaviour
    {
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private GameObject editModePreview;

        private void Awake()
        {
            if (editModePreview != null) editModePreview.SetActive(false);

            var controller = Object.FindObjectOfType<GameController>();
            if (controller == null)
            {
                var go = new GameObject("GameController");
                go.transform.SetParent(transform, false);
                controller = go.AddComponent<GameController>();
            }

            if (targetCanvas == null) targetCanvas = Object.FindObjectOfType<Canvas>();
            if (targetCanvas != null) controller.BuildUI(targetCanvas);
            else Debug.LogError("SceneGameHost 找不到 Canvas，无法搭建运行时界面。", this);

            Bootstrap.Controller = controller;
        }
    }
}
