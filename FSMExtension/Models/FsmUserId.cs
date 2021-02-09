namespace FSMExtension.Models.Fsm
{
    /// <summary>
    /// Convenience wrapper around the various FSM account details.
    /// </summary>
    public class FsmUserId
    {
        public FsmUserId(string cloudHost, string accountId, string companyId, string userId)
        {
            CloudHost = cloudHost;
            AccountId = accountId;
            CompanyId = companyId;
            UserId = userId;
        }

        public string CloudHost { get; }

        public string AccountId { get; }

        public string CompanyId { get; }

        public string UserId { get; }
    }
}
