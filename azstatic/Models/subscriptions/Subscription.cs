namespace azstatic.Models.subscriptions
{
    public class Subscription
    {
        public string id { get; set; }
        public string subscriptionId { get; set; }
        public string displayName { get; set; }
        public string state { get; set; }
        public SubscriptionPolicies subscriptionPolicies { get; set; }
    }
}
