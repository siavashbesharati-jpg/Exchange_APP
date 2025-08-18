namespace ForexExchange.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string message);
        Task SendEmailAsync(string to, string subject, string htmlMessage, string? plainTextMessage = null);
    }
    
    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _configuration;
        
        public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }
        
        public async Task SendEmailAsync(string to, string subject, string message)
        {
            await SendEmailAsync(to, subject, message, message);
        }
        
        public async Task SendEmailAsync(string to, string subject, string htmlMessage, string? plainTextMessage = null)
        {
            try
            {
                // For development/demo purposes, just log the email
                // In production, implement with actual email service (SendGrid, SMTP, etc.)
                
                _logger.LogInformation($"EMAIL SENT:");
                _logger.LogInformation($"To: {to}");
                _logger.LogInformation($"Subject: {subject}");
                _logger.LogInformation($"Message: {htmlMessage}");
                _logger.LogInformation($"--- END EMAIL ---");
                
                // Simulate async operation
                await Task.Delay(100);
                
                // TODO: Implement actual email sending
                // Example with SMTP:
                /*
                var smtpClient = new SmtpClient(_configuration["Email:Host"])
                {
                    Port = int.Parse(_configuration["Email:Port"]),
                    Credentials = new NetworkCredential(_configuration["Email:Username"], _configuration["Email:Password"]),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_configuration["Email:From"]),
                    Subject = subject,
                    Body = htmlMessage,
                    IsBodyHtml = true,
                };
                mailMessage.To.Add(to);

                await smtpClient.SendMailAsync(mailMessage);
                */
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To} with subject {Subject}", to, subject);
                throw;
            }
        }
    }
}
