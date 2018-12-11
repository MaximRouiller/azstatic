namespace azstatic.Models
{
    public class ConfigurationFile
    {
        public string SubscriptionId { get; set; }
        public string ResourceGroup { get; set; }
        public string StorageAccount { get; set; }
        public string AzureCDNName { get; set; }
    }
}