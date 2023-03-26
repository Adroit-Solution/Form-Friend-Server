using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Server.Models
{
    public class QuestionModel
    {
        [BsonId]
        public Guid Id { get; set; }
        public string Question { get; set; }
        public string? PhotoPath { get; set; } = null!;
        public List<string>? Options { get; set; } = null!;
        public string Type { get; set; }
    }
}
