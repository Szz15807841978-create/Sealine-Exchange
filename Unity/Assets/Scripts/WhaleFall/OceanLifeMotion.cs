using UnityEngine;

namespace WhaleFall
{
    // The visual is a child so different model forward axes can be corrected in the scene.
    public sealed class OceanLifeMotion : MonoBehaviour
    {
        public Vector3 center;
        public Vector2 radius = new Vector2(5, 3);
        public float speed = .22f;
        public float phase;
        public float bob = .35f;
        void Update()
        {
            float t = Time.time * speed + phase;
            transform.position = center + new Vector3(Mathf.Sin(t) * radius.x, Mathf.Sin(t * 1.7f) * bob, Mathf.Cos(t) * radius.y);
            Vector3 tangent = new Vector3(Mathf.Cos(t) * radius.x, Mathf.Cos(t * 1.7f) * bob * 1.7f, -Mathf.Sin(t) * radius.y);
            if (tangent.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
        }
    }
}
