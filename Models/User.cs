using AspNetCore.Identity.MongoDbCore.Models;
using MongoDbGenericRepository.Attributes;

namespace Server.Models
{
    [CollectionName("User")]
    public class User:MongoIdentityUser<Guid>
    {
        public string Name { get; set; }
        public string Profile { get; set; }
    }
}
