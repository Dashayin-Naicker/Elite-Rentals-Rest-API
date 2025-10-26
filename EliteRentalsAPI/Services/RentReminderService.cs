using EliteRentalsAPI.Data;
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
                    var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
                    var lastDay = new DateTime(now.Year, now.Month, daysInMonth);

                    // Rent reminders go out 3 days before month-end
                    var reminderDay = lastDay.AddDays(-3);

                    if (now.Date == reminderDay.Date)
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

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

                                _logger.LogInformation("✅ Sent rent reminder to {Tenant}", tenant.Email);
                            }
                            catch (Exception sendEx)
                            {
                                _logger.LogWarning(sendEx, "⚠️ Failed to send reminder to {Tenant}", tenant.Email);
                            }
                        }
                    }

                    // Wait 24 hours before checking again
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
