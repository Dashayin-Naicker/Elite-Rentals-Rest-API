using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using EliteRentalsAPI.Helpers;

namespace EliteRentalsAPI.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void SendEmail(string toEmail, string subject, string body)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");

            var fromAddress = new MailAddress(emailSettings["FromEmail"], emailSettings["FromName"]);
            var smtp = new SmtpClient
            {
                Host = emailSettings["SmtpServer"],
                Port = int.Parse(emailSettings["Port"]),
                EnableSsl = true,
                Credentials = new NetworkCredential(emailSettings["Username"], emailSettings["Password"])
            };

            // Wrap the message in the Elite Rentals HTML template
            var formattedBody = EmailTemplateHelper.WrapEmail(subject, body);

            using var message = new MailMessage(fromAddress, new MailAddress(toEmail))
            {
                Subject = subject,
                Body = formattedBody,
                IsBodyHtml = true
            };

            smtp.Send(message);
        }
    }
}
