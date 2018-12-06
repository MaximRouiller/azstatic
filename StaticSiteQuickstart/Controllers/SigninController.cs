using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace StaticSiteQuickstart.Controllers
{
    [AllowAnonymous]
    public class SigninController : Controller
    {
        private readonly IApplicationLifetime lifetime;

        public SigninController(IApplicationLifetime lifetime)
        {
            this.lifetime = lifetime;
        }
        public async Task<string> Index(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) { return "failure"; }
            HttpClient client = new HttpClient();

            HttpResponseMessage result = await client.PostAsync("https://login.microsoftonline.com/common/oauth2/token", new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>( "client_id", Common.ClientId),
                new KeyValuePair<string, string>( "resource", "https://management.azure.com/"),
                new KeyValuePair<string, string>( "code", code),
                new KeyValuePair<string, string>( "redirect_uri", Common.ReplyUrl),
                new KeyValuePair<string, string>( "grant_type", "authorization_code"),
                new KeyValuePair<string, string>( "client_secret", "")
            }));

            if (!result.IsSuccessStatusCode) { return "failure"; }

            try
            {


                string authResult = await result.Content.ReadAsStringAsync();

                using (FileStream fs = new FileStream(".token", FileMode.Create))
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    await sw.WriteAsync(authResult);
                }
                lifetime.StopApplication();
                return "success!";
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}
