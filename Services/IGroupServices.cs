﻿using Microsoft.AspNetCore.Identity;
using Server.Models;

namespace Server.Services
{
    public interface IGroupServices
    {
        Task<IdentityResult> AddGroup(Group group, string email);
        Task<List<GroupModel>> GetGroups(string email);
    }
}