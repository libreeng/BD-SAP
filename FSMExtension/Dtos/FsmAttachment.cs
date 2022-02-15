using Newtonsoft.Json;
using System;
using System.Collections.Generic;

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
        public string objectId { get; set; }
        public string objectType { get; set; }
    }

    public class Document
    {
        public string id { get; set; }
        public string parentID { get; set; }
        public string title { get; set; }
        public string createdBy { get; set; }
        public string author { get; set; }
        public string captureTime { get; set; }
        public Dictionary<string, string> captureLocation { get; set; }
        public string lastContributor { get; set; }
        public List<string> contributors { get; set; }
        public string type { get; set; }
        public string path { get; set; }
        public string createdOn { get; set; }
        public string lastModifiedTime { get; set; }
        public List<Dictionary<string,string>> tags { get; set; }
        public string description { get; set; }
        public string keywords { get; set; }
        public string deviceName { get; set; }
        public Dictionary<string,string> externalMetadata { get; set; }
        public string downloadUrl { get; set; }
        public FileProperties fileProperties { get; set; }
        public PictureInfo pictureinfo { get; set; }

    }

    public class PictureInfo
    {
        public string colorSpace { get; set; }
        public int depth { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public string format { get; set; }

    }

    public class FileProperties
    { 
        public string name { get; set; }
        public int contentLength { get; set; }
        public string mimeType { get; set; }
        public string digestAlgorithm { get; set; }
        public string digest { get; set; }
    }
}


