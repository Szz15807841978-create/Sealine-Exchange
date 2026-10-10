using UnityEngine;

namespace WhaleFall
{
    public sealed class BioMineralNode : MonoBehaviour
    {
        [Header("你放置的四个形态（顺序1、2、3、4）")]
        public GameObject[] stages = new GameObject[4];
        public Collider interactionVolume;
        [Header("开采")]
        [Min(1)] public float bareHandSeconds=100;
        public Vector3 pickaxeMultipliers=new Vector3(1.6f,2.4f,3.5f);
        [Min(.5f)] public float interactionDistance=2.6f;
        [Range(0,1)] public float progress;
        [Header("每开采一层的掉落")]
        [Min(0)] public int leatherPerLayer=2;
        [Min(0)] public int crystalsPerLayer=2;
        [Min(0)] public int bonesPerLayer=1;
        [Min(0)] public int yeastPerLayer=1;
        int paidLayers;
        public bool Depleted=>progress>=1;
        public int Stage=>Mathf.Min(3,Mathf.FloorToInt(progress*4))+1;
        public Vector3 Center=>interactionVolume?interactionVolume.bounds.center:transform.position;
        public float Efficiency(int tier)=>tier==1?pickaxeMultipliers.x:tier==2?pickaxeMultipliers.y:tier==3?pickaxeMultipliers.z:1;
        void Awake() { progress=0;paidLayers=0;RefreshStage(); }
        public Vector3 InteractionPoint(Vector3 player)
        { return interactionVolume?interactionVolume.ClosestPoint(player):Center; }
        public bool CanReach(Underwater3DDiver diver)
        {
            Vector3 point=InteractionPoint(diver.transform.position);
            return !Depleted&&Vector3.Distance(diver.transform.position,point)<=interactionDistance
                &&DivePhysics.Visible(diver.transform.position,point,diver.transform,transform);
        }
        public void Mine(DiveExpedition session,float seconds)
        {
            if(Depleted||!CanReach(session.diver)) return;
            progress=Mathf.Clamp01(progress+seconds*Mathf.Max(.1f,Efficiency(session.inventory.equippedPickaxe))/Mathf.Max(1,bareHandSeconds));
            int layers=Mathf.Min(4,Mathf.FloorToInt(progress*4+.0001f));
            while(paidLayers<layers)
            {
                paidLayers++;
                session.inventory.Add(DiveItem.Leather,leatherPerLayer);
                session.inventory.Add(DiveItem.OceanCrystal,crystalsPerLayer);
                session.inventory.Add(DiveItem.Bone,bonesPerLayer);
                session.inventory.Add(DiveItem.Yeast,yeastPerLayer);
                session.Notify(Depleted?"生物矿开采完成！带着物资上浮撤离。":"开采一层：获得皮革、海洋结晶、骨骼、酵母菌");
            }
            RefreshStage();
        }
        public void RefreshStage()
        { for(int i=0;i<stages.Length;i++) if(stages[i]) stages[i].SetActive(i==Stage-1); }
    }
}
