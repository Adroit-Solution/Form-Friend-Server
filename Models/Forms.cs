using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;

namespace Server.Models
{
    public class Forms
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public MetaDataModel Form { get; set; }
        public SettingsModel Settings { get; set; }
        public List<TrackingModel> Group { get; set; }
        [BsonElement("Responses")]
        [JsonPropertyName("Responses")]
        public List<ResponseModel>? Responses { get; set; } = null!;
        public bool AcceptResponse { get; set; }
        public string Message { get; set; } = "Form is Closed";//Message when Form is Closed
    }
}
