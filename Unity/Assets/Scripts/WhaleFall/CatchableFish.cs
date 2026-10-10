using UnityEngine;

namespace WhaleFall
{
    public sealed class CatchableFish : MonoBehaviour
    {
        public string species="海鱼";
        [Header("每条鱼入场时生成一次重量；命中才判定捕获")]
        public Vector2 weightRange=new Vector2(10,80);
        [Range(0,1)] public float lightFishChance=.8f;
        [Range(0,1)] public float heavyFishChance=.35f;
        public float escapeSeconds=4;
        [SerializeField] float kilograms;
        bool caught;
        public float Kilograms=>kilograms;
        void Awake()
        {float lo=Mathf.Clamp(weightRange.x,10,80),hi=Mathf.Clamp(weightRange.y,lo,80);kilograms=Mathf.Round(Random.Range(lo,hi)*10)/10;}
        public void Hit(HarpoonGun gun)
        {
            if(caught||!gun||!gun.session||!gun.session.ActiveRound) return;
            float chance=Mathf.Lerp(lightFishChance,heavyFishChance,Mathf.InverseLerp(10,80,kilograms));
            if(Random.value<chance)
            {
                caught=true;gun.session.inventory.AddFish(species,kilograms);
                gun.session.Notify("捕获 "+species+" · "+kilograms.ToString("0.0")+" kg！记得上浮撤离。");
                gameObject.SetActive(false);Destroy(gameObject);
            }
            else
            {
                var swim=GetComponent<MineralFaunaSwimmer>();if(swim) swim.Flee(gun.transform.position,escapeSeconds);
                gun.session.Notify("命中 "+species+"（"+kilograms.ToString("0.0")+" kg），但它挣脱逃走了！");
            }
            gun.ShowHit(caught);
        }
    }
}
