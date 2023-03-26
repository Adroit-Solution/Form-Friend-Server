using MongoDB.Bson.Serialization.Attributes;

namespace Server.Models
{
    public class ResponseModel
    {
        [BsonId]
        public Guid Id { get; set; }
        public Guid? FormId { get; set; }
        public Guid? UserId { get; set; }
        public List<Answer> Response { get; set; }

        
    }
    public class Answer
    {
        public Guid Id { get; set; }
        public Guid? QuestionId { get; set; }

    }

}
