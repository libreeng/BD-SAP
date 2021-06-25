using Newtonsoft.Json;

namespace FSMExtension.Dtos
{
    public class FsmEquipment
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonIgnore]
        public FsmContact RemoteExpert { get; set; }
    }
}
