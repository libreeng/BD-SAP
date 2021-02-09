using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FSMExtension.Models;
using Newtonsoft.Json.Linq;

namespace FSMExtension.Services
{
    /// <summary>
    /// Helper interface for retrieving userinfo from a 3rd-party OpenID Connect provider.
    /// </summary>
    public interface IOpenIdService
    {
        /// <summary>
        /// Retrieves the email address of the user corresponding to the given identity provider authCode.
        /// </summary>
        /// <param name="idp"></param>
        /// <param name="authCode"></param>
        /// <param name="redirectUri"></param>
        /// <returns></returns>
        Task<string> GetEmailAsync(IdentityProvider idp, string authCode, string redirectUri);
    }

    public class OpenIdService : IOpenIdService
    {
        public OpenIdService(HttpClient httpClient)
        {
            HttpClient = httpClient;
        }

        private HttpClient HttpClient { get; }

        public async Task<string> GetEmailAsync(IdentityProvider idp, string authCode, string redirectUri)
        {
            var token = await GetTokenAsync(idp, authCode, redirectUri);
            var userInfo = await GetUserInfoAsync(idp, token);

            var jtoken = userInfo["email"] ?? userInfo["preferred_username"];
            return jtoken?.Value<string>();
        }

        private async Task<string> GetTokenAsync(IdentityProvider idp, string authCode, string redirectUri)
        {
            var tokenRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(idp.TokenUrl),
                Method = HttpMethod.Post,
                Content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("client_id", idp.ClientId),
                    new KeyValuePair<string, string>("client_secret", idp.ClientSecret),
                    new KeyValuePair<string, string>("code", authCode),
                    new KeyValuePair<string, string>("redirect_uri", redirectUri)
                })
            };
            var tokenResponse = await HttpClient.SendAsync(tokenRequest);
            tokenResponse.EnsureSuccessStatusCode();
            var tokenResponseString = await tokenResponse.Content.ReadAsStringAsync();
            var jobject = JObject.Parse(tokenResponseString);

            return jobject["access_token"].Value<string>();
        }

        private async Task<JObject> GetUserInfoAsync(IdentityProvider idp, string token)
        {
            var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, idp.UserInfoUrl);
            userInfoRequest.Headers.Authorization = AuthenticationHeaderValue.Parse($"Bearer {token}");
            var userInfoResponse = await HttpClient.SendAsync(userInfoRequest);
            userInfoResponse.EnsureSuccessStatusCode();
            var userInfoResponseString = await userInfoResponse.Content.ReadAsStringAsync();

            return JObject.Parse(userInfoResponseString);
        }
    }
}
