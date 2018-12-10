using azstatic.ConsoleComponents;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using azstatic.Models;
using azstatic.Models.subscriptions;
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
using static azstatic.Models.AzureConfiguration;

namespace azstatic
{
    class Program
    {
        private static Random random = new Random();
        static async Task Main(string[] args)
        {
            if (args.Length == 0) { Console.WriteLine("Missing parameters"); return; }

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
                string rawToken = File.ReadAllText(".token");
                Token token = JsonConvert.DeserializeObject<Token>(rawToken);

                HttpClient client = new HttpClient();
                client.BaseAddress = new Uri("https://management.azure.com");
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.access_token);
                client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("azstatic", "0.0.1"));


                // select subscription
                Configuration config = AzureConfiguration.GetFromFile();
                if (string.IsNullOrWhiteSpace(config.SubscriptionId))
                {
                    Subscription subscription = await SelectSubcriptionAsync(client);

                    Console.Clear();
                    Console.WriteLine($"The selected subscription is: {subscription.displayName}");
                }

                // select resource group
                config = AzureConfiguration.GetFromFile();
                if (string.IsNullOrWhiteSpace(config.ResourceGroup))
                {
                    //todo: allow to specify RG name
                    string rgName = $"azstatic-{RandomString(10)}-prod-rg";

                    await CreateResourceGroupAsync(rgName, client, config);
                }

                // select storage account name
                config = AzureConfiguration.GetFromFile();
                if (string.IsNullOrWhiteSpace(config.StorageAccount))
                {

                }

                // todo: write code that will be very nice and will actually do magnificient work.
                Console.WriteLine("Static site deployed.");
            }
        }

        private static async Task CreateResourceGroupAsync(string rgName, HttpClient client, Configuration config)
        {
            //var rgDefinition = new { location = "eastus" };
            string rawJson = "{\"location\": \"eastus\"}";

            //HttpResponseMessage result = await client.PutAsJsonAsync($"/subscriptions/{config.SubscriptionId}/resourcegroups/{rgName}?api-version=2018-05-01", rgDefinition);
            //var formatter = new JsonMediaTypeFormatter();

            //formatter.SerializerSettings.Formatting = Formatting.Indented;

            //HttpResponseMessage result = await client.PutAsync($"/subscriptions/{config.SubscriptionId}/resourcegroups/{rgName}?api-version=2018-05-01", 
            //    new ObjectContent(rgDefinition.GetType(), rgDefinition, formatter));

            HttpResponseMessage result = await client.PutAsync($"/subscriptions/{config.SubscriptionId}/resourcegroups/{rgName}?api-version=2018-05-01", new StringContent(rawJson, Encoding.UTF8, "application/json"));

            string requestContent = await result.RequestMessage.Content.ReadAsStringAsync();
            HttpRequestMessage request = result.RequestMessage;
            string content = await result.Content.ReadAsStringAsync();
            if (result.IsSuccessStatusCode)
            {
                //SiteConfiguration.SetDefaultResourceGroup(rgName);
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
            AzureConfiguration.SetDefaultSubscription(selectedSubscription);
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
