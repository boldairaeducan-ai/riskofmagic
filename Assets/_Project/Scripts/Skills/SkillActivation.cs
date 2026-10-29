using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Skills
{
    public enum SkillActivationMode : byte
    {
        /// <summary>Fires once per press.</summary>
        Press,
        /// <summary>Fires repeatedly while held (rate limited by cast duration).</summary>
        Hold,
        /// <summary>Charges while held, fires on release with Charge in [0,1].</summary>
        Charge
    }

    /// <summary>Owner-computed parameters of a cast, replicated to the server and other peers.</summary>
    public struct SkillActivation : INetworkSerializable
    {
        public Vector3 Origin;
        public Vector3 Direction;
        public Vector3 Target;
        public float Charge;
        public byte Step;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Origin);
            serializer.SerializeValue(ref Direction);
            serializer.SerializeValue(ref Target);
            serializer.SerializeValue(ref Charge);
            serializer.SerializeValue(ref Step);
        }
    }
}
