namespace RecordsMaster.Services
{
    // This calls the SMTPEmailSender class
    public interface IEmailSender
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlMessage);
    }
}
