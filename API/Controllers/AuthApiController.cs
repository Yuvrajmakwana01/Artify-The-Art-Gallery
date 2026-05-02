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
        private readonly EmailService _emailService;
        private readonly RedisService _redis;
        private readonly RabbitService _rabbit;
        private readonly ILogger<AuthApiController> _logger;

        public AuthApiController(
            IAuthInterface auth,
            IConfiguration config,
            EmailService emailService,
            RedisService redis,
            RabbitService rabbit,
            ILogger<AuthApiController> logger)
        {
            _auth = auth;
            _config = config;
            _emailService = emailService;
            _redis = redis;
            _rabbit = rabbit;
            _logger = logger;
        }

        [HttpPost("UserGoogleLogin")]
        public async Task<IActionResult> UserGoogleLogin([FromBody] vm_GoogleLogin model)
        {
            if (model == null || string.IsNullOrEmpty(model.c_Email))
                return BadRequest("Invalid Google data");
            
            var existingUser = await _auth.GetUserByEmail(model.c_Email);

            if (existingUser != null)
            {
                var token = GenerateJwtToken(existingUser);
                return Ok(new
                {
                    token,
                    message = "Welcome back!",
                    user = new
                    {
                        existingUser.c_UserId,
                        existingUser.c_FullName,
                        existingUser.c_UserName,
                        existingUser.c_Email,
                        existingUser.c_ProfileImage
                    }
                });
            }

            var newUser = new t_UserRegister
            {
                c_FullName = model.c_FullName,
                c_Email = model.c_Email,
                c_UserName = model.c_Email.Split('@')[0],
                c_PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.c_GoogleId),
                c_Gender = "Other",
            };

            int result = await _auth.UserRegister(newUser);

            if (result > 0)
            {
                var registeredUser = await _auth.GetUserByEmail(model.c_Email);
                if (registeredUser == null)
                    return StatusCode(500, "Registered user could not be loaded");

                try
                {
                    await _rabbit.PublishRegisterNotificationAsync(
                        registeredUser.c_UserId,
                        registeredUser.c_UserName,
                        "User");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish user register notification for {Email}.", model.c_Email);
                }

                // Send welcome email for Google users too
                try
                {
                    string loginUrl = _config["AppBaseUrl"] + "/Auth/UserLogin";
                    string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "MVC", "wwwroot", "images", "Artify-Logos.png");

                    var placeholders = new Dictionary<string, string>
                    {
                        { "UserName", registeredUser.c_FullName ?? registeredUser.c_UserName },
                        { "LoginUrl", loginUrl }
                    };

                    await _emailService.SendEmailAsync(
                        toEmail: registeredUser.c_Email,
                        subject: "Welcome to Artify Gallery!",
                        templateFile: "UserRegisterEmail.html",
                        placeholders: placeholders,
                        logoPath: logoPath
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send welcome email to Google user {Email}", model.c_Email);
                }

                var token = GenerateJwtToken(registeredUser);
                return Ok(new
                {
                    token,
                    message = "Registration & Login Successful",
                    user = new
                    {
                        registeredUser.c_UserId,
                        registeredUser.c_FullName,
                        registeredUser.c_UserName,
                        registeredUser.c_Email,
                        registeredUser.c_ProfileImage
                    }
                });
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
                // Send welcome email using the correct template
                try
                {
                    string loginUrl = _config["AppBaseUrl"] + "/Auth/UserLogin";
                    string logoPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "MVC", "wwwroot", "images", "Artify-Logos.png"));

                    var placeholders = new Dictionary<string, string>
                    {
                        { "UserName", model.c_FullName ?? model.c_UserName },
                        { "LoginUrl", loginUrl }
                    };

                    await _emailService.SendEmailAsync(
                        toEmail: model.c_Email,
                        subject: "Welcome to Artify Gallery!",
                        templateFile: "UserRegisterEmail.html",
                        placeholders: placeholders,
                        logoPath: logoPath
                    );

                    _logger.LogInformation("Welcome email sent successfully to {Email}", model.c_Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Welcome email failed for {Email}", model.c_Email);
                    // Don't fail registration if email fails
                }

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
            await _redis.SetOtpAsync(model.c_Email.ToLower().Trim(), otp);

            try
            {
                string logoPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "MVC", "wwwroot", "images", "Artify-Logos.png"));

                var placeholders = new Dictionary<string, string>
                {
                    { "UserName", user.c_FullName ?? user.c_UserName },
                    { "OTP", otp }
                };

                await _emailService.SendEmailAsync(
                    toEmail: user.c_Email,
                    subject: "Artify - Password Reset OTP",
                    templateFile: "UserOtpTemplate.html",
                    placeholders: placeholders,
                    logoPath: logoPath
                );

                _logger.LogInformation("OTP email sent to {Email}", user.c_Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OTP email failed for {Email}", user.c_Email);
                return StatusCode(500, "Failed to send OTP email. Please try again.");
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
                return BadRequest("OTP expired or not found. Please request a new OTP.");

            if (savedOtp != model.c_Otp)
                return BadRequest("Invalid OTP. Please try again.");

            await _redis.SetOtpVerifiedAsync(email);

            return Ok(new { message = "OTP verified successfully" });
        }

        [HttpPost("UserResetPassword")]
        public async Task<IActionResult> UserResetPassword([FromBody] t_ResetPassword model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var email = model.c_Email.ToLower().Trim();

            var isVerified = await _redis.IsOtpVerifiedAsync(email);

            if (!isVerified)
                return BadRequest("OTP not verified. Please verify OTP first.");

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.c_NewPassword);

            var result = await _auth.UpdatePassword(model.c_Email, hashedPassword);

            if (result > 0)
            {
                // Send password change confirmation email
                try
                {
                    string loginUrl = _config["AppBaseUrl"] + "/Auth/UserLogin";
                    string logoPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "MVC", "wwwroot", "images", "Artify-Logos.png"));

                    var placeholders = new Dictionary<string, string>
                    {
                        { "UserName", email },
                        { "LoginUrl", loginUrl }
                    };

                    await _emailService.SendEmailAsync(
                        toEmail: email,
                        subject: "Security Alert: Password Changed Successfully",
                        templateFile: "UserPasswordChange.html",
                        placeholders: placeholders,
                        logoPath: logoPath
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send password change confirmation to {Email}", email);
                }

                await _redis.DeleteOtpAsync(email);
                await _redis.DeleteVerifiedFlagAsync(email);

                return Ok(new { message = "Password updated successfully" });
            }
            
            return StatusCode(500, "Password reset failed");
        }

        // ================= JWT TOKEN =================
        private string GenerateJwtToken(t_UserRegister user)
        {
            var claims = new[]
            {
                new Claim("user_id", user.c_UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.c_Email),
                new Claim(ClaimTypes.NameIdentifier, user.c_UserId.ToString()),
                new Claim(ClaimTypes.Name, user.c_UserName),
                new Claim(ClaimTypes.Email, user.c_Email),
                new Claim(ClaimTypes.Role, "User")
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
