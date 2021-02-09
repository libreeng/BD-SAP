using Newtonsoft.Json.Converters;
using System.Text.Json.Serialization;

namespace FSMExtension.Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum OnsightConnectPlatform
    {
        PC,
        Android,
        iOS
    }
}
