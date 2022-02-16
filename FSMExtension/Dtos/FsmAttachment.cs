using Newtonsoft.Json;
using System;

namespace FSMExtension.Dtos
{
    /// <summary>
    /// An FSM Atachment DTO.
    /// </summary>

    public class FsmAttachment
    {
        [JsonProperty("fileName")]
        public string FileName { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("fileContent")]
        public string FileContent { get; set; }

        [JsonProperty("object")]
        public ActivityObject Object { get; set; }

        [JsonProperty("createDateTime")]
        public DateTime CreateDateTime { get; set; }
    }

    public class ActivityObject
    {
        [JsonProperty("objectId")]
        public string ObjectId { get; set; }

        [JsonProperty("objectType")]
        public string ObjectType { get; set; }
    }
}


