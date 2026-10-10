using UnityEngine;

namespace WhaleFall
{
    public sealed class UnderwaterAmbientFish : MonoBehaviour
    {
        public Vector3 center;
        public float radius = 5;
        public float speed = .3f;
        public float phase;
        void Update()
        {
            float t = Time.time * speed + phase;
            transform.position = center + new Vector3(Mathf.Sin(t)*radius, Mathf.Sin(t*1.7f)*.6f, Mathf.Cos(t)*.5f);
            transform.rotation = Quaternion.Euler(0, Mathf.Cos(t)>0 ? 90 : -90, Mathf.Cos(t*1.7f)*5);
        }
    }
}
