using azstatic.ConsoleComponents;
using azstatic.Models;
using azstatic.Models.storage;
using azstatic.Models.subscriptions;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Shared.Protocol;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace azstatic
{
    class Program
    {
        private static Random random = new Random();
        static async Task Main(string[] args)
        {
            if (args.Length == 0) { Console.WriteLine("Missing parameters"); return; }

            HttpClient client = null;
            if (File.Exists(".token"))
            {
                string rawToken = File.ReadAllText(".token");
                Token token = JsonConvert.DeserializeObject<Token>(rawToken);

                client = new HttpClient();
                client.BaseAddress = new Uri("https://management.azure.com");
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.access_token);
                client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("azstatic", "0.0.1"));
            }
            string action = args[0];
            if (action == "login")
            {
                Console.WriteLine("We have opened a browser window for you to login. As soon as the authentication is completed, you can close your browser window.");
                OpenBrowser($"http://login.microsoftonline.com/common/oauth2/authorize?response_type=code&client_id={Common.ClientId}&redirect_uri={WebUtility.UrlEncode(Common.ReplyUrl)}&state=code&resource=https%3a%2f%2fmanagement.azure.com%2f&prompt=select_account");
                // start kestrel and ... somehow... retrieve/save the authentication flow? 🤷‍♂️
                CreateWebHostBuilder().Build().Run();
                Console.WriteLine("Authentication completed.");
            }

            if (action == "init")
            {
                if (client == null)
                {
                    Console.WriteLine("Please use the `azstatic login` before initializing resources.");
                    return;
                }

                // select subscription
                ConfigurationFile config = AzureConfiguration.GetFromFile();
                if (string.IsNullOrWhiteSpace(config.SubscriptionId))
                {
                    Subscription subscription = await SelectSubcriptionAsync(client);

                    Console.Clear();
                    Console.WriteLine($"The selected subscription is: {subscription.displayName}");
                }
                else
                {
                    Console.WriteLine($"Existing subscription found: {config.SubscriptionId}");
                }

                // select resource group
                config = AzureConfiguration.GetFromFile();
                if (string.IsNullOrWhiteSpace(config.ResourceGroup))
                {
                    //todo: allow to specify RG name
                    string rgName = $"azstatic-{RandomString(10)}-prod-rg";

                    await CreateResourceGroupAsync(rgName, client, config);
                    Console.WriteLine($"Resource group {rgName} created.");
                }
                else
                {
                    await CreateResourceGroupAsync(config.ResourceGroup, client, config);
                    Console.WriteLine($"Existing resource group updated: {config.ResourceGroup}");
                }

                // select storage account name
                config = AzureConfiguration.GetFromFile();
                if (string.IsNullOrWhiteSpace(config.StorageAccount))
                {
                    string storageAccountName = $"azstatic{RandomString(10)}";
                    await CreateStorageAccountAsync(storageAccountName, client, config);
                    Console.WriteLine($"Storage account {storageAccountName} created.");
                }
                else
                {
                    await CreateStorageAccountAsync(config.StorageAccount, client, config);
                    Console.WriteLine($"Existing storage account updated: {config.StorageAccount}");
                }

                // retrieve storage key and use it to enable static website
                string storageKey = await GetAzureStorageKey(config, client);
                await SetAzureStorageServiceProperties(storageKey, config);

                // retrieve web endpoint
                string storageUrl = await GetAzureStorageUriAsync(config, client);

                config = AzureConfiguration.GetFromFile();
                if (string.IsNullOrWhiteSpace(config.AzureCDNName))
                {
                    string cdnName = config.StorageAccount;
                    await CreateAzureCDNProfile(cdnName, client, config);
                    await CreateAzureCDNEndpoint(cdnName, storageUrl, client, config);
                    Console.WriteLine($"Azure CDN endpoint {cdnName} created.");
                }
                else
                {
                    await CreateAzureCDNProfile(config.AzureCDNName, client, config);
                    await CreateAzureCDNEndpoint(config.AzureCDNName, storageUrl, client, config);
                    Console.WriteLine($"Existing storage account updated: {config.StorageAccount}");
                }

                Console.WriteLine("Website provisioned properly and accessible on: ");
                Console.WriteLine($"\t{storageUrl}");
            }

            if (action == "deploy")
            {
                string pathOfStaticSite = ".";
                if (args.Length == 2)
                {
                    if (Directory.Exists(args[1]))
                    {
                        pathOfStaticSite = args[1];
                    }
                }
                ConfigurationFile config = AzureConfiguration.GetFromFile();

                // retrieve storage key to allow upload
                string storageKey = await GetAzureStorageKey(config, client);
                await UploadPathToAzureStorage(pathOfStaticSite, storageKey, config, client);
            }
        }

        private static async Task CreateAzureCDNProfile(string azureCDNName, HttpClient client, ConfigurationFile config)
        {
            string template = "{\"location\": \"WestCentralUs\",\"sku\": {\"name\":\"Standard_Verizon\"}}";

            HttpResponseMessage result = await client.PutAsync($"/subscriptions/{config.SubscriptionId}/resourcegroups/{config.ResourceGroup}/providers/Microsoft.Cdn/profiles/{azureCDNName}/?api-version=2017-10-12", new StringContent(template, Encoding.UTF8, "application/json"));

            string requestContent = await result.RequestMessage.Content.ReadAsStringAsync();
            HttpRequestMessage request = result.RequestMessage;
            string content = await result.Content.ReadAsStringAsync();
            if (result.IsSuccessStatusCode)
            {
                //await AzureConfiguration.SetDefaultCDNNameAsync(azureCDNName);
            }
        }

        private static async Task CreateAzureCDNEndpoint(string azureCDNName, string storageUrl, HttpClient client, ConfigurationFile config)
        {

            string validHostName = new Uri(storageUrl).DnsSafeHost;
            string template = $"{{ \"location\": \"WestCentralUs\", \"properties\": {{ \"origins\": [{{\"name\": \"{azureCDNName}-origin\",\"properties\": {{\"hostName\": \"{validHostName}\",\"httpPort\": 80,\"httpsPort\": 443}}}}]}}}}";

            HttpResponseMessage result = await client.PutAsync($"/subscriptions/{config.SubscriptionId}/resourcegroups/{config.ResourceGroup}/providers/Microsoft.Cdn/profiles/{azureCDNName}/endpoints/{azureCDNName}?api-version=2017-10-12", new StringContent(template, Encoding.UTF8, "application/json"));

            string requestContent = await result.RequestMessage.Content.ReadAsStringAsync();
            HttpRequestMessage request = result.RequestMessage;
            string content = await result.Content.ReadAsStringAsync();
            if (result.IsSuccessStatusCode)
            {
                //await AzureConfiguration.SetDefaultCDNNameAsync(azureCDNName);
            }
        }

        private static async Task UploadPathToAzureStorage(string pathOfStaticSite, string storageKey, ConfigurationFile config, HttpClient client)
        {
            string fullPath = Path.GetFullPath(pathOfStaticSite);
            string[] filesToUpload = Directory.GetFiles(fullPath, "*", new EnumerationOptions { RecurseSubdirectories = true, ReturnSpecialDirectories = false, IgnoreInaccessible = true });

            CloudStorageAccount storageAccount = new CloudStorageAccount(new StorageCredentials(config.StorageAccount, storageKey), true);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("$web");
            if (!await container.ExistsAsync())
            {
                Console.WriteLine("Could not find the static site `$web` container. Make sure your run `azstatic init` again.");
                return;
            }

            List<Task> uploadTasks = new List<Task>();
            foreach (string fileToUpload in filesToUpload)
            {
                string relativePath = fileToUpload.Replace(fullPath + "\\", string.Empty);
                CloudBlockBlob blob = container.GetBlockBlobReference(relativePath);
                blob.Properties.ContentType = DetectMimeTypeForFileExtension(Path.GetExtension(fileToUpload));
                uploadTasks.Add(blob.UploadFromFileAsync(fileToUpload));
            }

            try
            {
                await Task.WhenAll(uploadTasks);
            }
            catch (Exception ex)
            {
                throw;
            }

        }

        private static string DetectMimeTypeForFileExtension(string fileExtension)
        {
            return MimeTypeMap.List.MimeTypeMap.GetMimeType(fileExtension).FirstOrDefault();
        }

        private static async Task<string> GetAzureStorageUriAsync(ConfigurationFile config, HttpClient client)
        {
            string subscriptionId = config.SubscriptionId;
            string resourceGroupName = config.ResourceGroup;
            string accountName = config.StorageAccount;

            HttpResponseMessage result = await client.GetAsync($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{accountName}?api-version=2018-07-01");

            string content = await result.Content.ReadAsStringAsync();
            GetProperties properties = JsonConvert.DeserializeObject<GetProperties>(content);
            return properties?.properties?.primaryEndpoints?.web;
        }

        private static async Task SetAzureStorageServiceProperties(string storageKey, ConfigurationFile config)
        {
            CloudStorageAccount storageAccount = new CloudStorageAccount(new StorageCredentials(config.StorageAccount, storageKey), true);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            ServiceProperties blobServiceProperties = new ServiceProperties();
            blobServiceProperties.StaticWebsite = new StaticWebsiteProperties
            {
                Enabled = true,
                IndexDocument = "index.html",
                ErrorDocument404Path = "404.html"
            };
            await blobClient.SetServicePropertiesAsync(blobServiceProperties);
        }

        private static async Task<string> GetAzureStorageKey(ConfigurationFile config, HttpClient client)
        {
            string subscriptionId = config.SubscriptionId;
            string resourceGroupName = config.ResourceGroup;
            string accountName = config.StorageAccount;

            HttpResponseMessage result = await client.PostAsync($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{accountName}/listKeys?api-version=2018-07-01", null);

            string content = await result.Content.ReadAsStringAsync();
            StorageKeysCollection keyCollection = JsonConvert.DeserializeObject<StorageKeysCollection>(content);
            return keyCollection.Keys.First().Value;
        }

        private static async Task CreateStorageAccountAsync(string storageAccountName, HttpClient client, ConfigurationFile config)
        {
            string template = $"{{\"sku\": {{ \"name\": \"Standard_LRS\" }}, \"kind\": \"StorageV2\", \"location\": \"eastus\", \"properties\": {{ \"staticWebsite\": {{ \"enabled\": true}} }} }}";

            HttpResponseMessage result = await client.PutAsync($"/subscriptions/{config.SubscriptionId}/resourcegroups/{config.ResourceGroup}/providers/Microsoft.Storage/storageAccounts/{storageAccountName}?api-version=2018-02-01", new StringContent(template, Encoding.UTF8, "application/json"));
            string requestContent = await result.RequestMessage.Content.ReadAsStringAsync();
            HttpRequestMessage request = result.RequestMessage;
            string content = await result.Content.ReadAsStringAsync();
            if (result.IsSuccessStatusCode)
            {
                await AzureConfiguration.SetDefaultStorageAccountAsync(storageAccountName);
            }
        }

        private static async Task CreateResourceGroupAsync(string rgName, HttpClient client, ConfigurationFile config)
        {
            string defaultLocation = "eastus";
            string template = $"{{\"location\": \"{defaultLocation}\"}}";

            HttpResponseMessage result = await client.PutAsync($"/subscriptions/{config.SubscriptionId}/resourcegroups/{rgName}?api-version=2018-05-01", new StringContent(template, Encoding.UTF8, "application/json"));

            string requestContent = await result.RequestMessage.Content.ReadAsStringAsync();
            HttpRequestMessage request = result.RequestMessage;
            string content = await result.Content.ReadAsStringAsync();
            if (result.IsSuccessStatusCode)
            {
                await AzureConfiguration.SetDefaultResourceGroupAsync(rgName);
            }
        }

        public static string RandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        private static async Task<Subscription> SelectSubcriptionAsync(HttpClient client)
        {

            HttpResponseMessage response = await client.GetAsync("https://management.azure.com/subscriptions?api-version=2014-04-01");
            Subscriptions subscriptions = await response.Content.ReadAsAsync<Subscriptions>();

            Console.Clear();
            Console.WriteLine("Please choose the subscription into which the resources will be deployed.");



            List<Subscription> activeSubscription = subscriptions.value.Where(x => x.state == "Enabled").ToList();
            Subscription selectedSubscription = CliPicker.SelectFromList(activeSubscription, x => $"{x.displayName} ({x.subscriptionId})");
            await AzureConfiguration.SetDefaultSubscriptionAsync(selectedSubscription);
            return selectedSubscription;
        }

        public static IWebHostBuilder CreateWebHostBuilder() =>
            WebHost.CreateDefaultBuilder()
                .SuppressStatusMessages(true)
                .ConfigureLogging((context, logging) =>
                {
                    logging.ClearProviders();
                })
                .UseStartup<Startup>()
                .UseKestrel(options =>
                {
                    options.ListenLocalhost(Common.Port);
                });

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Copied from https://stackoverflow.com/a/38604462/24975</remarks>
        /// <param name="url"></param>
        public static void OpenBrowser(string url)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url.Replace("&", "^&")}"));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                // throw?
            }
        }
    }
}
