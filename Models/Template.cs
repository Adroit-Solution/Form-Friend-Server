using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace Server.Models
{
    public class Template
    {
        [BsonId]
        public Guid Id { get; set; }

        [BsonRequired]
        public Guid TemplateId { get; set; }

        [BsonRequired]
        public string FormName { get; set; } = "Untitled Form";

        [BsonRequired]
        public string Title { get; set; } = "Untitled Form";

        [BsonRequired]
        public string Description { get; set; } = "";//Store with Every Styling given by Creator

        [BsonRequired]
        [BsonElement("Questions")]
        [JsonPropertyName("Questions")]
        //It will have Question and Its Type
        public List<QuestionModel> Questions { get; set; } =
            new List<QuestionModel>
            { new QuestionModel
                {
                    Id = Guid.NewGuid(),
                    Options = new List<string>{"Option1"},
                    PhotoPath="",
                    Question="Untitled Question",
                    Type="Radio"
                }
            };

        public CSSModel CSS { get; set; }
    }
}
