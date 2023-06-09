﻿using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Models;

namespace Server.Services
{
    public interface IMongoDbServices
    {
        Task<IEnumerable<dynamic>> GetAllFormsAsync(string email);
        Task<Forms> AddForm(string email);
        Task<Forms> EditFrom(Guid id, string email);
        Task<MetaDataModel> ViewForm(Guid id, string email);
        Task<IdentityResult> UpdateMeta(string email, MetaDataModel meta);
        Task<IdentityResult> UpdateSettings(string email, SettingsModel settings, Guid id);
        Task<IdentityResult> AddGroupToForm(Guid groupId, Guid formId, string email);
        Task<IdentityResult> AddResponse(UserResponse model, string email);
        Task<IdentityResult> DeleteGroupFromForm(Guid groupId, Guid formId, string email);
        Task<IdentityResult> AddReminder(RequestReminder requestReminder, string email);
        Task<List<ResponseReminder>> ReminderList(string email);
        Task<IdentityResult> ViewReminder(string id, string email);
        Task<IdentityResult> MarkAsUnRead(string id, string email);
        Task<IdentityResult> DeleteReminder(string id, string email);
        Task<IdentityResult> ChangeStatus(Guid formId, string email);
        IdentityResult DeleteForm();
        Task<Forms> AddTemplate(Guid id, string email);
        Task<Template> AddNewTemplate();
        List<Template> GetTemplate(string email);
        void SendEmail(Email email);
        Task<IdentityResult> SendReminder();
    }
}