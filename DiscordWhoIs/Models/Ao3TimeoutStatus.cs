namespace DiscordWhoIs.Models
{
    public class Ao3TimeoutStatus
    {
        public int TimeoutCount { get; set; }

        public DateTime? LateTimeoutUtc { get; set; }

        public string? LastTimeoutMessage { get; set; }

        public bool IsDegraded { get; set; }

        public Ao3TimeoutStatus(
            int timeoutCount,
            DateTime? lateTimeoutUtc,
            string? lastTimeoutMessage,
            bool isDegraded
            )
        {
            TimeoutCount = timeoutCount;
            LateTimeoutUtc = lateTimeoutUtc;
            LastTimeoutMessage = lastTimeoutMessage;
            IsDegraded = isDegraded;
        }
    }
}
