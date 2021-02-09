using Newtonsoft.Json;

namespace FSMExtension.Dtos
{
    /// <summary>
    /// An FSM Person DTO.
    /// </summary>
    public class FsmPerson
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("firstName")]
        public string FirstName { get; set; }

        [JsonProperty("lastName")]
        public string LastName { get; set; }

        [JsonProperty("jobTitle")]
        public string JobTitle { get; set; }

        [JsonProperty("positionName")]
        public string PositionName { get; set; }

        [JsonProperty("emailAddress")]
        public string EmailAddress { get; set; }

        public override string ToString()
        {
            return $"{FirstName} {LastName} ({PositionName ?? JobTitle})";
        }
    }
}
