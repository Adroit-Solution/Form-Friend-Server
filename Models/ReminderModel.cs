using MongoDB.Bson.Serialization.Attributes;

namespace Server.Models
{
    public class ReminderModel
    {
        [BsonId]
        [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
        public string Id { get; set; }
        public Guid Group { get; set; }
        public string GroupName { get; set; }
        public Guid User { get; set; }
        public string AdminName { get; set; }
        public string Message { get; set; } = "New Form Added into The Group";
        public bool IsSeen { get; set; } = false;

    }

    public class RequestReminder
    {
        public Guid GroupId { get; set; }
        public string Message { get; set; }
        public List<string> Participants { get; set; }
    }
}
