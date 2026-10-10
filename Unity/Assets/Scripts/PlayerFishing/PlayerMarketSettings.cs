using UnityEngine;
namespace Fishing.Player
{
    [CreateAssetMenu(menuName="Fishing/Player Market Settings")]
    public sealed class PlayerMarketSettings : ScriptableObject
    {
        [Tooltip("玩家版数值；不会在普通玩家界面显示。修改仅用于后续调试。")]
        public MarketRules Rules=new MarketRules();
    }
}
