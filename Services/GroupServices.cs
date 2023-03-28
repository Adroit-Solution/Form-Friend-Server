using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Server.Models;

namespace Server.Services
{
    public class GroupServices : IGroupServices
    {
        private readonly IMongoCollection<GroupModel> _group;
        private readonly IMongoCollection<Forms> _form;
        private readonly UserManager<User> userManager;

        public GroupServices(IOptions<MongoDBSettings> settings, UserManager<User> userManager)
        {
            var client = new MongoClient(settings.Value.ConnectionURI);
            var database = client.GetDatabase(settings.Value.DatabaseName);
            _group = database.GetCollection<GroupModel>("group");
            _form = database.GetCollection<Forms>("forms");
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

        public async Task<IdentityResult> DeleteGroup(Guid id, string email)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                throw new Exception("User Not Found");

            var group = await _group.Find(a => a.GroupId == id).FirstOrDefaultAsync();
            if (group is null)
                throw new Exception("Group not Found");

            if(group.Creator!=user.Id)
            {
                throw new Exception("Not authorised to delete");
            }

            var forms = await _form.Find(a => a.Group.Any(a => a.GroupId == group.GroupId)).ToListAsync();
            if(forms is not null)
            {
                foreach (var item in forms)
                {
                    TrackingModel trackingModel = new()
                    {
                        GroupId = group.GroupId,
                        GroupName = group.GroupName
                    };
                    var index = forms.IndexOf(item);
                    var groupIndex = forms[index].Group.IndexOf(trackingModel);

                    var update = Builders<Forms>.Update.PullFilter(a => a.Group, a => a.GroupId == group.GroupId);

                    var result = _form.UpdateOne(a => a.Group[groupIndex].GroupId==group.GroupId, update);

                    if (result.ModifiedCount == 0)
                        throw new Exception("Group Not Deleted From Form");
                }
            }

            var deletedGroup = await _group.FindOneAndDeleteAsync(a => a.GroupId == group.GroupId);
            if(deletedGroup is not null)
                return IdentityResult.Success;

            return IdentityResult.Failed();

        }
    }
}
