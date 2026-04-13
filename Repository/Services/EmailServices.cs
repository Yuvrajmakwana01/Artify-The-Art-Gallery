using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;
using MimeKit.Utils;
using Repository.Models;

namespace Repository.Services
{
    public class EmailServices
    {
        private readonly t_EmailSettings _emailSettings;

        public EmailServices(IConfiguration config)
        {
            var section = config.GetSection("EmailSettings");

            _emailSettings = new t_EmailSettings
            {
                Email = section["Email"],
                Password = section["Password"],
                Host = section["Host"],
                Port = int.Parse(section["Port"])
            };
        }

        public async Task SendEmailWithLogo(string toEmail, string subject, string body, string logoPath)
        {
            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_emailSettings.Email));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            var builder = new BodyBuilder();

            // Attach image and create CID
            var image = builder.LinkedResources.Add(logoPath);
            image.ContentId = MimeUtils.GenerateMessageId();

            // Replace placeholder with CID
            body = body.Replace("{{Logo}}", $"cid:{image.ContentId}");

            builder.HtmlBody = body;
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(
                _emailSettings.Host,
                _emailSettings.Port,
                MailKit.Security.SecureSocketOptions.StartTls
            );
            await smtp.AuthenticateAsync(_emailSettings.Email, _emailSettings.Password);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }
    }
}
