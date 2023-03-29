using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Server.Models;
using Server.Services;
using System.IdentityModel.Tokens.Jwt;


namespace Server.Controllers
{
    [Route("api/[controller]/[action]")]
    [EnableCors("AllowAll")]
    [ApiController]
    [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class GroupController : ControllerBase
    {
        private readonly IGroupServices services;

        public GroupController(IGroupServices services)
        {
            this.services = services;
        }
        private string UserIdFromToken()
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            var token = HttpContext.Request.Headers["Authorization"].ToString();

            token = token.Substring(7);

            var jwtToken = (JwtSecurityToken)tokenHandler.ReadToken(token);
            var userId = (jwtToken.Claims.First(x => x.Type == "email").Value);
            return userId;
        }

        [HttpPost]
        public async Task<IActionResult> AddGroup([FromBody] Group group)
        {
            var user = UserIdFromToken();
            try
            {
                var result = await services.AddGroup(group, user);
                if (result.Succeeded)
                    return Ok("Group Added");

                return BadRequest(result.Errors);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetGroups()
        {
            var user = UserIdFromToken();
            try
            {
                var result = await services.GetGroups(user);
                return Ok(result);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGroup(Guid id)
        {
            var user = UserIdFromToken();
            try
            {
                var result = await services.DeleteGroup(id, user);
                if (result.Succeeded)
                    return Ok(result);
                else
                    return BadRequest("Group not Deleted");
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPut("{groupId}")]
        public async Task<IActionResult> AddMemberToGroup([FromBody] List<string> participants, Guid groupId)
        {
            var user = UserIdFromToken();
            try
            {
                var result = await services.AddMemberToGroup(participants, groupId, user);
                if (result.Succeeded)
                    return Ok(result);
                else
                {
                    return BadRequest("Some Error Happend");
                }
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("{formId}")]
        public async Task<IActionResult> FetchMembers(Guid formId)
        {
            var user = UserIdFromToken();
            try
            {
                var result = await services.FetchMembers(formId, user);
                if (result.Succeeded)
                    return Ok(result);
                else
                    return BadRequest(result);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpDelete("{groupId}")]
        public async Task<IActionResult> DeleteMemberFromGroup([FromBody]List<string> emails,Guid groupId)
        {
            var user = UserIdFromToken();
            try
            {
                var result = await services.DeleteMemberFromGroup(emails,groupId,user);
                if (result.Succeeded)
                    return Ok(result);
                else
                    return BadRequest("Members not deleted from Group");
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }
}
