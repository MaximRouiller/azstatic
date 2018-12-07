using Newtonsoft.Json;
using StaticSiteQuickstart.Models.subscriptions;
using System;
using System.IO;

namespace StaticSiteQuickstart.Models
{
    public static class AzureConfiguration
    {
        public class Configuration
        {
            public string SubscriptionId { get; set; }
            public string ResourceGroup { get; set; }
            public string StorageAccount { get; set; }
        }

        private static readonly string filename = "azure.json";
        

        public static Configuration GetFromFile()
        {
            if (File.Exists(filename))
            {
                // read file into a string and deserialize JSON to a type
                return JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(filename));
            }
            return new Configuration();
        }

        public static void SaveToFile(Configuration configuration)
        {
            using (FileStream fs = File.OpenWrite(filename))
            using (StreamWriter sw = new StreamWriter(fs))
            {
                sw.Write(JsonConvert.SerializeObject(configuration));
            }
        }
        public static void SetDefaultSubscription(Subscription defaultSubscription)
        {
            Configuration configuration = GetFromFile();
            configuration.SubscriptionId = defaultSubscription.subscriptionId;
            SaveToFile(configuration);
        }

        public static void SetDefaultResourceGroup(string rgName)
        {
            Configuration configuration = GetFromFile();
            configuration.ResourceGroup = rgName;
            SaveToFile(configuration);
        }
    }
}