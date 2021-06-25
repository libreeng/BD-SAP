using FSMExtension.Dtos;
using FSMExtension.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace FSMExtension.Services
{
    /// <summary>
    /// Provides access to the FSM API's data model objects.
    /// </summary>
    public interface IFsmApiService
    {
        Task<FsmActivity> GetActivityAsync(string cloudHost, CompanyInfo company, string activityId);
        Task<FsmContact> GetContactAsync(string cloudHost, CompanyInfo company, string contactId);
        Task<FsmPerson[]> GetPersonsAsync(string cloudHost, CompanyInfo company, params string[] personIds);
        Task<FsmUser> GetUserAsync(string cloudHost, CompanyInfo company, string userId);
        Task<FsmContact> GetEquipmentContactAsync(string cloudHost, CompanyInfo company, string equipmentId);
    }

    /// <summary>
    /// Implementation of the IFsmDataService.
    /// </summary>
    public class FsmApiService : IFsmApiService
    {
        /// <summary>
        /// URL for getting tokens to be used with FSM APIs.
        /// </summary>
        private static readonly string TokenUrl = "https://auth.coresuite.com/api/oauth2/v1/token";
        private static readonly FormUrlEncodedContent TokenRequestBody = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        });

        // These were the latest versions of each data model at the time of writing.
        // Older versions could likely be used as well, since only a small number of
        // fields from each are actually used by this extension.
        private readonly int ActivityVersion = 37;
        private readonly int ContactVersion = 17;
        private readonly int PersonVersion = 24;
        private readonly int EquipmentVersion = 23;
        private const string OnsightRemoteExpertName = "OnsightRemoteExpertName";
        private const string OnsightRemoteExpertEmail = "OnsightRemoteExpertEmail";

        private readonly MemoryCache cache;


        public FsmApiService(HttpClient httpClient, IConfiguration config, ILogger<FsmApiService> logger)
        {
            HttpClient = httpClient;
            Logger = logger;

            var cacheOptions = new MemoryCacheOptions();
            cache = new MemoryCache(cacheOptions);

            if (int.TryParse(config["FSM:DTOs:Activity"], out var intValue))
                ActivityVersion = intValue;
            if (int.TryParse(config["FSM:DTOs:Contact"], out intValue))
                ContactVersion = intValue;
            if (int.TryParse(config["FSM:DTOs:Person"], out intValue))
                PersonVersion = intValue;
        }

        private HttpClient HttpClient { get; }

        private ILogger<FsmApiService> Logger { get; }

        public async Task<FsmActivity> GetActivityAsync(string cloudHost, CompanyInfo company, string activityId)
        {
            var request = await CreateMessageAsync(cloudHost, company, "Activity", activityId, ActivityVersion);
            return await GetDtoAsync<FsmActivity>(request, "Activity");
        }

        public async Task<FsmContact> GetContactAsync(string cloudHost, CompanyInfo company, string contactId)
        {
            var request = await CreateMessageAsync(cloudHost, company, "Contact", contactId, ContactVersion);
            return await GetDtoAsync<FsmContact>(request, "Contact");
        }

        public async Task<FsmPerson[]> GetPersonsAsync(string cloudHost, CompanyInfo company, params string[] personIds)
        {
            var tasks = new Task<FsmPerson>[personIds.Length];

            for (var i = 0; i < personIds.Length; i++)
            {
                var personId = personIds[i];
                var request = await CreateMessageAsync(cloudHost, company, "Person", personId, PersonVersion);
                tasks[i] = GetDtoAsync<FsmPerson>(request, "Person");
            }

            return await Task.WhenAll(tasks);
        }

        public async Task<FsmUser> GetUserAsync(string cloudHost, CompanyInfo company, string userId)
        {
            var account = company.Account;
            var install = account.FindInstall(cloudHost);
            var request = await CreateMessageAsync(
                HttpMethod.Get,
                new Uri($"https://{install.CloudHost}/api/user/v1/users/{userId}?account={account.Name}"),
                cloudHost,
                company);

            var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<FsmUser>(json);
        }

        private async Task<T> GetDtoAsync<T>(HttpRequestMessage request, string dtoName)
        {
            var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return default;

            var content = await response.Content.ReadAsStringAsync();
            var jobject = JObject.Parse(content);
            var itemArray = jobject["data"] as JArray;
            if (itemArray.Count == 0)
                return default;

            var firstItem = itemArray.First();
            var itemObj = firstItem[dtoName.ToLower()];

            if (itemObj == null)
                return default;

            return itemObj.ToObject<T>();
        }

        private Task<HttpRequestMessage> CreateMessageAsync(string cloudHost, CompanyInfo company, string dtoName, string dtoId, int dtoVersion)
        {
            var requestUri = new Uri($"https://{cloudHost}/api/data/v4/{dtoName}/{dtoId}?dtos={dtoName}.{dtoVersion}");
            return CreateMessageAsync(HttpMethod.Get, requestUri, cloudHost, company);
        }

        private async Task<HttpRequestMessage> CreateMessageAsync(
            HttpMethod method,
            Uri requestUri,
            string cloudHost,
            CompanyInfo company,
            HttpContent body = null)
        {
            var install = company.Account.FindInstall(cloudHost);
            var message = new HttpRequestMessage(method, requestUri);

            var token = await GenerateTokenAsync(company.Account.Id, company.Id, install);
            message.Headers.Authorization = AuthenticationHeaderValue.Parse($"Bearer {token}");
            message.Headers.Add("X-Client-ID", install.ClientId);
            message.Headers.Add("X-Client-Version", install.ClientVersion);
            message.Headers.Add("X-Account-ID", company.Account.Id);
            message.Headers.Add("X-Company-ID", company.Id);
            message.Content = body;

            return message;
        }

        private async Task<string> GenerateTokenAsync(string fsmAccountId, string fsmCompanyId, InstallInfo install)
        {
            // If we already have an in-memory cached token, return it
            var cacheKey = $"{fsmAccountId}:{fsmCompanyId}:{install.CloudHost}";
            if (cache.TryGetValue(cacheKey, out string token))
                return token;

            // Otherwise, we need to request a new token from FSM's OAuth2 provider
            var message = new HttpRequestMessage(HttpMethod.Post, TokenUrl);
            var authHeaderValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{install.ClientId}:{install.ClientSecret}"));

            message.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);
            message.Content = TokenRequestBody;
            var startTime = DateTime.UtcNow;
            var response = await HttpClient.SendAsync(message);

            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();

            var jobject = JObject.Parse(responseString);
            token = jobject["access_token"].Value<string>();
            var expiration = startTime.AddSeconds(jobject["expires_in"].Value<long>());

            // Before returning, cache this token in memory for next time
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSize(1)
                .SetAbsoluteExpiration(expiration);

            cache.Set(cacheKey, token, cacheEntryOptions);
            return token;
        }

        public async Task<FsmContact> GetEquipmentContactAsync(string cloudHost, CompanyInfo company, string equipmentId)
        {
            var queryApiRequest = new Uri($"https://{cloudHost}/api/query/v1?dtos=Equipment.{EquipmentVersion}");
            var body = JsonContent.Create(new { query = $"SELECT eqp.id, eqp.code, eqp.udf.OnsightRemoteExpertEmail, eqp.udf.OnsightRemoteExpertName FROM Equipment eqp WHERE eqp.id = '{equipmentId}'" });
            var message = await CreateMessageAsync(HttpMethod.Post, queryApiRequest, cloudHost, company, body);

            var eqpResult = await GetDtoAsync<FsmEquipmentResult>(message, "eqp");
            if (eqpResult == null || eqpResult.UdfValues == null || eqpResult.UdfValues.Count == 0)
                return null;

            // Map each user-defined field by its name
            var udfMap = eqpResult.UdfValues.ToDictionary(udf => udf.Name);

            // Our FSM custom fields are NOT of type FsmContact (FSM UI isn't user-friendly in listing Contacts for user),
            // so we need to instantiate our own FsmContact from the individual custom fields we do have.
            return new FsmContact
            {
                FirstName = udfMap.GetValueOrDefault(OnsightRemoteExpertName)?.Value ?? string.Empty,
                LastName = string.Empty,
                EmailAddress = udfMap.GetValueOrDefault(OnsightRemoteExpertEmail)?.Value ?? string.Empty,
                Code = string.Empty,
                PositionName = string.Empty
            };
        }
    }
}
