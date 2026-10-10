using System;
using UnityEngine;

namespace Fishing.Player
{
    // Unity's inline serialization does not reliably preserve null in nested serializable classes.
    // Explicit presence flags prevent phantom positions, surveys or challenges after reload.
    [Serializable] public class PlayerMarketSaveData
    {
        public int version=1;
        public MarketState state;
        public bool position,gauge,shape,result;
        public bool[] surveys=new bool[3];
    }
    public static class PlayerMarketSave
    {
        public static string Encode(MarketState state)
        {
            var data=new PlayerMarketSaveData{state=state,position=state.position!=null,gauge=state.gauge!=null,shape=state.shape!=null,result=state.result!=null};
            for(int i=0;i<3;i++)data.surveys[i]=state.maps[i].survey!=null;
            return JsonUtility.ToJson(data);
        }
        public static MarketState Decode(string json)
        {
            var data=JsonUtility.FromJson<PlayerMarketSaveData>(json);
            if(data==null||data.version!=1||data.state==null||data.surveys==null||data.surveys.Length!=3||data.state.maps==null||data.state.maps.Count!=3)throw new ArgumentException("Invalid player save envelope");
            var s=data.state;if(!data.position)s.position=null;if(!data.gauge)s.gauge=null;if(!data.shape)s.shape=null;if(!data.result)s.result=null;s.dive=null;
            for(int i=0;i<3;i++)if(!data.surveys[i])s.maps[i].survey=null;
            if(!PlayerMarket.ValidState(s))throw new ArgumentException("Invalid player save state");return s;
        }
    }
}
