using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;

namespace RecordsMaster.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _config;

        public SmtpEmailSender(IConfiguration config)
        {
            _config = config;
        }
        // Depending on your SMTP configuration, you may need to change a few things. 
        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("App", _config["Smtp:From"] ?? "from@example.com"));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlMessage };
            //message.Body = new TextPart("html") { Text = htmlMessage + "<p>this inbox cannot receive messages.</p>" };

            using var client = new SmtpClient();
            await client.ConnectAsync(_config["Smtp:Host"] ?? "Host name. here", int.Parse(_config["Smtp:Port"] ?? "587"), true);
            await client.AuthenticateAsync(_config["Smtp:Username"] ?? "Username", _config["Smtp:Password"] ?? "Password"); // not necessary on port 587
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}
