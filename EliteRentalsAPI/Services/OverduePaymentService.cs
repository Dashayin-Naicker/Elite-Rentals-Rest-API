using EliteRentalsAPI.Data;
using EliteRentalsAPI.Helpers;
using EliteRentalsAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EliteRentalsAPI.Services
{
    public class OverduePaymentService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OverduePaymentService> _logger;
        private readonly FcmService _fcm;

        public OverduePaymentService(IServiceScopeFactory scopeFactory, ILogger<OverduePaymentService> logger, FcmService fcm)
        {
            _scopeFactory = scopeFactory;
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
                    using var scope = _scopeFactory.CreateScope();
                    var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var email = scope.ServiceProvider.GetRequiredService<EmailService>();

                    var today = DateTime.UtcNow.Date;

                    var overduePayments = await ctx.Payments
                        .Include(p => p.Tenant)
                        .Where(p => p.Status != "Paid" && (today - p.Date).TotalDays > 14)
                        .ToListAsync(stoppingToken);

                    foreach (var payment in overduePayments)
                    {
                        // Notify tenant
                        if (!string.IsNullOrEmpty(payment.Tenant?.FcmToken))
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

                        if (!string.IsNullOrEmpty(payment.Tenant?.Email))
                        {
                            string subject = "Overdue Payment Notice";
                            string messageBody = $@"
<p>Dear {payment.Tenant.FirstName},</p>
<p>Your payment <b>#{payment.PaymentId}</b> is overdue by more than 14 days.</p>
<p>Please make the payment immediately to avoid escalation.</p>
<a class='button' href='#'>Make Payment</a>
<p>Regards,<br>Elite Rentals Billing</p>
";
                            email.SendEmail(payment.Tenant.Email, subject, EmailTemplateHelper.WrapEmail(subject, messageBody));
                        }

                        // Notify managers/admins (similar logic)
                        var managers = await ctx.Users
                            .Where(u => (u.Role == "Admin" || u.Role == "PropertyManager") && !string.IsNullOrEmpty(u.FcmToken))
                            .ToListAsync(stoppingToken);

                        foreach (var mgr in managers)
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

                            if (!string.IsNullOrEmpty(mgr.Email))
                            {
                                string subject = "⚠️ Escalation: Tenant Overdue Payment";
                                string messageBody = $@"
<p>Tenant: {payment.Tenant?.FirstName} ({payment.TenantId})</p>
<p>Payment ID: #{payment.PaymentId}</p>
<p>Status: Overdue by more than 14 days.</p>
<p>Please review and follow up accordingly.</p>
<a class='button' href='#'>View Payment</a>
";
                                email.SendEmail(mgr.Email, subject, EmailTemplateHelper.WrapEmail(subject, messageBody));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in OverduePaymentService");
                }

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }

}
