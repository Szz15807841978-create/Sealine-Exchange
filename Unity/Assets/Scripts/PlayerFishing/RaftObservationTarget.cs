using UnityEngine;

namespace Fishing.Player
{
    public enum RaftObservationKind { Station, Wind, Temperature, Rumor }

    [AddComponentMenu("Fishing/Player/Raft Observation Target")]
    public sealed class RaftObservationTarget : MonoBehaviour
    {
        public RaftMarketStation Station;
        public RaftObservationKind Kind;
        public string Caption;
        public Transform LabelAnchor;
        public void Inspect() { if(Station)Station.Open(Kind); }
    }
}
