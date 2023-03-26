using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Server.Models;

namespace Server.Services
{
    public class GroupServices : IGroupServices
    {
        private readonly IMongoCollection<GroupModel> _group;
        private readonly UserManager<User> userManager;

        public GroupServices(IOptions<MongoDBSettings> settings, UserManager<User> userManager)
        {
            var client = new MongoClient(settings.Value.ConnectionURI);
            var database = client.GetDatabase(settings.Value.DatabaseName);
            _group = database.GetCollection<GroupModel>("group");
            this.userManager = userManager;
        }

        public async Task<IdentityResult> AddGroup(Group group, string email)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                throw new Exception("User not Found");

            GroupModel groupModel = new GroupModel()
            {
                Creator = user.Id,
                GroupId = Guid.NewGuid(),
                GroupName = group.GroupName,
                GroupLink = Guid.NewGuid(),
                GroupType = "Personal",
                Description = group.Description
            };
            
            if(group.GroupParticipant is not null)
            {
                groupModel.GroupParticipant = group.GroupParticipant;
            }
            else
            {
                groupModel.GroupParticipant = new List<string>();
            }

            await _group.InsertOneAsync(groupModel);
            return IdentityResult.Success;

        }

        public async Task<List<GroupModel>> GetGroups(string email)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                throw new Exception("User Not Found");

            var result = await _group.Find(a=>a.Creator==user.Id).ToListAsync();
            return result;
        }
    }
}
