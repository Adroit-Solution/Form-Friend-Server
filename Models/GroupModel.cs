using MongoDB.Bson.Serialization.Attributes;

namespace Server.Models
{
    public class GroupModel
    {
        #region Group
        [BsonId]
        [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
        public string Id { get; set; }
        public Guid GroupId { get; set; }

        [BsonRequired]
        public Guid Creator { get; set; }

        [BsonRequired]
        public string GroupName { get; set; } 

        [BsonRequired]
        public string? GroupType { get; set; } = null!;//Specify if Google Group or Our Group

        [BsonRequired]
        public Guid? GroupLink { get; set; } = null!;//Link to the Group

        public string Description { get; set; }

        public List<string>? GroupParticipant { get; set; } = null!;//All Emails of Member of Group
        public DateTime Date { get; set; } = DateTime.Now;//All Emails of Member of Group
        #endregion
    }

    public class Group
    {
        public string GroupName { get; set; }
        public string Description { get; set; }
        public List<string>? GroupParticipant { get; set; } = null!;//All Emails of Member of Group
    }
}
