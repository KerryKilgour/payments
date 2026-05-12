using Microsoft.EntityFrameworkCore;
using payments.Models;

namespace payments.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<PaymentRequest> PaymentRequests => Set<PaymentRequest>();
    public DbSet<PaymentQueueItem> PaymentQueueItems => Set<PaymentQueueItem>();
    public DbSet<PaymentDeadLetter> PaymentDeadLetters => Set<PaymentDeadLetter>();

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
    }
}
