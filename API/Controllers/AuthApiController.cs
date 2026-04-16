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
using Repository.Services;


namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthApiController : Controller
    {
        private readonly IAuthInterface _auth;
        private readonly IConfiguration _config;
        private readonly EmailServices _emailService;
        private readonly RedisService _redis;
        // private readonly IWebHostEnvironment _env;

        public AuthApiController(IAuthInterface auth, IConfiguration config, EmailServices emailService, RedisService redis)
        {
            _auth = auth;
            _config = config;
            _emailService = emailService;
             _redis = redis;
        }

        [HttpPost("UserGoogleLogin")]
        public async Task<IActionResult> UserGoogleLogin([FromBody] vm_GoogleLogin model)
        {
            if (model == null || string.IsNullOrEmpty(model.c_Email))
                return BadRequest("Invalid Google data");
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
        public async Task<IActionResult> UserRegister([FromForm] t_UserRegister model, IFormFile? profileImageFile)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(model.c_Password))
            {
                return BadRequest("Password is missing from request");
            }
            // 🔥 PASSWORD HASH HERE
            model.c_PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.c_Password);


            if (profileImageFile != null && profileImageFile.Length > 0)
            {
                string apiDirectory = Directory.GetCurrentDirectory();
                string mvcWwwRoot = Path.GetFullPath(Path.Combine(apiDirectory, "..", "MVC", "wwwroot"));
                string uploadsFolder = Path.Combine(mvcWwwRoot, "UserImages");

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Use profileImageFile here
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(profileImageFile.FileName);
                string filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await profileImageFile.CopyToAsync(stream);
                }

                model.c_ProfileImage = fileName;
            }


            int result = await _auth.UserRegister(model);
            if (result > 0)
            {
                // 1. Template file read karein
                string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "UserRegisterEmail.html");
                string body = await System.IO.File.ReadAllTextAsync(templatePath);

                // 2. DYNAMIC NAME REPLACE:
                // Yahan 'model.c_UserName' aapki property ka naam hai jo register waqt aayi thi
                body = body.Replace("{UserName}", model.c_UserName) 
                        .Replace("{LoginUrl}", "http://localhost:5092/Auth/UserLogin");

                string logoPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "mvc", "wwwroot", "images", "Logo.jpeg"));

                // 3. Email send karein
                await _emailService.SendEmailAsync(model.c_Email, "Welcome to Artify!", body, logoPath);

                return Ok(new { message = "Registration successful" });
            }
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

            await _redis.SetOtpAsync(model.c_Email.ToLower().Trim(), otp);

            try
            {
                // Template Path (API ke andar hi hai)
                string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "UserOtpTemplate.html");
                string body = await System.IO.File.ReadAllTextAsync(templatePath);

                // Expiry time string banayein
                var expiryDisplay = DateTime.Now.AddMinutes(5).ToString("hh:mm tt");

                body = body.Replace("{{UserName}}", user.c_FullName)
                        .Replace("{{OTP}}", otp)
                        .Replace("2 minutes", $"2 minutes (Valid until {expiryDisplay})");  

                // // --- DYNAMIC LOGO PATH ---
                // // Maan lijiye structure hai: SolutionFolder/api aur SolutionFolder/mvc
                string logoPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "mvc", "wwwroot", "images", "Logo.jpeg"));

                // Service Call
                await _emailService.SendEmailAsync(user.c_Email, "Reset Password", body, logoPath);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error: " + ex.Message);
            }

            return Ok(new
            {
                message = "OTP sent successfully to registered email"
            });
        }

        [HttpPost("UserVerifyOtp")]
        public async Task<IActionResult> UserVerifyOtpAsync([FromBody] t_VerifyOtp model)
        {
            
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
                
             var email = model.c_Email.ToLower().Trim();

            var savedOtp = await _redis.GetOtpAsync(email);

            if (string.IsNullOrEmpty(savedOtp))
                return BadRequest("OTP not found. Please request OTP again.");

            if (savedOtp != model.c_Otp)
                return BadRequest("Invalid OTP");
            
            // Success hone par ye line ADD karein:
            await _redis.SetOtpVerifiedAsync(email);

            // // ✅ OTP Verified flag set karo
            // HttpContext.Session.SetString("OtpVerified", "true");


            return Ok(new { message = "OTP verified successfully" });
        }

        [HttpPost("UserResetPassword")]
        public async Task<IActionResult> UserResetPassword([FromBody] t_ResetPassword model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
 

            var email = model.c_Email.ToLower().Trim();

            // ✅ Check verified
            var isVerified = await _redis.IsOtpVerifiedAsync(email);

            if (!isVerified)
                return BadRequest("OTP not verified");
                
            // model.c_NewPassword ko hash karein
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.c_NewPassword);

            // Ab hashed password ko bhejien na ki plain password ko
            var result = await _auth.UpdatePassword(model.c_Email, hashedPassword);

            if (result > 0)
            {
                // 1. Template Path
                string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "UserPasswordChange.html");
                string body = await System.IO.File.ReadAllTextAsync(templatePath);

                // 2. Placeholder replacement
                // Note: LoginUrl ko aap apne MVC login page ka absolute URL dein
                string loginUrl = " http://localhost:5092/Auth/UserLogin";

                body = body.Replace("{{UserName}}", model.c_Email) // Aap user object se name le sakte hain
                           .Replace("{{LoginUrl}}", loginUrl);

                // 3. Dynamic Logo Path (Git-friendly)
                string logoPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "mvc", "wwwroot", "images", "Logo.jpeg"));

                // 4. Send Success Email
                await _emailService.SendEmailAsync(model.c_Email, "Security Alert: Password Changed", body, logoPath);

                 // ✅ Cleanup after success
                await _redis.DeleteOtpAsync(email);
                await _redis.DeleteVerifiedFlagAsync(email);

               
                // Clear Sessions...
                return Ok(new { message = "Password updated successfully" });
            }
            if (result <= 0)
                return StatusCode(500, "Password reset failed");

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
                new Claim("user_id", user.c_UserId.ToString()), // ✅ ADD
                new Claim(JwtRegisteredClaimNames.Email, user.c_Email),
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