using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using Server.Models;
using Server.Services;
using System.IdentityModel.Tokens.Jwt;

namespace Server.Controllers
{
    [Route("api/[controller]/[action]")]
    [EnableCors("AllowAll")]
    [ApiController]
    [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class FormController : ControllerBase
    {
        private readonly IMongoDbServices mongoDbServices;

        public FormController(IMongoDbServices mongoDbServices)
        {
            this.mongoDbServices = mongoDbServices;
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

        [HttpGet]
        public async Task<IActionResult> AllForms()
        {
            string userId = UserIdFromToken();
            try
            {
                var result = await mongoDbServices.GetAllFormsAsync(userId);
                return Ok(result);
            }
            catch (Exception e)
            {
                if (e.Message == "User Not Found")
                {
                    return NotFound("User Not Found");
                }
                else
                {
                    return BadRequest(e.Message);
                }
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> EditForm(Guid id)
        {
            string userEmail = UserIdFromToken();
            try
            {
                var doc = await mongoDbServices.EditFrom(id, userEmail);
                return Ok(doc);
            }
            catch (Exception e)
            {
                if (e.Message == "User Not Found")
                    return NotFound("User Not Found");
                else if (e.Message == "Form not Present")
                    return BadRequest("No form present with Such Id");
                else
                    return BadRequest(e.Message);
            }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateMeta([FromBody] MetaDataModel model)
        {
            string userEmail = UserIdFromToken();
            try
            {
                var doc = await mongoDbServices.UpdateMeta(userEmail, model);
                if (doc.Succeeded)
                { return Ok(doc); }

                return BadRequest(doc.Errors);
            }
            catch (Exception e)
            {
                if (e.Message == "User Not Found")
                    return NotFound("User Not Found");
                else if (e.Message == "Form not Present")
                    return BadRequest("No form present with Such Id");
                else if (e.Message == "User Not Authorized")
                    return Unauthorized("You are not authorised to Edit the Form");
                else
                    return BadRequest(e.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSettings([FromBody] SettingsModel model, Guid id)
        {
            string userEmail = UserIdFromToken();
            try
            {
                var doc = await mongoDbServices.UpdateSettings(userEmail, model, id);
                if (doc.Succeeded)
                { return Ok(doc); }

                return BadRequest(doc.Errors);
            }
            catch (Exception e)
            {
                if (e.Message == "User Not Found")
                    return NotFound("User Not Found");
                else if (e.Message == "Form not Present")
                    return BadRequest("No form present with Such Id");
                else if (e.Message == "User Not Authorized")
                    return Unauthorized("You are not authorised to Edit the Form");
                else
                    return BadRequest(e.Message);
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> ViewForm(Guid id)
        {
            string userId = UserIdFromToken();
            try
            {
                var doc = await mongoDbServices.ViewForm(id, userId);
                if (doc is null)
                    return BadRequest("Some Error Occurred");
                return Ok(doc);
            }
            catch (Exception e)
            {
                if (e.Message == "User Not Found")
                {
                    return NotFound("User not Present");
                }
                else if (e.Message == "Form not Present")
                {
                    return NotFound("Form Not Found");
                }
                else if (e.Message == "Form Closed")
                {
                    return StatusCode(503, "Form Closed");
                }
                else
                {
                    return BadRequest(e.Message);
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> AddNewForm()
        {
            string userEmail = UserIdFromToken();
            try
            {
                var form = await mongoDbServices.AddForm(userEmail);
                return Ok(form);
            }
            catch (Exception e)
            {
                if (e.Message == "User Not Found")
                {
                    return NotFound("User Not Found");
                }
                else
                {
                    return BadRequest(e.Message);
                }
            }
        }

        [HttpPut("{groupId}/{formId}")]
        public async Task<IActionResult> AddGroupToForm(Guid groupId, Guid formId)
        {
            string userEmail = UserIdFromToken();
            try
            {
                var doc = await mongoDbServices.AddGroupToForm(groupId, formId, userEmail);
                if (doc.Succeeded)
                { return Ok(doc); }

                return BadRequest(doc.Errors);
            }
            catch (Exception e)
            {
                if (e.Message == "User not Present")
                    return NotFound("User not Present");
                else if (e.Message == "Form not Present")
                    return BadRequest("No form present with Such Id");
                else if (e.Message == "User Not Authorized")
                    return Unauthorized("You are not authorised to Edit the Form");
                else if (e.Message == "Group not Present")
                    return Unauthorized("Seleted Group is not Present");
                else
                    return BadRequest(e.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddResponse(UserResponse model)
        {
            var user = UserIdFromToken();

            try
            {
                var result = await mongoDbServices.AddResponse(model, user);
                return Ok(result);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpDelete("{groupId}/{formId}")]
        public async Task<IActionResult> DeleteGroupFromForm(Guid groupId,Guid formId)
        {
            var user = UserIdFromToken();
            try
            {
                var result = await mongoDbServices.DeleteGroupFromForm(groupId, formId, user);
                if (result.Succeeded)
                    return Ok(result);
                else
                    return BadRequest("Group not Removed from Form");
            }
            catch(Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddReminder([FromBody]RequestReminder requestReminder)
        {
            var user = UserIdFromToken();
            try
            {
                var result = await mongoDbServices.AddReminder(requestReminder, user);
                if (result.Succeeded)
                    return Ok(result);
                else
                    return BadRequest("Reminder not Sent Successfully");
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ReminderList()
        {
            var user = UserIdFromToken();
            try
            {
                var result = await mongoDbServices.ReminderList(user);
                if (result is not null)
                    return Ok(result);
                else
                    return BadRequest("Some Error Occurred");
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> ViewReminder(string id)
        {
            var user = UserIdFromToken();
            try
            {
                var result = await mongoDbServices.ViewReminder(id,user);
                if (result.Succeeded)
                    return Ok(result);
                else
                    return BadRequest("Some Error Occurred");
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> MarkAsUnRead(string id)
        {
            var user = UserIdFromToken();
            try
            {
                var result = await mongoDbServices.MarkAsUnRead(id, user);
                if (result.Succeeded)
                    return Ok(result);
                else
                    return BadRequest("Some Error Occurred");
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReminder(string id)
        {
            var user = UserIdFromToken();
            try
            {
                var result = await mongoDbServices.DeleteReminder(id, user);
                if (result.Succeeded)
                    return Ok(result);
                else
                    return BadRequest("Reminder not Deleted");
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> ChangeStatus(Guid id)
        {
            var user = UserIdFromToken();
            try
            {
                var result = await mongoDbServices.ChangeStatus(id, user);
                if (result.Succeeded)
                    return Ok(result);
                else
                    return BadRequest("Reminder not Deleted");
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }
}
