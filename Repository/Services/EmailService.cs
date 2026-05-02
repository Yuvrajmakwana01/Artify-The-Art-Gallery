using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;
using MimeKit.Utils;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Repository.Services
{
    public class EmailService
    {
        private readonly string _fromEmail;
        private readonly string _password;
        private readonly string _host;
        private readonly int _port;
        private readonly string _templateBasePath;

        public EmailService(IConfiguration config)
        {
            var s = config.GetSection("EmailSettings");
            _fromEmail = s["Email"]!;
            _password = s["Password"]!;
            _host = s["Host"]!;
            _port = int.Parse(s["Port"]!);

            // Get the correct template path - looking for Templates folder in API project
            var apiPath = AppDomain.CurrentDomain.BaseDirectory;
            var apiRoot = Directory.GetParent(apiPath)?.Parent?.Parent?.Parent?.FullName;
            _templateBasePath = Path.Combine(apiRoot ?? Directory.GetCurrentDirectory(), "Templates");
            
            // Fallback to current directory if not found
            if (!Directory.Exists(_templateBasePath))
            {
                _templateBasePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates");
            }
        }

        /// <summary>
        /// Single email sender for the entire project.
        /// </summary>
        public async Task SendEmailAsync(
            string toEmail,
            string subject,
            string? htmlBody = null,
            string? templateFile = null,
            Dictionary<string, string>? placeholders = null,
            string? logoPath = null,
            byte[]? attachment = null,
            string? attachmentFileName = null)
        {
            // ── 1. Resolve HTML body ─────────────────────────────────────
            if (templateFile is not null)
            {
                string fullPath = Path.Combine(_templateBasePath, templateFile);
                if (!File.Exists(fullPath))
                    throw new FileNotFoundException($"Template not found: {fullPath}");
                htmlBody = await File.ReadAllTextAsync(fullPath);
            }

            if (string.IsNullOrWhiteSpace(htmlBody))
                throw new ArgumentException("Provide either htmlBody or a valid templateFile.");

            // ── 2. Replace {{Placeholder}} tokens ────────────────────────
            if (placeholders is not null)
            {
                foreach (var (key, value) in placeholders)
                {
                    // Replace both {{key}} and {key} patterns
                    htmlBody = htmlBody.Replace($"{{{{{key}}}}}", value ?? string.Empty);
                    htmlBody = htmlBody.Replace($"{{{key}}}", value ?? string.Empty);
                }
            }

            // ── 3. Build MIME message ────────────────────────────────────
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("Artify Gallery", _fromEmail));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            var builder = new BodyBuilder();

            // ── 4. Handle logo embedding ─────────────────────────────────
            string templateLogo = Path.Combine(_templateBasePath, "logo.png");
            string resolvedLogo = File.Exists(templateLogo) ? templateLogo : logoPath ?? templateLogo;
            if (File.Exists(resolvedLogo))
            {
                var image = builder.LinkedResources.Add(resolvedLogo);
                image.ContentId = MimeUtils.GenerateMessageId();
                // Replace all possible logo placeholder patterns
                htmlBody = htmlBody.Replace("{{Logo}}", $"cid:{image.ContentId}");
                htmlBody = htmlBody.Replace("{Logo}", $"cid:{image.ContentId}");
                htmlBody = htmlBody.Replace("cid:logo", $"cid:{image.ContentId}");
            }
            else
            {
                // Remove logo placeholders if no logo file exists
                htmlBody = htmlBody.Replace("{{Logo}}", string.Empty);
                htmlBody = htmlBody.Replace("{Logo}", string.Empty);
                // If template uses cid:logo but no logo file, just show alt text
                htmlBody = htmlBody.Replace("cid:logo", "");
            }

            // IMPORTANT: Set the HTML body with proper content type
            builder.HtmlBody = htmlBody;

            // ── 5. Optional file attachment ──────────────────────────────
            if (attachment is not null && attachmentFileName is not null)
                builder.Attachments.Add(attachmentFileName, attachment);

            email.Body = builder.ToMessageBody();

            // ── 6. Send via SMTP ─────────────────────────────────────────
            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_host, _port, MailKit.Security.SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_fromEmail, _password);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }
    }
}
