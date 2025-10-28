using EliteRentalsAPI.Data;
using EliteRentalsAPI.Helpers;
using EliteRentalsAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EliteRentalsAPI.Services
{
    public class LeaseExpiryService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LeaseExpiryService> _logger;
        private readonly FcmService _fcm;

        public LeaseExpiryService(IServiceScopeFactory scopeFactory, ILogger<LeaseExpiryService> logger, FcmService fcm)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _fcm = fcm;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🏡 LeaseExpiryService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var email = scope.ServiceProvider.GetRequiredService<EmailService>();

                    var leases = await ctx.Leases
                        .Include(l => l.Tenant)
                        .Where(l => l.Tenant != null && l.Tenant.FcmToken != null)
                        .ToListAsync(stoppingToken);

                    var today = DateTime.UtcNow.Date;

                    foreach (var lease in leases)
                    {
                        var daysRemaining = (lease.EndDate.Date - today).TotalDays;

                        if (daysRemaining == 14)
                            await SendReminder(lease.Tenant, lease, lease.EndDate, email, "2 weeks remaining before your lease expires. Please contact management to renew.");
                        else if (daysRemaining == 7)
                            await SendReminder(lease.Tenant, lease, lease.EndDate, email, "1 week remaining before your lease expires. Renewal required soon.");
                        else if (daysRemaining == 0)
                            await SendReminder(lease.Tenant, lease, lease.EndDate, email, "Your lease has expired today. Please contact the office for renewal or move-out instructions.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in LeaseExpiryService");
                }

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }

        private async Task SendReminder(Models.User tenant, Models.Lease lease, DateTime leaseEnd, EmailService email, string message)
        {
            var payload = new Dictionary<string, string>
        {
            { "type", "lease_expiry" },
            { "tenantId", tenant.UserId.ToString() },
            { "leaseId", lease.LeaseId.ToString() },
            { "expiryDate", leaseEnd.ToString("yyyy-MM-dd") }
        };

            await _fcm.SendAsync(tenant.FcmToken, "📅 Lease Expiry Reminder", $"Hi {tenant.FirstName}, {message}", payload);

            string subject = "Lease Expiry Reminder";
            string messageBody = $@"
<p>Dear {tenant.FirstName},</p>
<p>{message}</p>
<p><b>Lease End Date:</b> {leaseEnd:yyyy-MM-dd}</p>
<p>Please contact management if you wish to renew.</p>
<a class='button' href='#'>Contact Management</a>
";
            email.SendEmail(tenant.Email, subject, EmailTemplateHelper.WrapEmail(subject, messageBody));
        }
    }
}

