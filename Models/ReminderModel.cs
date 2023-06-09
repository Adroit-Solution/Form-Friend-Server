﻿using MongoDB.Bson.Serialization.Attributes;
using System.Security.Principal;

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
        public Guid FromId { get; set; }

    }

    public class RequestReminder
    {
        public Guid GroupId { get; set; }
        public string Message { get; set; }
        public Guid FormId { get; set; }
        public List<string> Participants { get; set; }
    }

    public class ResponseReminder
    {
        public string Id { get; set; }
        public Guid GroupId { get; set; }
        public string Message { get; set; }
        public string GroupName { get; set; }
        public string AdminName { get; set; }
        public Guid FormId { get; set; }
        public bool IsSeen { get; set; }
    }
}
