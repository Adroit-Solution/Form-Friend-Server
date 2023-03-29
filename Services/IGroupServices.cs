using Microsoft.AspNetCore.Identity;
using Server.Models;

namespace Server.Services
{
    public interface IGroupServices
    {
        Task<IdentityResult> AddGroup(Group group, string email);
        Task<IdentityResult> AddMemberToGroup(List<string> participants, Guid groupId, string email);
        Task<IdentityResult> DeleteGroup(Guid id, string email);
        Task<IdentityResult> DeleteMemberFromGroup(List<string> participants, Guid groupId, string email);
        Task<IdentityResult> FetchMembers(Guid formId, string email);
        Task<List<GroupModel>> GetGroups(string email);
    }
}