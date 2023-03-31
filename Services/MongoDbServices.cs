using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using DnsClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Server.Models;
using System;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography.Xml;
using ZstdSharp.Unsafe;
using System.Runtime.InteropServices;

namespace Server.Services
{
    public class MongoDbServices : IMongoDbServices
    {
        private readonly IMongoCollection<Forms> _collection;
        private readonly IMongoCollection<Template> _Template;
        private readonly IMongoCollection<GroupModel> _group;
        private readonly IMongoCollection<ReminderModel> _reminder;
        private readonly UserManager<User> userManager;

        public MongoDbServices(IOptions<MongoDBSettings> settings, UserManager<User> userManager)
        {
            var client = new MongoClient(settings.Value.ConnectionURI);
            var database = client.GetDatabase(settings.Value.DatabaseName);
            _collection = database.GetCollection<Forms>("forms");
            _group = database.GetCollection<GroupModel>("group");
            _reminder = database.GetCollection<ReminderModel>("remainder");
            _Template = database.GetCollection<Template>("template");
            this.userManager = userManager;
        }

        public async Task<IEnumerable<dynamic>> GetAllFormsAsync(string email)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                throw new Exception("User Not Found");


            var result =  _collection.Find(a => a.Form.CreatorId == user.Id).ToList().Select(a=>new { a.Form.Id,a.Form.FormName,a.Form.CreatedOn,a.Form.LastEdited});
            return result;

        }

        public async Task<Forms> AddForm(string email)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                throw new Exception("User Not Found");
            Forms forms = new Forms()
            {
                Form = new MetaDataModel { Id = Guid.NewGuid(),UrlId = Guid.NewGuid(), CreatorId = user.Id,CSS = new CSSModel(),CreatedOn = DateTime.UtcNow,LastEdited = DateTime.UtcNow },
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

            if (form.Settings.IsTimeBound)
                if (form.Settings.EndTime < DateTime.UtcNow|| form.Settings.StartTime > DateTime.UtcNow)
                    throw new Exception("Form is Closed");

            if (form.Settings.IsResponseLimit)
            {
                if (form.Responses is not null)
                {
                    var count = form.Responses.Count();
                    if(count>=form.Settings.ResponseLimit) { throw new Exception("Response Limit Achieved"); }
                }
            }

            if(form.Settings.IsResponseLimitPerUser)
            {
                if (form.Responses is not null)
                {
                    var response = form.Responses.FindAll(a => a.UserId == user.Id);
                    if (response.Count()>=form.Settings.ResponseLimitPerUser)
                        throw new Exception("You have already filled the Form");
                }
            }

            if(!form.Settings.IsMultiple)
            {
                if (form.Responses is not null)
                {
                    var response = form.Responses.FindAll(a => a.UserId == user.Id);
                    if (response.Count() >= 1)
                        throw new Exception("You have already filled the Form");
                }
            }

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
                if (added.ModifiedCount>0)
                {
                    formAded = true;
                    return IdentityResult.Success;
                }
                else
                {
                    throw new Exception("Response not added");
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

            if (form.Settings.IsTimeBound)
                if (form.Settings.EndTime < DateTime.UtcNow || form.Settings.StartTime > DateTime.UtcNow)
                    throw new Exception("Form is Closed");

            if (form.Settings.IsResponseLimit)
            {
                if (form.Responses is not null)
                {
                    var count = form.Responses.Count();
                    if (count >= form.Settings.ResponseLimit) { throw new Exception("Response Limit Achieved"); }
                }
            }

            if (form.Settings.IsResponseLimitPerUser || !form.Settings.IsMultiple)
            {
                if (form.Responses is not null)
                {
                    var response = form.Responses.FindAll(a => a.UserId == user.Id);
                    if (response.Count() >= form.Settings.ResponseLimitPerUser)
                        throw new Exception("You have already filled the Form");
                }
            }

            if (form.Settings.IsGroup)
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
              .Set(x => x.Form.FormName, meta.FormName)
              .Set(x => x.Form.Title, meta.Title)
              .Set(x => x.Form.Description, meta.Description)
              .Set(x => x.Form.LastEdited, DateTime.UtcNow)
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
              .Set(x => x.Settings, settings)
              .Set(x=>x.Form.LastEdited,DateTime.UtcNow);

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

            var toDelete = form.Group.Find(a => a.GroupId == groupId);
            if (toDelete is null)
                return IdentityResult.Success;
            var groupIndex = form.Group.IndexOf(toDelete);

            var update = Builders<Forms>.Update.PullFilter(a => a.Group, a => a.GroupId == group.GroupId);

            var result = _collection.UpdateOne(a => a.Group[groupIndex].GroupId == group.GroupId, update);

            if (result.ModifiedCount == 0)
                throw new Exception("Group Not Deleted From Form");
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> AddReminder(RequestReminder requestReminder, string email)
        {
            var admin = await userManager.FindByNameAsync(email);
            if (admin is null)
                throw new Exception("User not Found");

            var form = await _collection.Find(a=>a.Form.Id==requestReminder.FormId).FirstOrDefaultAsync();
            if (form is null)
                throw new Exception("Form not Found");

            var group = await _group.Find(a => a.GroupId == requestReminder.GroupId).FirstOrDefaultAsync();
            if (group is null)
                throw new Exception("Group not Present");

            if (admin.Id != group.Creator)
                throw new Exception("Not Authorized to send Notification");

            List<ReminderModel> reminders = new List<ReminderModel>();

            if (group.GroupParticipant is null)
                return IdentityResult.Success;

            foreach (var participant in requestReminder.Participants)
            {
                var user = await userManager.FindByEmailAsync(participant);
                if (user is null)
                    continue;

                if (!group.GroupParticipant.Contains(participant))
                    continue;

                ReminderModel reminderModel = new()
                {
                    AdminName = admin.Name,
                    Group = group.GroupId,
                    GroupName = group.GroupName,
                    Message = requestReminder.Message,
                    User = user.Id,
                    FromId = requestReminder.FormId
                };
                reminders.Add(reminderModel);

                //SendEmail(user.FirstName,email,requestReminder.Message);
            }

            await _reminder.InsertManyAsync(reminders);
            return IdentityResult.Success;

        }

        public async Task<List<ResponseReminder>> ReminderList(string email)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                throw new Exception("User Not Present");

            var reminders = _reminder.Find(a => a.User == user.Id).ToList();

            List<ResponseReminder> response = new();

            foreach (var reminder in reminders)
            {
                ResponseReminder responseModel = new()
                {
                    Id = reminder.Id,
                    AdminName = reminder.AdminName,
                    GroupName = reminder.GroupName,
                    GroupId = reminder.Group,
                    IsSeen = reminder.IsSeen,
                    Message = reminder.Message,
                    FormId = reminder.FromId
                };
                response.Add(responseModel);
            }


            return response;

        }

        public async Task<IdentityResult> ViewReminder(string id,string email)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                throw new Exception("User Not Present");

            var reminder = await _reminder.Find(a => a.User == user.Id&&a.Id==id).FirstOrDefaultAsync();

            if (reminder is null)
                throw new Exception("No such Notification Found");

            var filter = Builders<ReminderModel>.Filter.Eq(a=>a.Id, id);
            var update = Builders<ReminderModel>.Update
              .Set(x => x.IsSeen, true);

            var result = await _reminder.UpdateOneAsync(filter: filter, update: update);

            return result.ModifiedCount>0 ? IdentityResult.Success : IdentityResult.Failed();
        }

