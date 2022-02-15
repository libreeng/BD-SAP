﻿using FSMExtension.Dtos;
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
        Task<FsmEquipment> GetEquipmentAsync(string cloudHost, CompanyInfo company, DomainMapping domainMapping, string equipmentId);
        Task<FsmAttachment[]> CreateAttachmentAsync(string activityId, List<Document> documents, CompanyInfo company, string cloudHost, string apiKey);
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

        private readonly MemoryCache cache;


        public FsmApiService(HttpClient httpClient, IConfiguration config, ILogger<FsmApiService> logger)
        {
            HttpClient = httpClient;
            Logger = logger;

            var cacheOptions = new MemoryCacheOptions();
            cache = new MemoryCache(cacheOptions);

            // Get DTO versions to use by reading from appsettings.json
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
        public async Task<FsmAttachment[]> CreateAttachmentAsync(
            string activityId, 
            List<Document> documents, 
            CompanyInfo company, 
            string cloudHost, 
            string apiKey)
        {
            // do not return duplicates.
            var attachments = new FsmAttachment[documents.Count];

            for (var i = 0; i < documents.Count; i++)
            {
                var document = documents[i];
                var assetArr = await GetAssetFromWorkspaceAsync(document, apiKey);

                HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, "https://eu.coresuite.com/api/data/v4/Attachment?dtos=Attachment.18");
                var install = company.Account.FindInstall(cloudHost);

                var token = await GenerateTokenAsync(company.Account.Id, company.Id, install);
                message.Headers.Authorization = AuthenticationHeaderValue.Parse($"Bearer {token}");

                message.Headers.Add("X-Client-ID", install.ClientId);
                message.Headers.Add("X-Client-Version", install.ClientVersion);
                message.Headers.Add("X-Account-ID", company.Account.Id);
                message.Headers.Add("X-Company-ID", company.Id);
                message.Headers.Add("X-Requested-With", "XMLHttpRequest");

                var attachment = new FsmAttachment();
                attachment.FileContent = Convert.ToBase64String(assetArr);
                attachment.FileName = document.title;
                attachment.Type = document.pictureinfo?.format;
                attachment.Object = new ActivityObject { objectId = activityId, objectType = "ACTIVITY" };
                attachment.Description = "Workspace Document";
                attachment.CreateDateTime = Convert.ToDateTime(document.captureTime);
                attachment.Title = document.title;

                message.Content = new StringContent(JsonConvert.SerializeObject(attachment), Encoding.UTF8, "application/json");

                var response = await HttpClient.SendAsync(message);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    JObject jObject = JObject.Parse(json);
                    var item = jObject["data"].First()["attachment"].ToString();
                    attachments[i] = JsonConvert.DeserializeObject<FsmAttachment>(item);
                }
            }
            return attachments;
        }

        public async Task<byte[]> GetAssetFromWorkspaceAsync(Document document, string apiKey)
        {
            HttpRequestMessage message = new HttpRequestMessage();
            message.Method = HttpMethod.Get;
            message.Headers.Add("X-Api-Key", apiKey);
            message.RequestUri = new Uri(document.downloadUrl);

            var asset = await HttpClient.SendAsync(message);
            return await asset.Content.ReadAsByteArrayAsync();
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

        public async Task<FsmEquipment> GetEquipmentAsync(string cloudHost, CompanyInfo company, DomainMapping domainMapping, string equipmentId)
        {
            var customRemoteExpertMapping = domainMapping.FsmAccount.Customization?.RemoteExpert;

            var queryApiRequest = new Uri($"https://{cloudHost}/api/query/v1?dtos=Equipment.{EquipmentVersion}");
            var body = JsonContent.Create(new { query = $"SELECT eqp.id, eqp.code, eqp.udf.{customRemoteExpertMapping?.Email ?? "meta"}, eqp.udf.{customRemoteExpertMapping?.Name ?? "meta"} FROM Equipment eqp WHERE eqp.id = '{equipmentId}'" });
            var message = await CreateMessageAsync(HttpMethod.Post, queryApiRequest, cloudHost, company, body);

            var eqpResult = await GetDtoAsync<FsmEquipmentResult>(message, "eqp");
            if (eqpResult == null || eqpResult.UdfValues == null || eqpResult.UdfValues.Count == 0)
                return null;

            // Map each user-defined field by its name
            var udfMap = eqpResult.UdfValues.ToDictionary(udf => udf.Name);

            var expertEmail = string.Empty;
            var expertName = string.Empty;
            if (customRemoteExpertMapping != null)
            {
                Logger.LogInformation($"Found a customRemoteExpertMapping. Name Field={customRemoteExpertMapping.Name}; Email Field={customRemoteExpertMapping.Email}");
                expertName = udfMap.GetValueOrDefault(customRemoteExpertMapping.Name)?.Value ?? string.Empty;
                expertEmail = udfMap.GetValueOrDefault(customRemoteExpertMapping.Email)?.Value ?? string.Empty;
            }

            return new FsmEquipment
            {
                Code = eqpResult.Code,

                // Our FSM custom fields are NOT of type FsmContact (FSM UI isn't user-friendly in listing Contacts for user),
                // so we need to instantiate our own FsmContact from the individual custom fields we do have.
                RemoteExpert = new FsmContact
                {
                    FirstName = expertName,
                    LastName = string.Empty,
                    EmailAddress = expertEmail,
                    Code = string.Empty,
                    PositionName = string.Empty
                }
            };
        }
    }
}
