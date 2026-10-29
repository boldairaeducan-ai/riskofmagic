using RiskOfMagic.Core;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Combat
{
    /// <summary>
    /// Everything needed to describe a hit. Serializable so it can be broadcast for feedback (VFX, damage numbers).
    /// </summary>
    public struct DamageInfo : INetworkSerializable
    {
        public float Amount;
        public DamageType Type;
        public Team SourceTeam;
        public bool HasSource;
        public ulong SourceId;
        public bool IsCritical;
        public Vector3 Point;
        public Vector3 Direction;

        public static DamageInfo Create(float amount, DamageType type, NetworkObject source, Team sourceTeam, Vector3 point, Vector3 direction)
        {
            return new DamageInfo
            {
                Amount = amount,
                Type = type,
                SourceTeam = sourceTeam,
                HasSource = source != null,
                SourceId = source != null ? source.NetworkObjectId : 0,
                Point = point,
                Direction = direction
            };
        }

        public bool TryGetSource(out NetworkObject source)
        {
            source = null;
            var manager = NetworkManager.Singleton;
            return HasSource && manager != null && manager.SpawnManager != null
                   && manager.SpawnManager.SpawnedObjects.TryGetValue(SourceId, out source);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Amount);
            serializer.SerializeValue(ref Type);
            serializer.SerializeValue(ref SourceTeam);
            serializer.SerializeValue(ref HasSource);
            serializer.SerializeValue(ref SourceId);
            serializer.SerializeValue(ref IsCritical);
            serializer.SerializeValue(ref Point);
            serializer.SerializeValue(ref Direction);
        }
    }
}
