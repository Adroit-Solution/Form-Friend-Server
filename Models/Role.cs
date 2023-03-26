using AspNetCore.Identity.MongoDbCore.Models;
using MongoDbGenericRepository.Attributes;

namespace Server.Models
{
    [CollectionName("Role")]
    public class Role:MongoIdentityRole<Guid>
    {
    }
}
