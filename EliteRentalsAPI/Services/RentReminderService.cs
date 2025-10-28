using EliteRentalsAPI.Data;
using EliteRentalsAPI.Helpers;
using EliteRentalsAPI.Services;
using Microsoft.EntityFrameworkCore;

namespace EliteRentalsAPI.Services
{
    public class RentReminderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RentReminderService> _logger;
        private readonly FcmService _fcm;

        public RentReminderService(IServiceScopeFactory scopeFactory, FcmService fcm, ILogger<RentReminderService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _fcm = fcm;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🏠 Rent Reminder Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var lastDay = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));
                    var reminderDay = lastDay.AddDays(-3);

                    if (now.Date == reminderDay.Date)
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var email = scope.ServiceProvider.GetRequiredService<EmailService>();

                        var tenants = await ctx.Users
                            .Where(u => u.Role == "Tenant" && !string.IsNullOrEmpty(u.FcmToken))
                            .ToListAsync(stoppingToken);

                        foreach (var tenant in tenants)
                        {
                            try
                            {
                                var dataPayload = new Dictionary<string, string>
                            {
                                { "type", "rent_due" },
                                { "tenantId", tenant.UserId.ToString() },
                                { "dueDate", lastDay.ToString("yyyy-MM-dd") }
                            };

                                await _fcm.SendAsync(
                                    tenant.FcmToken,
                                    "💰 Rent Due Reminder",
                                    $"Hi {tenant.FirstName}, your rent is due on {lastDay:dd MMM}.",
                                    dataPayload
                                );

                                if (!string.IsNullOrEmpty(tenant.Email))
                                {
                                    string subject = "Rent Payment Reminder";
                                    string messageBody = $@"
<p>Dear {tenant.FirstName},</p>
<p>This is a friendly reminder that your rent is due on <b>{lastDay:dd MMM yyyy}</b>.</p>
<p>Please ensure your payment is made before the due date to avoid penalties.</p>
<a class='button' href='#'>Pay Rent Now</a>
<p>Thank you,<br>Elite Rentals</p>
";
                                    string htmlBody = EmailTemplateHelper.WrapEmail(subject, messageBody);
                                    email.SendEmail(tenant.Email, subject, htmlBody);
                                    _logger.LogInformation("📧 Rent reminder email sent to {Email}", tenant.Email);
                                }

                                _logger.LogInformation("✅ Sent rent reminder to {Tenant}", tenant.Email);
                            }
                            catch (Exception sendEx)
                            {
                                _logger.LogWarning(sendEx, "⚠️ Failed to send reminder to {Tenant}", tenant.Email);
                            }
                        }
                    }

                    await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in RentReminderService loop");
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }

            _logger.LogInformation("🏠 Rent Reminder Service stopped.");
        }
    }

}
