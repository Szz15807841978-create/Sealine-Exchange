using UnityEngine;

namespace Fishing
{
    /// <summary>Real mesh chest, rendered into the existing 2D scene through a dedicated camera.</summary>
    public sealed class Chest3DView : MonoBehaviour
    {
        public Transform LidHinge;
        public GameObject ChestPanel;
        [Range(0,115)] public float OpenAngle = 105;
        [Tooltip("Use -1 when the imported model's rear hinge opens around negative local X.")]
        public float AxisSign = 1;
        [Min(1)] public float DegreesPerSecond = 240;
        [Tooltip("Preview without opening inventory, also usable on the standalone prefab.")]
        public bool PreviewOpen;
        public bool IsOpen => PreviewOpen || (ChestPanel && ChestPanel.activeSelf);
        private float angle;
        private void LateUpdate()
        {
            SetAngle(Mathf.MoveTowards(angle, IsOpen ? OpenAngle : 0,
                DegreesPerSecond * Time.deltaTime));
        }
        public void SetAngle(float value)
        {
            angle = Mathf.Clamp(value,0,115);
            if(LidHinge) LidHinge.localRotation = Quaternion.Euler(angle*AxisSign,0,0);
        }
    }
}
