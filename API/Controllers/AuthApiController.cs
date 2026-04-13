using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Org.BouncyCastle.Crypto.Generators;
using Repository.Interfaces;
using Repository.Models;
using BCrypt.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;


namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthApiController : Controller
    {
        private readonly IAuthInterface _auth;
        private readonly IConfiguration _config;
        // private readonly IWebHostEnvironment _env;

        public AuthApiController(IAuthInterface auth, IConfiguration config)
        {
            _auth = auth;
            _config = config;
        }

        [HttpPost("UserGoogleLogin")]
        public async Task<IActionResult> UserGoogleLogin([FromBody] vm_GoogleLogin model)
        {
            if (model == null || string.IsNullOrEmpty(model.c_Email))
                return BadRequest("Invalid Google data");

            // // 1. Check user in DB
            // var user = await _auth.GetUserByEmail(model.c_Email);

            // if (user == null)
            // {
            //     // 2. Naya user object method ke andar define karein
            //     var newUser = new t_UserRegister
            //     {
            //         c_FullName = model.c_FullName,
            //         c_Email = model.c_Email,
            //         c_UserName = model.c_Email.Split('@')[0], 
            //         // Google ID ko hash karke store karein
            //         c_PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.c_GoogleId), // Google ID as temporary pass
            //         c_Gender = "other"
            //     };

            //     int result = await _auth.UserRegister(newUser);
            //     if (result <= 0) return StatusCode(500, "Error registering new user");

            //     // Register hone ke baad user fetch karein taaki ID mil jaye
            //     user = await _auth.GetUserByEmail(model.c_Email);
            // }

            // // 3. JWT Token Generate karo
            // var token = GenerateJwtToken(user);

            // return Ok(new { token = token, message = "Login via Google Successful" });

            // 1. Pehle check karein ki kya user pehle se database mein hai?
            var existingUser = await _auth.GetUserByEmail(model.c_Email);

            if (existingUser != null)
            {
                // ✅ User pehle se hai! Seedha Token generate karke return karein
                var token = GenerateJwtToken(existingUser);
                return Ok(new { token = token, message = "Welcome back!" });
            }

            // 2. Agar user nahi hai, tabhi register karein
            var newUser = new t_UserRegister
            {
                c_FullName = model.c_FullName,
                c_Email = model.c_Email,
                c_UserName = model.c_Email.Split('@')[0],
                c_PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.c_GoogleId),
                c_Gender = "Other",
                // c_Password = "GoogleUser@123", // Validation pass karne ke liye
                // c_ConfirmPassword = "GoogleUser@123"
            };

            int result = await _auth.UserRegister(newUser);

            if (result > 0)
            {
                var registeredUser = await _auth.GetUserByEmail(model.c_Email);
                var token = GenerateJwtToken(registeredUser);
                return Ok(new { token = token, message = "Registration & Login Successful" });
            }

            return StatusCode(500, "Error registering new user");
        }


        // ================= REGISTER =================
        [HttpPost("UserRegister")]
        public async Task<IActionResult> UserRegister([FromForm] t_UserRegister model, IFormFile? image)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(model.c_Password))
            {
                return BadRequest("Password is missing from request");
            }
            // 🔥 PASSWORD HASH HERE
            model.c_PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.c_Password);


            if (image != null && image.Length > 0)
            {
                string rootPath = @"C:\Users\disha\casepoint\ARTIFY_PROJECT\Artify-The-Art-Gallery\MVC\wwwroot\";

                if (!Directory.Exists(rootPath))
                {
                    Directory.CreateDirectory(rootPath);
                }

                string uploadsFolder = Path.Combine(rootPath, "images");

                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName);
                string filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                model.c_ProfileImage = fileName;
            }


            int result = await _auth.UserRegister(model);
            // model.c_PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.c_Password);
            return result switch
            {
                0 => BadRequest("Email already exists"),
                -1 => BadRequest("Username already exists"),
                -99 => StatusCode(500, "Something went wrong"),
                _ => Ok(new { message = "User Registered Successfully" })
            };
        }

        // ================= LOGIN =================
        [HttpPost("UserLogin")]
        public async Task<IActionResult> UserLogin([FromBody] t_UserLogin model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);


            // ── STEP 2: reCAPTCHA server-side verify ──
            // if (string.IsNullOrWhiteSpace(model.c_CaptchaToken))
            //     return BadRequest(new { message = "CAPTCHA verification is required." });

            // bool captchaValid = await VerifyRecaptchaAsync(model.c_CaptchaToken);
            // if (!captchaValid)
            //     return BadRequest(new { message = "CAPTCHA verification failed. Please try again." });


            var user = await _auth.UserLogin(model);

            if (user == null)
                return Unauthorized("Invalid email or password");


            HttpContext.Session.SetString("UserEmail", user.c_Email);
            HttpContext.Session.SetString("UserName", user.c_UserName);


            if (string.IsNullOrEmpty(user.c_PasswordHash))
                return StatusCode(500, "Password hash missing in DB");

            bool isValid = BCrypt.Net.BCrypt.Verify(model.c_Password, user.c_PasswordHash);

            if (!isValid)
                return Unauthorized("Invalid password");

            // 🔹 Generate JWT Token
            var token = GenerateJwtToken(user);

            return Ok(new
            {
                message = "Login Successful",
                token,
                user = new
                {
                    user.c_UserId,
                    user.c_FullName,
                    user.c_UserName,
                    user.c_Email,
                    user.c_ProfileImage
                }
            });

        }


        [HttpPost("UserForgotPassword")]
        public async Task<IActionResult> UserForgotPassword([FromBody] t_ForgotPassword model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _auth.GetUserByEmail(model.c_Email);
            if (user == null)
                return NotFound("User not found");

            var otp = new Random().Next(100000, 999999).ToString();
            var expiryTime = DateTime.UtcNow.AddMinutes(5);

            HttpContext.Session.SetString("ForgotPasswordEmail", model.c_Email.Trim().ToLower());
            HttpContext.Session.SetString("ForgotPasswordOtp", otp);
            HttpContext.Session.SetString("ForgotPasswordOtpExpiry", expiryTime.ToString("o"));

            // yahan aap email service call karogi
            // await _emailService.SendForgotPasswordOtp(user.c_Email, user.c_FullName, otp);

            return Ok(new
            {
                message = "OTP sent successfully to registered email"
            });
        }

        [HttpPost("UserResetPassword")]
        public async Task<IActionResult> UserResetPassword([FromBody] t_ResetPassword model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var sessionEmail = HttpContext.Session.GetString("ForgotPasswordEmail");
            var sessionOtp = HttpContext.Session.GetString("ForgotPasswordOtp");
            var sessionExpiry = HttpContext.Session.GetString("ForgotPasswordOtpExpiry");

            if (string.IsNullOrEmpty(sessionEmail) || string.IsNullOrEmpty(sessionOtp) || string.IsNullOrEmpty(sessionExpiry))
                return BadRequest("OTP session expired. Please request OTP again.");

            if (sessionEmail != model.c_Email.Trim().ToLower())
                return BadRequest("Invalid email");

            if (sessionOtp != model.c_Otp.Trim())
                return BadRequest("Invalid OTP");

            if (DateTime.UtcNow > DateTime.Parse(sessionExpiry))
                return BadRequest("OTP expired");

            var result = await _auth.UpdatePassword(model.c_Email, model.c_NewPassword);

            if (result <= 0)
                return StatusCode(500, "Password reset failed");

            HttpContext.Session.Remove("ForgotPasswordEmail");
            HttpContext.Session.Remove("ForgotPasswordOtp");
            HttpContext.Session.Remove("ForgotPasswordOtpExpiry");

            return Ok(new
            {
                message = "Password updated successfully"
            });
        }


        // ================= JWT TOKEN =================
        private string GenerateJwtToken(t_UserRegister user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.c_UserId.ToString()),
                new Claim(ClaimTypes.Name, user.c_UserName),
                new Claim(ClaimTypes.Email, user.c_Email),
                new Claim(ClaimTypes.Role, "User") // 🔥 Role fix
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

    }
}