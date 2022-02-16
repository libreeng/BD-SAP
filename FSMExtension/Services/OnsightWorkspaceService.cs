using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace FSMExtension.Services
{
    public interface IOnsightWorkspaceService
    {
        /// <summary>
        /// Returns workspace document json based on activity code metadata.
        /// </summary>
        /// <param name="apiKey">Onsight API key of the (from) caller.</param>
        /// <param name="activityCode">Activity Code which the Workspace Document metadata will be retrieved.</param>
        /// <returns></returns>
        Task<byte[]> DownloadWorkspaceDocumentAsync(string downloadUrl, string apiKey);
        Task<string> GetWorkspaceDocumentsAsync(string apiKey, string activityCode);
    }

    /// <summary>
    /// Implementation of the IOnsightWorkspaceService.
    /// </summary>
    public class OnsightWorkspaceService : IOnsightWorkspaceService
    {
        public OnsightWorkspaceService(HttpClient httpClient)
        {
            HttpClient = httpClient;
        }

        private HttpClient HttpClient { get; }

        public async Task<byte[]> DownloadWorkspaceDocumentAsync(string downloadUrl, string apiKey)
        {
            var request = new CreateOnsightDocumentsRequestMessage(apiKey, new Uri(downloadUrl));

            var response = await HttpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsByteArrayAsync();
        }

        public async Task<string> GetWorkspaceDocumentsAsync(string apiKey, string activityCode)
        {
            var request = new CreateOnsightDocumentsRequestMessage(apiKey, activityCode);
            var response = await HttpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return null;

            var jsonString = await response.Content.ReadAsStringAsync();

            return jsonString;
        }

        private class CreateOnsightDocumentsRequestMessage : HttpRequestMessage
        {
            private static readonly Uri OnsightConnectFlowUri = new Uri("https://api.librestream.com/workspace/documents?query=(externalMetadataValue = '");
            public CreateOnsightDocumentsRequestMessage(string apiKey, string activityCode)
            {
                RequestUri = new Uri(OnsightConnectFlowUri + activityCode + @"')");
                Method = HttpMethod.Get;
                Headers.Add("X-Api-Key", apiKey);
            }

            public CreateOnsightDocumentsRequestMessage(string apiKey, Uri downloadUrl)
            {
                RequestUri = downloadUrl;
                Method = HttpMethod.Get;
                Headers.Add("X-Api-Key", apiKey);
            }
        }
    }
}
