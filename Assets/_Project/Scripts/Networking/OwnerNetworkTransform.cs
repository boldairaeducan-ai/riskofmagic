using Unity.Netcode.Components;
using UnityEngine;

namespace RiskOfMagic.Networking
{
    /// <summary>Owner-authoritative NetworkTransform for responsive player movement.</summary>
    [DisallowMultipleComponent]
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
