using Newtonsoft.Json;

namespace FSMExtension.Dtos
{
    /// <summary>
    /// An FSM User DTO.
    /// </summary>
    public class FsmUser
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("firstName")]
        public string FirstName { get; set; }

        [JsonProperty("lastName")]
        public string LastName { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }
    }
}
