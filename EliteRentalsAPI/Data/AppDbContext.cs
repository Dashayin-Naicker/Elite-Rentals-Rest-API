using EliteRentalsAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EliteRentalsAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Property> Properties { get; set; }
        public DbSet<RentalApplication> Applications { get; set; }
        public DbSet<Lease> Leases { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Maintenance> Maintenance { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Report> Reports { get; set; }

        public DbSet<PropertyImage> PropertyImages { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Explicit PKs
            modelBuilder.Entity<User>().HasKey(u => u.UserId);
            modelBuilder.Entity<Property>().HasKey(p => p.PropertyId);
            modelBuilder.Entity<RentalApplication>().HasKey(a => a.ApplicationId);
            modelBuilder.Entity<Lease>().HasKey(l => l.LeaseId);
            modelBuilder.Entity<Invoice>().HasKey(i => i.InvoiceId);
            modelBuilder.Entity<Payment>().HasKey(p => p.PaymentId);
            modelBuilder.Entity<Maintenance>().HasKey(m => m.MaintenanceId);
            modelBuilder.Entity<Notification>().HasKey(n => n.NotificationId);
            modelBuilder.Entity<Message>().HasKey(m => m.MessageId);
            modelBuilder.Entity<Report>().HasKey(r => r.ReportId);

            // Relationships
            modelBuilder.Entity<Property>()
                .HasOne(p => p.Manager)
                .WithMany()
                .HasForeignKey(p => p.ManagerId);

            modelBuilder.Entity<RentalApplication>()
                .HasOne(a => a.Property)
                .WithMany()
                .HasForeignKey(a => a.PropertyId);

            modelBuilder.Entity<Lease>()
                .HasOne(l => l.Property)
                .WithMany(p => p.Leases)
                .HasForeignKey(l => l.PropertyId);

            modelBuilder.Entity<Lease>()
                .HasOne(l => l.Tenant)
                .WithMany()
                .HasForeignKey(l => l.TenantId);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Tenant)
                .WithMany()
                .HasForeignKey(i => i.TenantId);
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Lease)
                .WithMany()
                .HasForeignKey(i => i.LeaseId);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Tenant)
                .WithMany()
                .HasForeignKey(p => p.TenantId);

            modelBuilder.Entity<Maintenance>()
                .HasOne(m => m.Tenant)
                .WithMany()
                .HasForeignKey(m => m.TenantId);
            modelBuilder.Entity<Maintenance>()
                .HasOne(m => m.Property)
                .WithMany()
                .HasForeignKey(m => m.PropertyId);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Message>()
                .HasOne(m => m.Receiver)
                .WithMany()
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Report>()
                .HasOne(r => r.Manager)
                .WithMany()
                .HasForeignKey(r => r.ManagerId);

            modelBuilder.Entity<PropertyImage>()
    .HasOne(pi => pi.Property)
    .WithMany(p => p.Images)
    .HasForeignKey(pi => pi.PropertyId)
    .OnDelete(DeleteBehavior.Cascade);

        }
    }
}
