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
        private readonly string _templateBasePath; // ✅ ADDED

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

            // ✅ ADDED (template path setup)
            // _templateBasePath = Path.Combine(AppContext.BaseDirectory, "Templates");
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

        // ─────────────────────────────────────────────────────────────
        // ✅ NEW METHOD (Artwork Moderation Email)
        // ─────────────────────────────────────────────────────────────
        public async Task SendArtworkModerationEmailAsync(
            string toEmail,
            string artistName,
            string artworkTitle,
            string categoryName,
            bool isApproved,
            string adminNote,
            string dashboardUrl = "https://localhost:5086/Artist/Gallery")
        {
            string templatePath = Path.Combine(_templateBasePath, "ArtworkModerationEmail.html");

            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"Template not found: {templatePath}");

            string body = await File.ReadAllTextAsync(templatePath);

            string logoPath = Path.Combine(_templateBasePath, "logo.png");

            string subject, statusLabel, badgeClass, introMessage, outroMessage,
                   noteLabel, noteColor, noteBg;

            if (isApproved)
            {
                subject = $"🎉 Your artwork \"{artworkTitle}\" has been approved!";
                statusLabel = "✅ Approved";
                badgeClass = "badge-approved";
                introMessage = "Your artwork is now live on Artify!";
                outroMessage = "Keep creating amazing work!";
                noteLabel = "Admin Note";
                noteColor = "#059669";
                noteBg = "#ecfdf5";

                if (string.IsNullOrWhiteSpace(adminNote))
                    adminNote = "No additional comments.";
            }
            else
            {
                subject = $"Your artwork \"{artworkTitle}\" was not approved";
                statusLabel = "❌ Rejected";
                badgeClass = "badge-rejected";
                introMessage = "Your artwork could not be approved.";
                outroMessage = "Please review and resubmit.";
                noteLabel = "Reason for Rejection";
                noteColor = "#dc2626";
                noteBg = "#fef2f2";
            }

            body = body
                .Replace("{{ArtistName}}", artistName)
                .Replace("{{ArtworkTitle}}", artworkTitle)
                .Replace("{{CategoryName}}", categoryName)
                .Replace("{{ReviewedOn}}", DateTime.Now.ToString("dd MMM yyyy, hh:mm tt"))
                .Replace("{{StatusLabel}}", statusLabel)
                .Replace("{{BadgeClass}}", badgeClass)
                .Replace("{{IntroMessage}}", introMessage)
                .Replace("{{OutroMessage}}", outroMessage)
                .Replace("{{NoteLabel}}", noteLabel)
                .Replace("{{AdminNote}}", adminNote)
                .Replace("{{NoteColor}}", noteColor)
                .Replace("{{NoteBg}}", noteBg)
                .Replace("{{DashboardUrl}}", dashboardUrl)
                .Replace("{{Year}}", DateTime.Now.Year.ToString());

            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_emailSettings.Email));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            var builder = new BodyBuilder();

            if (File.Exists(logoPath))
            {
                var image = builder.LinkedResources.Add(logoPath);
                image.ContentId = MimeUtils.GenerateMessageId();
                body = body.Replace("{{Logo}}", $"cid:{image.ContentId}");
            }
            else
            {
                body = body.Replace("{{Logo}}", "");
            }

            builder.HtmlBody = body;
            email.Body = builder.ToMessageBody();

            // ✅ USING SAME SMTP LOGIC (NO NEW METHOD ADDED)
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
