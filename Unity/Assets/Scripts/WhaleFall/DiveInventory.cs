using System;
using System.Collections.Generic;
using UnityEngine;

namespace WhaleFall
{
    public enum DiveItem { Thread, Pickaxe1, Pickaxe2, Pickaxe3, Wood, IronOre, Leather, OceanCrystal, Bone, Yeast }
    [Serializable] public class DiveItemStack { public DiveItem item; public int count; }
    [Serializable] public class DiveFishCatch { public string species; public float kilograms; }

    public sealed class DiveInventory : MonoBehaviour
    {
        public List<DiveItemStack> items = new List<DiveItemStack>();
        public List<DiveFishCatch> fish = new List<DiveFishCatch>();
        public float FishWeight {get {float total=0;foreach(var catchItem in fish) total+=catchItem.kilograms;return total;}}
        public void AddFish(string species,float kilograms) {fish.Add(new DiveFishCatch{species=species,kilograms=Mathf.Clamp(kilograms,10,80)});}
        [Range(0,3)] public int equippedPickaxe;
        public int Count(DiveItem item) { var stack=items.Find(s=>s.item==item);return stack==null?0:stack.count; }
        public void Add(DiveItem item,int amount)
        {
            if(amount<=0) return;
            var stack=items.Find(s=>s.item==item);
            if(stack==null) { stack=new DiveItemStack {item=item};items.Add(stack); }
            stack.count+=amount;
            int tier=item==DiveItem.Pickaxe1?1:item==DiveItem.Pickaxe2?2:item==DiveItem.Pickaxe3?3:0;
            if(tier>equippedPickaxe) equippedPickaxe=tier;
        }
        public void Equip(int tier)
        {
            if(tier==0 || (tier>=1&&tier<=3&&Count((DiveItem)((int)DiveItem.Pickaxe1+tier-1))>0)) equippedPickaxe=tier;
        }
        public void Clear() { items.Clear();fish.Clear();equippedPickaxe=0; }
        public static string Label(DiveItem item)
        {
            switch(item)
            {
                case DiveItem.Thread:return "线";case DiveItem.Pickaxe1:return "粗制镐 · I级";
                case DiveItem.Pickaxe2:return "精制镐 · II级";case DiveItem.Pickaxe3:return "强化镐 · III级";
                case DiveItem.Wood:return "木头";case DiveItem.IronOre:return "铁矿石";case DiveItem.Leather:return "皮革";
                case DiveItem.OceanCrystal:return "海洋结晶";case DiveItem.Bone:return "骨骼";default:return "酵母菌";
            }
        }
    }
}
