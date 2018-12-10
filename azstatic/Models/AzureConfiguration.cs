using azstatic.Models.subscriptions;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace azstatic.Models
{
    public static class AzureConfiguration
    {
        private static readonly string filename = "azure.json";        

        public static ConfigurationFile GetFromFile()
        {
            if (File.Exists(filename))
            {
                // read file into a string and deserialize JSON to a type
                return JsonConvert.DeserializeObject<ConfigurationFile>(File.ReadAllText(filename));
            }
            return new ConfigurationFile();
        }

        public static async Task SaveToFile(ConfigurationFile configuration)
        {
            using (FileStream fs = File.OpenWrite(filename))
            using (StreamWriter sw = new StreamWriter(fs))
            {
                await sw.WriteAsync(JsonConvert.SerializeObject(configuration));
            }
        }
        public static async Task SetDefaultSubscriptionAsync(Subscription defaultSubscription)
        {
            ConfigurationFile configuration = GetFromFile();
            configuration.SubscriptionId = defaultSubscription.subscriptionId;
            await SaveToFile(configuration);
        }

        public static async Task SetDefaultResourceGroupAsync(string rgName)
        {
            ConfigurationFile configuration = GetFromFile();
            configuration.ResourceGroup = rgName;
            await SaveToFile(configuration);
        }

        public static async Task SetDefaultStorageAccountAsync(string storageAccountName)
        {

            ConfigurationFile configuration = GetFromFile();
            configuration.StorageAccount = storageAccountName;
            await SaveToFile(configuration);
        }
    }
}