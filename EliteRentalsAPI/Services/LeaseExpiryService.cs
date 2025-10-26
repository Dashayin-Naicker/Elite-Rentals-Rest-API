using EliteRentalsAPI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EliteRentalsAPI.Services
{
    public class LeaseExpiryService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<LeaseExpiryService> _logger;
        private readonly FcmService _fcm;

        public LeaseExpiryService(IServiceProvider services, ILogger<LeaseExpiryService> logger, FcmService fcm)
        {
            _services = services;
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
                    using (var scope = _services.CreateScope())
                    {
                        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                        // Ensure you have a Leases table with TenantId, LeaseEndDate, etc.
                        var leases = await ctx.Leases
                            .Include(l => l.Tenant)
                            .Where(l => l.Tenant != null && l.Tenant.FcmToken != null)
                            .ToListAsync(stoppingToken);

                        var today = DateTime.UtcNow.Date;

                        foreach (var lease in leases)
                        {
                            var daysRemaining = (lease.EndDate.Date - today).TotalDays;

                            // 🔹 14 days before expiry
                            if (daysRemaining == 14)
                            {
                                await SendReminder(lease.Tenant, lease, "2 weeks remaining before your lease expires. Please contact management to renew.");
                            }
                            // 🔹 7 days before expiry
                            else if (daysRemaining == 7)
                            {
                                await SendReminder(lease.Tenant, lease, "1 week remaining before your lease expires. Renewal required soon.");
                            }
                            // 🔹 On expiry date
                            else if (daysRemaining == 0)
                            {
                                await SendReminder(lease.Tenant, lease, "Your lease has expired today. Please contact the office for renewal or move-out instructions.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in LeaseExpiryService");
                }

                // Check once daily
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }

        private async Task SendReminder(Models.User tenant, Models.Lease lease, string message)
        {
            var payload = new Dictionary<string, string>
            {
                { "type", "lease_expiry" },
                { "tenantId", tenant.UserId.ToString() },
                { "leaseId", lease.LeaseId.ToString() },
                { "expiryDate", lease.EndDate.ToString("yyyy-MM-dd") }
            };

            await _fcm.SendAsync(
                tenant.FcmToken,
                "📅 Lease Expiry Reminder",
                $"Hi {tenant.FirstName}, {message}",
                payload
            );
        }
    }
}
