using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Server.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Nodes;
using System.Text;
using Microsoft.AspNetCore.Cors;

namespace Server.Controllers
{
    [Route("api/[controller]/[action]")]
    [EnableCors("AllowAll")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> userManager;
        private readonly SignInManager<User> signInManager;

        public AuthController(UserManager<User> userManager, SignInManager<User> signInManager)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody]UserModel user)
        {
            var isUser = await userManager.FindByEmailAsync(user.Email);
            if (isUser is not null)
                return BadRequest(new JsonObject { { "Error", "User already Present" } });

            User toAdd = new User()
            {
                Email = user.Email,
                UserName = user.UserName,
                Name = user.Name,
                Profile = "https://localhost:44314//Profile/user-solid.svg"
            };

            var result = await userManager.CreateAsync(toAdd,user.Password);

            if (result.Succeeded)
            {
                return Ok(new JsonObject() { { "Success", "User Signed Up" } });
            }

            return BadRequest(new JsonObject { { "Errors", result.Errors.First().Description } });

        }

        //Login the User and Generate JWT Token
        [HttpPost]
        public async Task<IActionResult> SignIn([FromBody] LoginModel signInModel)
        {
            var isUserPresent = await userManager.FindByEmailAsync(signInModel.Email);
            if (isUserPresent is null)
                return NotFound(new JsonObject() { { "Error", "User Not Found" } });
            var result = await signInManager.PasswordSignInAsync(signInModel.Email, signInModel.Password, signInModel.IsPersistent, false);

            if (result.Succeeded)
            {
                var authClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, isUserPresent.UserName),
                    new Claim(ClaimTypes.Email, isUserPresent.Email),
                    new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti , Guid.NewGuid().ToString())
                };

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("+)3@5!7#9$0%2^4&6*8(0"));
                var tokenDescriptor = new SecurityTokenDescriptor()
                {

                    Subject = new ClaimsIdentity(authClaims),
                    Expires = DateTime.UtcNow.AddDays(28),
                    SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature)
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var token = tokenHandler.CreateToken(tokenDescriptor);

                return Ok(new JsonObject { { "Success", "User Logged In" }, { "User", tokenHandler.WriteToken(token).ToString() }, { "Valid", token.ValidTo } });

            }
            return BadRequest(new JsonObject() { { "Error", "Wrong Password" } });
        }

    }
}
