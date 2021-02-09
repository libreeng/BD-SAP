using Newtonsoft.Json;

namespace FSMExtension.Models
{
    /// <summary>
    /// Configuration details for an external OpenID Connect provider.
    /// </summary>
    public class IdentityProvider
    {
        [JsonProperty("authorizeUrl")]
        public string AuthorizeUrl { get; set; }

        [JsonProperty("tokenUrl")]
        public string TokenUrl { get; set; }

        [JsonProperty("userInfoUrl")]
        public string UserInfoUrl { get; set; }

        [JsonProperty("clientId")]
        public string ClientId { get; set; }

        [JsonProperty("clientSecret")]
        public string ClientSecret { get; set; }
    }
}
