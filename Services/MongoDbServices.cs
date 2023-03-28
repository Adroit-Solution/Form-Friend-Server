using DnsClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Server.Models;
using ZstdSharp.Unsafe;

namespace Server.Services
{
    public class MongoDbServices : IMongoDbServices
    {
        private readonly IMongoCollection<Forms> _collection;
        private readonly IMongoCollection<GroupModel> _group;
        private readonly UserManager<User> userManager;

        public MongoDbServices(IOptions<MongoDBSettings> settings, UserManager<User> userManager)
        {
            var client = new MongoClient(settings.Value.ConnectionURI);
            var database = client.GetDatabase(settings.Value.DatabaseName);
            _collection = database.GetCollection<Forms>("forms");
            _group = database.GetCollection<GroupModel>("group");
            this.userManager = userManager;
        }

        public async Task<IEnumerable<dynamic>> GetAllFormsAsync(string email)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                throw new Exception("User Not Found");


            var result =  _collection.Find(a => a.Form.CreatorId == user.Id).ToList().Select(a=>new { a.Form.Id,a.Form.FormName });
            return result;

        }

        public async Task<Forms> AddForm(string email)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                throw new Exception("User Not Found");
            Forms forms = new Forms()
            {
                Form = new MetaDataModel { Id = Guid.NewGuid(),UrlId = Guid.NewGuid(), CreatorId = user.Id,CSS = new CSSModel() },
                Group = new List<TrackingModel>(),
                Responses = new List<ResponseModel>(),
                Settings = new SettingsModel()
            };
            await _collection.InsertOneAsync(forms);
            return forms;
        }

        public async Task<Forms> EditFrom(Guid id, string email)
        {
            var user = await userManager.FindByEmailAsync(email);

            if(user is null)
                throw new Exception("User Not Found");

            var form = await _collection.Find(a => a.Form.Id == id && a.Form.CreatorId == user.Id).FirstOrDefaultAsync();
            if (form is null)
                throw new Exception("Form not Present");

            return form;
        }

        public async Task<IdentityResult> AddResponse(UserResponse model,string email)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                throw new Exception("User not Found");

            var form = await _collection.Find(a => a.Form.UrlId == model.FormId).FirstOrDefaultAsync();
            if (form is null)
                throw new Exception("Form not Present");

            if (form.AcceptResponse)
                throw new Exception("Form Closed");
            bool formAded = false;
            if (form.Settings.IsGroup)
            {
                var groups = form.Group;
                foreach (var group in groups)
                {
                    var indexOfGroup = groups.IndexOf(group);
                    var isPresent = group.Participants.Find(a => a.Email == user.Email);
                    if (isPresent is not null)
                    {
                        var index = group.Participants.IndexOf(isPresent);

                        var update = Builders<Forms>.Update.Set(a => a.Group[indexOfGroup].Participants[index].Filled, true);
                        var result = await _collection.UpdateOneAsync(a => a.Group[indexOfGroup].Participants[index].Email == isPresent.Email, update);

                        ResponseModel responseModel = new()
                        {
                            Id = Guid.NewGuid(),
                            Response = model.Response,
                            FormId = model.FormId,
                            UserId = user.Id
                        };
                        var formFilter = Builders<Forms>.Filter.Eq("_id", ObjectId.Parse(form.Id));

                        var push = Builders<Forms>.Update.Push("Responses", responseModel);

                        var added = _collection.UpdateOne(formFilter, push);
                        if (added.IsModifiedCountAvailable)
                        { 
                            formAded = true;
                            return IdentityResult.Success;
                        }
                        
                    }
                }
            }

            if (form.Settings.IsAnonymous && !formAded)
            {
                ResponseModel responseModel = new()
                {
                    Id = Guid.NewGuid(),
                    Response = model.Response,
                    FormId = model.FormId,
                    UserId = user.Id
                };
                var formFilter = Builders<Forms>.Filter.Eq("_id", ObjectId.Parse(form.Id));

                var push = Builders<Forms>.Update.Push("Responses", responseModel);

                var added = _collection.UpdateOne(formFilter, push);
                if (added.IsModifiedCountAvailable)
                {
                    formAded = true;
                    return IdentityResult.Success;
                }
            }

            throw new Exception("Edge Case Missed");
        }

        public async Task<MetaDataModel> ViewForm(Guid id, string email)
        {
            var user = await userManager.FindByEmailAsync(email);

            if (user is null)
                throw new Exception("User Not Found");

            var form = await _collection.Find(a => a.Form.UrlId == id).FirstOrDefaultAsync();
            if (form is null)
                throw new Exception("Form not Present");

            if (form.AcceptResponse)
                throw new Exception("Form Closed");

            if(form.Settings.IsGroup)
            {
                var groups = form.Group;
                foreach (var group in groups)
                {
                    var indexOfGroup = groups.IndexOf(group);
                    var isPresent = group.Participants.Find(a=>a.Email==user.Email);
                    if (isPresent is not null)
                    {
                        var index = group.Participants.IndexOf(isPresent);

                        var update = Builders<Forms>.Update.Set(a=> a.Group[indexOfGroup].Participants[index].Seen, true);
                        var part = _collection.Find(a => a.Group[indexOfGroup].Participants[index].Email == isPresent.Email).FirstOrDefault();
                        var result = await _collection.UpdateOneAsync(a => a.Group[indexOfGroup].Participants[index].Email == isPresent.Email, update);
                        
                        return form.Form;
                    }
                }
            }

            if (form.Settings.IsAnonymous)
                return form.Form;

            throw new Exception("Edge Case Missed");
        }

        public async Task<IdentityResult> UpdateMeta(string email, MetaDataModel meta)
        {
            var user = await userManager.FindByEmailAsync(email);
            if(user is null)
                throw new Exception("User Not Found");

            var form= await _collection.Find(a => a.Form.Id == meta.Id).FirstOrDefaultAsync();
            if (form is null)
                throw new Exception("Form not Present");

            if (form.Form.CreatorId != user.Id)
                throw new Exception("User Not Authorized");

            var filter = Builders<Forms>.Filter.Eq(x => x.Form.Id, meta.Id);
            var update = Builders<Forms>.Update
              .Set(x=>x.Form.FormName,meta.FormName)
              .Set(x => x.Form.Title, meta.Title)
              .Set(x => x.Form.Description, meta.Description)
              .Set(x => x.Form.CSS, meta.CSS)
              .Set(x=>x.Form.Questions, meta.Questions);

            var result = await _collection.UpdateOneAsync(filter: filter, update: update);

            return result.IsAcknowledged ? IdentityResult.Success : IdentityResult.Failed();

        }

        public async Task<IdentityResult> UpdateSettings(string email, SettingsModel settings,Guid id)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                throw new Exception("User Not Found");

            var form = await _collection.Find(a => a.Form.Id == id).FirstOrDefaultAsync();
            if (form is null)
                throw new Exception("Form not Present");

            if (form.Form.CreatorId != user.Id)
                throw new Exception("User Not Authorized");

            var filter = Builders<Forms>.Filter.Eq(x => x.Form.Id, id);
            var update = Builders<Forms>.Update
              .Set(x => x.Settings, settings);

            var result = await _collection.UpdateOneAsync(filter: filter, update: update);

            return result.IsAcknowledged ? IdentityResult.Success : IdentityResult.Failed();

            //.Set(x => x.Settings.IsAnonymous, settings.IsAnonymous)
            //  .Set(x => x.Settings.IsMultiple, settings.IsMultiple)
            //  .Set(x => x.Settings.IsEditable, settings.IsEditable)
            //  .Set(x => x.Settings.IsPublic, settings.IsPublic)
            //  .Set(x => x.Settings.IsPublic, settings.IsPublic)
            //  .Set(x => x.Settings.IsGroup, settings.IsGroup)
            //  .Set(x => x.Settings.IsPrivate, settings.IsPrivate)
            //  .Set(x => x.Settings.IsTimeBound, settings.IsTimeBound)
            //  .Set(x => x.Settings.StartTime, settings.StartTime)
            //  .Set(x => x.Settings.EndTime, settings.EndTime)
            //  .Set(x => x.Settings.IsResponseLimit, settings.IsResponseLimit)
            //  .Set(x => x.Settings.IsResponseLimitPerUser, settings.IsResponseLimitPerUser)
            //  .Set(x => x.Settings.ResponseLimitPerUser, settings.ResponseLimitPerUser)
            //  .Set(x => x.Settings.ShowProgressBar, settings.ShowProgressBar)
            //  .Set(x => x.Settings.ConfirmationMessage, settings.ConfirmationMessage)
        }

        public async Task<IdentityResult> AddGroupToForm(Guid groupId, Guid formId, string email)
        {
            var group = await _group.Find(a => a.GroupId == groupId).FirstOrDefaultAsync();

            if (group is null)
                throw new Exception("Group not Present");

            var form = await _collection.Find(a=>a.Form.Id.Equals(formId)).FirstOrDefaultAsync();
            if (form is null)
                throw new Exception("Form not Present");

            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                throw new Exception("User not Present");

            if (form.Form.CreatorId != user.Id||group.Creator!=user.Id||group.Creator!=form.Form.CreatorId)
                throw new Exception("User Not Authorized");
            

            List<Tracker> trackers = new List<Tracker>();
            if (group.GroupParticipant is not null)
            {
                foreach (var item in group.GroupParticipant)
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

            TrackingModel trackingModel = new TrackingModel
            {
                GroupId = groupId,
                GroupName = group.GroupName,
                Participants  = trackers
            };

            var filter = Builders<Forms>.Filter.Eq(x => x.Form.Id, formId);
            var update = Builders<Forms>.Update
              .AddToSet(x => x.Group, trackingModel)
              .Set(x=>x.Settings.IsGroup,true);

            var result = await _collection.UpdateOneAsync(filter: filter, update: update);
            return result.IsAcknowledged ? IdentityResult.Success : IdentityResult.Failed();
        }

        public async Task<IdentityResult> DeleteGroupFromForm(Guid groupId,Guid formId, string email)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                throw new Exception("User Not Found");

            var group = await _group.Find(a => a.GroupId == groupId).FirstOrDefaultAsync();
            if (group is null)
                throw new Exception("Group not Found");

            var form = await _collection.Find(a => a.Form.Id == formId).FirstOrDefaultAsync();
            if (form is null)
                throw new Exception("Form not Found");

            if (group.Creator != user.Id && group.Creator != form.Form.CreatorId && form.Form.CreatorId != user.Id)
                throw new Exception("Not authorised to delete the Group from Form");

            TrackingModel trackingModel = new()
            {
                GroupId = group.GroupId,
                GroupName = group.GroupName
            };
            
            var groupIndex = form.Group.IndexOf(trackingModel);

            var update = Builders<Forms>.Update.PullFilter(a => a.Group, a => a.GroupId == group.GroupId);

            var result = _collection.UpdateOne(a => a.Group[groupIndex].GroupId == group.GroupId, update);

            if (result.ModifiedCount == 0)
                throw new Exception("Group Not Deleted From Form");
            return IdentityResult.Success;
        }
    }
}
