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
        /// <summary>Null when online play is unavailable (package not installed).</summary>
        public static IOnlineSessionService Service { get; set; }
        public static bool IsAvailable => Service != null;
    }
}
