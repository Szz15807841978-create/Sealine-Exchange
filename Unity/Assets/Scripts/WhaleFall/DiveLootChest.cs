using System;
using System.Collections.Generic;
using UnityEngine;

namespace WhaleFall
{
    public sealed class DiveLootChest : MonoBehaviour
    {
        public List<DiveItemStack> contents=new List<DiveItemStack>();
        public Transform lid;
        public bool opened;
        public float interactionDistance=2.6f;
        public void Roll(System.Random rng,int index)
        {
            contents.Clear();
            // Every round includes a basic pickaxe; higher tiers remain random discoveries.
            if(index==0) Add(DiveItem.Pickaxe1,1);
            else { double roll=rng.NextDouble();if(roll<.12) Add(DiveItem.Pickaxe3,1);else if(roll<.40) Add(DiveItem.Pickaxe2,1);else if(roll<.65) Add(DiveItem.Pickaxe1,1); }
            var pool=new[]{DiveItem.Thread,DiveItem.Wood,DiveItem.IronOre,DiveItem.Leather};
            int rolls=rng.Next(2,5);
            for(int i=0;i<rolls;i++) Add(pool[rng.Next(pool.Length)],rng.Next(1,5));
        }
        void Add(DiveItem item,int count) { var s=contents.Find(x=>x.item==item);if(s==null) contents.Add(new DiveItemStack {item=item,count=count});else s.count+=count; }
        public bool CanReach(Underwater3DDiver diver)
        { return !opened&&Vector3.Distance(diver.transform.position,transform.position+Vector3.up*.5f)<interactionDistance&&DivePhysics.Visible(diver.transform.position,transform.position+Vector3.up*.5f,diver.transform,transform); }
        public void Open(DiveExpedition session)
        {
            if(!CanReach(session.diver)) return;
            opened=true;
            session.EmitNoise(transform.position,session.chestNoiseRadius);
            var summary=new List<string>();
            foreach(var stack in contents) { session.inventory.Add(stack.item,stack.count);summary.Add(DiveInventory.Label(stack.item)+" ×"+stack.count); }
            session.Notify("获得："+string.Join("、",summary));
        }
        void Update() { if(lid&&opened) lid.localRotation=Quaternion.RotateTowards(lid.localRotation,Quaternion.Euler(-100,0,0),Time.deltaTime*130); }
    }
}
