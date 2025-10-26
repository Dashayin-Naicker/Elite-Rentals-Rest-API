using EliteRentalsAPI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EliteRentalsAPI.Services
{
    public class OverduePaymentService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<OverduePaymentService> _logger;
        private readonly FcmService _fcm;

        public OverduePaymentService(IServiceProvider services, ILogger<OverduePaymentService> logger, FcmService fcm)
        {
            _services = services;
            _logger = logger;
            _fcm = fcm;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("💰 OverduePaymentService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var today = DateTime.UtcNow.Date;

                    // Fetch payments overdue by more than 14 days and not paid
                    var overduePayments = await ctx.Payments
                        .Include(p => p.Tenant)
                        .Where(p => p.Status != "Paid" && (today - p.Date).TotalDays > 14)
                        .ToListAsync(stoppingToken);

                    foreach (var payment in overduePayments)
                    {
                        // Notify tenant
                        if (!string.IsNullOrEmpty(payment.Tenant?.FcmToken))
                        {
                            try
                            {
                                await _fcm.SendAsync(
                                    payment.Tenant.FcmToken,
                                    "💸 Payment Overdue",
                                    $"Hi {payment.Tenant.FirstName}, your payment #{payment.PaymentId} is overdue by more than 14 days.",
                                    new Dictionary<string, string>
                                    {
                                        { "type", "payment_overdue" },
                                        { "paymentId", payment.PaymentId.ToString() },
                                        { "tenantId", payment.TenantId.ToString() }
                                    }
                                );
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to send overdue payment push to tenant {TenantId}", payment.TenantId);
                            }
                        }

                        // Notify managers/admins
                        var managers = await ctx.Users
                            .Where(u => (u.Role == "Admin" || u.Role == "PropertyManager") && !string.IsNullOrEmpty(u.FcmToken))
                            .ToListAsync(stoppingToken);

                        foreach (var mgr in managers)
                        {
                            try
                            {
                                await _fcm.SendAsync(
                                    mgr.FcmToken,
                                    "⚠️ Escalation: Overdue Payment",
                                    $"Tenant {payment.Tenant?.FirstName} ({payment.TenantId}) payment #{payment.PaymentId} is overdue by more than 14 days.",
                                    new Dictionary<string, string>
                                    {
                                        { "type", "payment_escalation" },
                                        { "paymentId", payment.PaymentId.ToString() },
                                        { "tenantId", payment.TenantId.ToString() }
                                    }
                                );
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to send overdue payment escalation to manager {UserId}", mgr.UserId);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in OverduePaymentService");
                }

                // Run once daily
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}
