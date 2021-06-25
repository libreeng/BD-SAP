using Newtonsoft.Json;
using System.Collections.Generic;

namespace FSMExtension.Dtos
{
    /// <summary>
    /// Result of an FSM query API call to fetch custom fields on a piece of Equipment.
    /// </summary>
    public class FsmEquipmentResult
    {
        /// <summary>
        /// The Equipment ID.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>
        /// The Equipment code.
        /// </summary>
        [JsonProperty("code")]
        public string Code { get; set; }

        /// <summary>
        /// All non-null user-defined fields associated with this piece of Equipment.
        /// </summary>
        [JsonProperty("udfValues")]
        public List<FsmUdf> UdfValues { get; set; }
    }
}
