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
        private readonly t_EmailSetting _emailSettings;

        public EmailServices(IConfiguration config)
        {
            var section = config.GetSection("EmailSettings");

            _emailSettings = new t_EmailSetting
            {
                Email = section["Email"],
                Password = section["Password"],
                Host = section["Host"],
                Port = int.Parse(section["Port"])
            };
        }

        // ✅ COMMON METHOD (Use everywhere)
        public async Task SendEmailAsync(
            string toEmail,
            string subject,
            string body,
            string logoPath = null,
            byte[] attachment = null,
            string fileName = null)
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("Artify Gallery", _emailSettings.Email));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            var builder = new BodyBuilder();

            // ✅ Logo embed
            if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
            {
                var image = builder.LinkedResources.Add(logoPath);
                image.ContentId = "logo";
            }

            builder.HtmlBody = body;

            // ✅ Attachment
            if (attachment != null && !string.IsNullOrEmpty(fileName))
            {
                builder.Attachments.Add(fileName, attachment, new ContentType("application", "pdf"));
            }

            email.Body = builder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                client.CheckCertificateRevocation = false;
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                await client.ConnectAsync(
                    _emailSettings.Host,
                    _emailSettings.Port,
                    MailKit.Security.SecureSocketOptions.StartTls
                );

                await client.AuthenticateAsync(_emailSettings.Email, _emailSettings.Password);
                await client.SendAsync(email);
                await client.DisconnectAsync(true);
            }
        }
    }
}