        public async Task<IdentityResult> MarkAsUnRead(string id, string email)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                throw new Exception("User Not Present");

            var reminder = await _reminder.Find(a => a.User == user.Id && a.Id == id).FirstOrDefaultAsync();

            if (reminder is null)
                throw new Exception("No such Notification Found");

            var filter = Builders<ReminderModel>.Filter.Eq(a => a.Id, id);
            var update = Builders<ReminderModel>.Update
              .Set(x => x.IsSeen, false);

            var result = await _reminder.UpdateOneAsync(filter: filter, update: update);

            return result.ModifiedCount > 0 ? IdentityResult.Success : IdentityResult.Failed();
        }

        public async Task<IdentityResult> DeleteReminder(string id, string email)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                throw new Exception("User Not Present");

            var reminder = await _reminder.Find(a => a.User == user.Id && a.Id == id).FirstOrDefaultAsync();

            if (reminder is null)
                throw new Exception("No such Notification Found");

            var filter = Builders<ReminderModel>.Filter.Eq(a => a.Id, id);
            

            var result = await _reminder.FindOneAndDeleteAsync(filter: filter);

            if (result is null)
                return IdentityResult.Failed();
            else
                return IdentityResult.Success;
        }

        public async Task<IdentityResult> ChangeStatus(Guid formId, string email)
        {
            var user = await userManager.FindByEmailAsync(email);
            if(user is null)
            {
                throw new Exception("User not Found");
            }



            var form = await _collection.Find(a => a.Form.Id == formId).FirstOrDefaultAsync();
            if (form is null)
                throw new Exception("Form not Found");

            if (user.Id != form.Form.CreatorId)
                throw new Exception("Not Authorized to Change the Status");

            var filter = Builders<Forms>.Filter.Eq(a => a.Form.Id, form.Form.Id);

            var update = Builders<Forms>.Update.Set(a => a.AcceptResponse, !form.AcceptResponse);
            var result = await  _collection.UpdateOneAsync(filter: filter, update: update);

            return result.ModifiedCount > 0 ? IdentityResult.Success : IdentityResult.Failed();
        }

        private void SendEmail(string name,string email,string body)
        {
            UserCredential credential;

            using (var stream = new FileStream("../../../../../credentials.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = "token.json";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    new[] { GmailService.Scope.GmailSend },
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }

            var service = new GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Form Friend",
            });

            var message = new MimeKit.MimeMessage();
            message.From.Add(new MimeKit.MailboxAddress("Form Friend", "agrawalvishesh9271@gmail.com"));
            message.To.Add(new MimeKit.MailboxAddress(name, email));
            message.Subject = "New Notification";
            message.Body = new MimeKit.TextPart("plain")
            {
                Text = body
            };

            var rawMessage = Base64UrlEncode(message.ToString());
            var gmailMessage = new Message { Raw = rawMessage };
            var request = service.Users.Messages.Send(gmailMessage, "me");
            request.Execute();
        }

        private static string Base64UrlEncode(string input)
        {
            var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
            // Special "url-safe" base64 encode.
            return Convert.ToBase64String(inputBytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .Replace("=", "");
        }

        public async Task<Forms> AddTemplate(Guid id, string email)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                throw new Exception("User is not Present");

            var template = await _Template.Find(a=>a.TemplateId== id).FirstOrDefaultAsync();
            if (template is null)
                throw new Exception("Template Not Found");

            Forms forms = new Forms()
            {
                Form = new MetaDataModel { Id = Guid.NewGuid(), UrlId = Guid.NewGuid(), CreatorId = user.Id, CSS = template.CSS, CreatedOn = DateTime.UtcNow, LastEdited = DateTime.UtcNow,Questions = template.Questions,Description = template.Description,FormName = template.FormName,Title = template.Title },
                Group = new List<TrackingModel>(),
                Responses = new List<ResponseModel>(),
                Settings = new SettingsModel()
            };
            await _collection.InsertOneAsync(forms);
            return forms;
        }

        public async Task<Template> AddNewTemplate()
        {

            Template forms = new Template()
            {
                Id = Guid.NewGuid(),
                Title = "Feedback",
                Description = "Give your feedback",
                CSS= new CSSModel(),
                Questions = new List<QuestionModel> 
                { 
                    new QuestionModel { Id = Guid.NewGuid(), Question = "Name", Type = "Text", Options = new List<string>() }, 
                    new QuestionModel { Id = Guid.NewGuid(), Question = "Email", Type = "Email", Options = new List<string>() }, 
                    new QuestionModel { Id = Guid.NewGuid(), Question = "Your Feedback", Type = "TextArea", Options = new List<string>() } 
                },
                TemplateId = Guid.NewGuid(),
                FormName = "Feedback Form",
            };

            Template forms1 = new Template()
            {
                Id = Guid.NewGuid(),
                Title = "Workshop Registration",
                Description = "Register Yourself",
                CSS = new CSSModel(),
                Questions = new List<QuestionModel>
                {
                    new QuestionModel { Id = Guid.NewGuid(), Question = "Name", Type = "Text", Options = new List<string>() },
                    new QuestionModel { Id = Guid.NewGuid(), Question = "Email", Type = "Email", Options = new List<string>() },
                    new QuestionModel { Id = Guid.NewGuid(), Question = "Enrollment No.", Type = "Number", Options = new List<string>() },
                    new QuestionModel { Id = Guid.NewGuid(), Question = "Branch Name", Type = "Radio", Options = new List<string>{ "IT","Computer","Mechanical"} }
                },
                TemplateId = Guid.NewGuid(),
                FormName = "Workshop Registration",
            };

            Template forms2 = new Template()
            {
                Id = Guid.NewGuid(),
                Title = "Webinar",
                Description = "Register Yourself for Webinar",
                CSS = new CSSModel(),
                Questions = new List<QuestionModel>
                {
                    new QuestionModel { Id = Guid.NewGuid(), Question = "Name", Type = "Text", Options = new List<string>() },
                    new QuestionModel { Id = Guid.NewGuid(), Question = "Email", Type = "Email", Options = new List<string>() },
                    new QuestionModel { Id = Guid.NewGuid(), Question = "Enrollment No.", Type = "Number", Options = new List<string>() },
                    new QuestionModel { Id = Guid.NewGuid(), Question = "Branch Name", Type = "Radio", Options = new List<string>{ "IT","Computer","Mechanical"} }
                },
                TemplateId = Guid.NewGuid(),
                FormName = "Webinar Registration",
            };
            await _Template.InsertOneAsync(forms);
            await _Template.InsertOneAsync(forms1);
            await _Template.InsertOneAsync(forms2);
            return forms;
        }

        public IdentityResult DeleteForm()
        {
            _collection.DeleteMany(a=>a.Id==a.Id);
            return IdentityResult.Success;
        }

        public List<Template> GetTemplate(string email)
        {
            var template = _Template.Find(a => a.Id == a.Id).ToList();
            return template;
        }
    }
}
