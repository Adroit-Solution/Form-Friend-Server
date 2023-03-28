using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        public async Task<IActionResult> AddGroup([FromBody]Group group)
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
                var result = await services.DeleteGroup(id,user);
                return Ok(result);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }
}
