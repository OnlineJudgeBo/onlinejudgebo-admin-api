using System.Text.Json.Serialization;

namespace OnlineJudgeAdminApi.DataTransferObjects
{
    public partial class RemoteExecutionResult
    {
        public int Memory { get; set; }

        [JsonPropertyName("in_date")]
        public string InDate { get; set; }

        public int Result { get; set; }

        [JsonPropertyName("judgetime")]
        public string JudgeTime { get; set; }

        [JsonPropertyName("remote_id")]
        public int RemoteId { get; set; }

        [JsonPropertyName("time")]
        public int Time { get; set; }
    }
}
