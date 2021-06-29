using Newtonsoft.Json;

namespace FSMExtension.Models
{
    public class Customization
    {
        [JsonProperty("remoteExpert")]
        public UdfNames RemoteExpert { get; set; }

        public class UdfNames
        {
            [JsonProperty("email")]
            public string Email { get; set; }
            
            [JsonProperty("name")]
            public string Name { get; set; }
        }
    }
}