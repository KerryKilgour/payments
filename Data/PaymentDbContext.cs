using Microsoft.EntityFrameworkCore;
using payments.Models;

namespace payments.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<PaymentRequest> PaymentRequests => Set<PaymentRequest>();
    public DbSet<PaymentQueueItem> PaymentQueueItems => Set<PaymentQueueItem>();
    public DbSet<PaymentDeadLetter> PaymentDeadLetters => Set<PaymentDeadLetter>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<SchedulePayment> SchedulePayments => Set<SchedulePayment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentRequest>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.ExternalId).IsUnique();
            entity.Property(p => p.Status).HasConversion<string>();
        });

        modelBuilder.Entity<PaymentQueueItem>(entity =>
        {
            entity.HasKey(q => q.Id);
            entity.HasIndex(q => q.RequestId);
        });

        modelBuilder.Entity<PaymentDeadLetter>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.HasIndex(d => d.RequestId);
        });

        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Frequency).HasConversion<string>();
            entity.Property(s => s.Status).HasConversion<string>();
            entity.HasMany(s => s.SchedulePayments)
                .WithOne(sp => sp.Schedule)
                .HasForeignKey(sp => sp.ScheduleId);
        });

        modelBuilder.Entity<SchedulePayment>(entity =>
        {
            entity.HasKey(sp => sp.Id);
            entity.HasIndex(sp => sp.ScheduleId);
            entity.HasIndex(sp => sp.PaymentRequestId);
            entity.Property(sp => sp.Status).HasConversion<string>();
            entity.HasOne(sp => sp.Schedule)
                .WithMany(s => s.SchedulePayments)
                .HasForeignKey(sp => sp.ScheduleId);
            entity.HasOne(sp => sp.PaymentRequest)
                .WithMany()
                .HasForeignKey(sp => sp.PaymentRequestId);
        });
    }
}
