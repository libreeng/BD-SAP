using System.Collections.Generic;
using Newtonsoft.Json;

namespace FSMExtension.Dtos
{
    /// <summary>
    /// A custom user-defined field attached to an Activity FSM DTO.
    /// </summary>
    public class Meta
    {
        [JsonProperty("externalId")]
        public string ExternalId { get; set; }
    }

    public class UdfValue
    {
        [JsonProperty("meta")]
        public Meta Meta { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }
    }

    public class fsmUdf
    {
        [JsonProperty("udfValues")]
        public List<UdfValue> UdfValues { get; set; }
    }
}
