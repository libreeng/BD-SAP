using FSMExtension.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace FSMExtension.Services
{
    public interface IOnsightConnectService
    {
        /// <summary>
        /// Generates Onsight Connect URLs based on the given contact information.
        /// </summary>
        /// <param name="platform">The platform from which this call will be initiated.</param>
        /// <param name="apiKey">Onsight API key of the (from) caller.</param>
        /// <param name="from">Email address of the person wishing to initiate the Onsight call.</param>
        /// <param name="to">Email address of the person to receive the Onsight call.</param>
        /// <param name="metadata">Any call-specific metadata to be attached to the call. Can be null.</param>
        /// <returns></returns>
        Task<string> GetUriAsync(OnsightConnectPlatform platform, string apiKey, string from, string to, object metadata);
    }

    /// <summary>
    /// Implementation of the IOnsightConnectService.
    /// </summary>
    public class OnsightConnectService : IOnsightConnectService
    {
        public OnsightConnectService(HttpClient httpClient, ILogger<OnsightConnectService> logger)
        {
            HttpClient = httpClient;
            Logger = logger;
        }

        private HttpClient HttpClient { get; }

        private ILogger<OnsightConnectService> Logger { get; }


        public async Task<string> GetUriAsync(OnsightConnectPlatform platform, string apiKey, string from, string to, object metadata)
        {
            var request = new CreateUriRequestMessage(platform, apiKey, from, to, metadata);
            var response = await HttpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var ocErrorBody = await response.Content.ReadAsStringAsync();
                Logger.LogError("Failed to get Onsight Connect call URI: ({StatusCode}) - {Message}", response.StatusCode, ocErrorBody);
                return null;
            }

            var uriString = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrEmpty(uriString))
                uriString = uriString.Replace("\"", string.Empty);

            return uriString;
        }

        private class CreateUriRequestMessage : HttpRequestMessage
        {
            private static readonly Uri OnsightConnectUri = new Uri("https://onsight.librestream.com/oamrestapi/api/launchrequest");


            public CreateUriRequestMessage(OnsightConnectPlatform platform, string apiKey, string from, string to, object metadata)
            {
                RequestUri = OnsightConnectUri;
                Method = HttpMethod.Post;
                Content = CreateContent(platform, from, to, metadata);
                Headers.Authorization = AuthenticationHeaderValue.Parse($"ls Bearer {apiKey}");
            }

            private static HttpContent CreateContent(OnsightConnectPlatform platform, string fromEmail, string toEmail, object metadata)
            {
                var body = new Body
                {
                    Platform = platform.ToString(),
                    Email = fromEmail,
                    CalleeEmail = toEmail,
                    MetadataItems = metadata
                };

                var json = JsonConvert.SerializeObject(body);
                return new StringContent(json, Encoding.UTF8, "application/json");
            }

            private class Body
            {
                [JsonProperty("Platform")]
                public string Platform { get; set; }
                
                [JsonProperty("email")]
                public string Email { get; set; }

                [JsonProperty("calleeEmail")]
                public string CalleeEmail { get; set; }

                [JsonProperty("metadataItems")]
                public object MetadataItems { get; set; }
            }
        }
    }
}
