using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentSystem.Core.Entities;

namespace PaymentSystem.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- User ------------------------------------------------------------
        // Email is the natural unique key for lookups; index it to keep login fast
        // at scale. StripeCustomerId is nullable — populated only after first checkout.
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.FullName).IsRequired().HasMaxLength(100);
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.StripeCustomerId).HasMaxLength(100);
        });

        // --- Subscription ----------------------------------------------------
        // decimal(18,2) matches Stripe's smallest-unit model when converted back
        // to display currency. Cascade delete keeps orphaned subscription rows from
        // accumulating if a user account is ever hard-deleted.
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Status).IsRequired().HasMaxLength(50);
            entity.Property(s => s.Currency).IsRequired().HasMaxLength(3);
            entity.Property(s => s.Price).HasColumnType("decimal(18,2)");
            entity.Property(s => s.StripeSubscriptionId).HasMaxLength(100);

            entity.HasOne(s => s.User)
                  .WithMany(u => u.Subscriptions)
                  .HasForeignKey(s => s.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
