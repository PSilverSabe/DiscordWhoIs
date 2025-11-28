namespace DiscordWhoIs.Models
{
    public class Ao3ThrottleStatus
    {
        public bool IsThrottled { get; set; }

        public TimeSpan ThrottleResetTime { get; set; }

        public TimeSpan TimeUntilNextAllowed { get; set; }

        public int RemainingRequests { get; set; }

        public Ao3ThrottleStatus(
            bool isThrottled,
            TimeSpan throttleResetTime,
            TimeSpan timeUntilNextAllowed,
            int remainingRequests
            )
        {
            IsThrottled = isThrottled;
            ThrottleResetTime = throttleResetTime;
            TimeUntilNextAllowed = timeUntilNextAllowed;
            RemainingRequests = remainingRequests;
        }
    }
}
