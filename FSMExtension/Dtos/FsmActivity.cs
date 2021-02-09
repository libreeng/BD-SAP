using Newtonsoft.Json;

namespace FSMExtension.Dtos
{
    /// <summary>
    /// An FSM Activity DTO.
    /// </summary>
    public class FsmActivity
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("equipment")]
        public string EquipmentId { get; set; }

        [JsonProperty("contact")]
        public string Contact { get; set; }

        [JsonProperty("responsibles")]
        public string[] Responsibles { get; set; }

        public override string ToString()
        {
            return $"Activity {Code}";
        }
    }
}
