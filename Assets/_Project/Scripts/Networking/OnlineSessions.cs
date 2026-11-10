using System.Threading.Tasks;

namespace RiskOfMagic.Networking
{
    /// <summary>Online play through join codes. Implemented by the RiskOfMagic.Online assembly when its package is installed.</summary>
    public interface IOnlineSessionService
    {
        string JoinCode { get; }

        /// <summary>Creates a private online game and starts hosting. Returns the join code.</summary>
        Task<string> HostAsync(int maxPlayers);

        /// <summary>Joins a game by code and starts the client.</summary>
        Task JoinAsync(string code);

        Task LeaveAsync();
    }

    public static class OnlineSessions
    {
        private static int servicePriority = int.MinValue;

        /// <summary>Null when online play is unavailable (no backend package installed / initialized).</summary>
        public static IOnlineSessionService Service { get; private set; }
        public static bool IsAvailable => Service != null;

        /// <summary>Optional (Steam): opens the platform's friend invite dialog for the current game.</summary>
        public static System.Action InviteFriends { get; set; }

        /// <summary>A join code received from outside the menu (Steam invite, launch argument), consumed by the menu.</summary>
        public static string PendingJoinCode { get; set; }

        /// <summary>Raised when <see cref="PendingJoinCode"/> is set while the game runs.</summary>
        public static event System.Action JoinRequested;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Service = null;
            servicePriority = int.MinValue;
            InviteFriends = null;
            PendingJoinCode = null;
            JoinRequested = null;
        }

        /// <summary>Backends register at startup; the highest priority wins (Steam over Unity Gaming Services).</summary>
        public static void Register(IOnlineSessionService service, int priority)
        {
            if (service == null || priority < servicePriority)
                return;
            Service = service;
            servicePriority = priority;
        }

        public static void RequestJoin(string code)
        {
            PendingJoinCode = code;
            JoinRequested?.Invoke();
        }
    }
}
