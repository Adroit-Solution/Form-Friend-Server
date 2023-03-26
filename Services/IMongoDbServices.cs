using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Models;

namespace Server.Services
{
    public interface IMongoDbServices
    {
        Task<IEnumerable<Guid>> GetAllFormsAsync(string email);
        Task<Forms> AddForm(string email);
        Task<Forms> EditFrom(Guid id, string email);
        Task<MetaDataModel> ViewForm(Guid id, string email);
        Task<IdentityResult> UpdateMeta(string email, MetaDataModel meta);
        Task<IdentityResult> UpdateSettings(string email, SettingsModel settings, Guid id);
        Task<IdentityResult> AddGroupToForm(Guid groupId, Guid formId, string email);
    }
}