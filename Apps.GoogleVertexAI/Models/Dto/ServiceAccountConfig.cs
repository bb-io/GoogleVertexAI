using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Apps.GoogleVertexAI.Models.Dto
{
    public class ServiceAccountConfig
    {
        [JsonProperty("project_id")]
        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; }
    }
}
