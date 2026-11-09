using System;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Local banner messages (shown by ItemNotificationWidget): title, description, color.</summary>
    public static class HudAnnouncements
    {
        public static event Action<string, string, Color> Raised;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Raised = null;

        public static void Show(string title, string description, Color color) => Raised?.Invoke(title, description, color);
    }
}
