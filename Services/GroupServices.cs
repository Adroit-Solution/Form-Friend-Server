using Amazon.SecurityToken.Model.Internal.MarshallTransformations;
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
                groupModel.GroupParticipant = group.GroupParticipant.Distinct().ToList();
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

        public async Task<IdentityResult> AddMemberToGroup(List<string> participants,Guid groupId,string email)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                throw new Exception("User Not Found");

            var group = await _group.Find(a => a.GroupId == groupId).FirstOrDefaultAsync();
            if (group is null)
                throw new Exception("Group not Found");

            if (group.Creator != user.Id)
                throw new Exception("Not authorised to Add participant to Group");

            if (group.GroupParticipant is not null)
            {
                List<string> toRemove = new();
                foreach (var participant in participants)
                {
                    if (group.GroupParticipant.Contains(participant))
                    {
                        toRemove.Add(participant);
                    }
                }
                toRemove.ForEach(a => participants.Remove(a));
            }

            if (participants.Count == 0)
                return IdentityResult.Success;
            var filter = Builders<GroupModel>.Filter.Eq(x => x.GroupId, groupId);

            var update = Builders<GroupModel>.Update
             .AddToSetEach(x => x.GroupParticipant, participants);

            var result = await _group.UpdateOneAsync(filter: filter, update: update);

            var forms = await _form.Find(a => a.Group.Any(a => a.GroupId == group.GroupId)).ToListAsync();

            List<Tracker> trackers = new List<Tracker>();
            if (participants is not null)
            {
                foreach (var item in participants)
                {
                    Tracker participant = new Tracker()
                    {
                        Email = item,
                        Filled = false,
                        Seen = false
                    };
                    trackers.Add(participant);
                }
            }

            if (forms is not null)
            {
                foreach (var item in forms)
                {
                    TrackingModel trackingModel = new()
                    {
                        GroupId = group.GroupId,
                        GroupName = group.GroupName
                    };
                    var index = forms.IndexOf(item);
                    var tracking = forms[index].Group.Find(a=>a.GroupId==trackingModel.GroupId);
                    if (tracking is null)
                        throw new Exception("Some Error Occured");
                    var groupIndex = forms[index].Group.IndexOf(tracking);

                    var formFilter = Builders<Forms>.Filter.Eq(a => a.Group[groupIndex].GroupId , group.GroupId);
                    var formUpdate = Builders<Forms>.Update.PushEach($"Group.{groupIndex}.Participants", trackers);
                    var formResult = await _form.UpdateOneAsync(formFilter, formUpdate);

                    if (formResult.ModifiedCount == 0)
                        throw new Exception("Members not added into the forms");
                }
            }
                return IdentityResult.Success;
        }

        public async Task<IdentityResult> FetchMembers(Guid formId, string email)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                throw new Exception("User Not Found");

            var form = await _form.Find(a => a.Form.Id == formId).FirstOrDefaultAsync();
            if (form is null)
                throw new Exception("Group not Found");

            if (form.Form.CreatorId != user.Id)
                throw new Exception("Not authorised to Add participant to Form");

            var groups = form.Group.ToList();
            foreach (var group in groups)
            {
                var isGroup = await _group.Find(a=>a.GroupId==group.GroupId).FirstOrDefaultAsync();
                if (isGroup is null)
                    throw new Exception("Group Not Present");

                var formGroupParticipants = group.Participants.Select(a=>a.Email).ToList();

                var groupParticipants = isGroup.GroupParticipant;
                if (groupParticipants is null)
                    continue;
                formGroupParticipants.ForEach(a => groupParticipants.Remove(a));
                if (groupParticipants.Count == 0)
                    continue;
                List<Tracker> trackers = new List<Tracker>();
                if (groupParticipants is not null)
                {
                    foreach (var item in groupParticipants)
                    {
                        Tracker participant = new Tracker()
                        {
                            Email = item,
                            Filled = false,
                            Seen = false
                        };
                        trackers.Add(participant);
                    }
                }

                TrackingModel trackingModel = new()
                {
                    GroupId = group.GroupId,
                    GroupName = group.GroupName
                };
                
                var tracking = form.Group.Find(a => a.GroupId == trackingModel.GroupId);
                if (tracking is null)
                    throw new Exception("Some Error Occured");
                var groupIndex = form.Group.IndexOf(tracking);

                var formFilter = Builders<Forms>.Filter.Eq(a => a.Group[groupIndex].GroupId, group.GroupId);
                var formUpdate = Builders<Forms>.Update.PushEach($"Group.{groupIndex}.Participants", trackers);
                var formResult = await _form.UpdateOneAsync(formFilter, formUpdate);

                if (formResult.ModifiedCount == 0)
                    throw new Exception("Members not added into the forms");
            }

            return IdentityResult.Success;
        }

        public async Task<IdentityResult> DeleteMemberFromGroup(List<string> participants, Guid groupId, string email)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                throw new Exception("User Not Found");

            var group = await _group.Find(a => a.GroupId == groupId).FirstOrDefaultAsync();
            if (group is null)
                throw new Exception("Group not Found");

            if (group.Creator != user.Id)
                throw new Exception("Not authorised to Add participant to Group");

            if (group.GroupParticipant is not null)
            {
                List<string> toRemove = new();
                foreach (var participant in participants)
                {
                    if (!group.GroupParticipant.Contains(participant))
                    {
                        toRemove.Add(participant);
                    }
                }
                toRemove.ForEach(a => participants.Remove(a));
            }

            if (participants.Count == 0)
                return IdentityResult.Success;
            var filter = Builders<GroupModel>.Filter.Eq(x => x.GroupId, groupId);

            var update = Builders<GroupModel>.Update
             .PullAll(x => x.GroupParticipant, participants);

            var result = await _group.UpdateOneAsync(filter: filter, update: update);

            var forms = await _form.Find(a => a.Group.Any(a => a.GroupId == group.GroupId)).ToListAsync();

            List<Tracker> trackers = new List<Tracker>();
            if (participants is not null)
            {
                foreach (var item in participants)
                {
                    Tracker participant = new Tracker()
                    {
                        Email = item
                    };
                    trackers.Add(participant);
                }
            }

            if (forms is not null)
            {
                foreach (var item in forms)
                {
                    TrackingModel trackingModel = new()
                    {
                        GroupId = group.GroupId,
                        GroupName = group.GroupName
                    };
                    var index = forms.IndexOf(item);
                    var tracking = forms[index].Group.Find(a => a.GroupId == trackingModel.GroupId);
                    if (tracking is null)
                        throw new Exception("Some Error Occured");
                    var groupIndex = forms[index].Group.IndexOf(tracking);

                    var formFilter = Builders<Forms>.Filter.Eq(a => a.Group[groupIndex].GroupId, group.GroupId);
                    var formUpdate = Builders<Forms>.Update.PullAll($"Group.{groupIndex}.Participants", trackers);
                    var formResult = await _form.UpdateOneAsync(formFilter, formUpdate);

                    if (formResult.ModifiedCount == 0)
                        throw new Exception("Members not added into the forms");
                }
            }
            return IdentityResult.Success;
        }
    }
}
