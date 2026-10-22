using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Data;

internal static class PaymentModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(e =>
        {
            e.ToTable("Payments", t =>
            {
                t.HasCheckConstraint("CK_Payments_Amount", "`ReceivedAmount` IS NULL OR `ReceivedAmount` >= 0");
                t.HasCheckConstraint("CK_Payments_Status", "`Status` IN ('Pending', 'Succeeded', 'Failed', 'Cancelled')");
                t.HasCheckConstraint("CK_Payments_Success", "`Status` <> 'Succeeded' OR (`VerifiedAt` IS NOT NULL AND `SucceededAt` IS NOT NULL AND `ReceivedAmount` IS NOT NULL AND `ProviderTransactionId` IS NOT NULL)");
            });
            e.HasKey(x => x.PaymentId);
            e.Property(x => x.Provider).HasMaxLength(50).IsRequired();
            e.Property(x => x.MerchantRequestId).HasMaxLength(100).UseCollation("utf8mb4_bin").IsRequired();
            e.Property(x => x.ProviderTransactionId).HasMaxLength(150).UseCollation("utf8mb4_bin");
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.FailureCode).HasMaxLength(100);
            e.Property(x => x.ReceivedAmount).HasPrecision(12, 2);
            e.Property(x => x.CreatedAt).HasColumnType("datetime(6)");
            e.Property(x => x.VerifiedAt).HasColumnType("datetime(6)");
            e.Property(x => x.SucceededAt).HasColumnType("datetime(6)");
            e.HasIndex(x => x.MerchantRequestId).IsUnique();
            e.HasIndex(x => new { x.Provider, x.ProviderTransactionId }).IsUnique();
            e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PaymentEvent>(e =>
        {
            e.ToTable("PaymentEvents", t => t.HasCheckConstraint("CK_PaymentEvents_Status", "`ProcessingStatus` IN ('Pending', 'Processed', 'Failed', 'Ignored')"));
            e.HasKey(x => x.PaymentEventId);
            e.Property(x => x.ProviderEventId).HasMaxLength(150).UseCollation("utf8mb4_bin").IsRequired();
            e.Property(x => x.Payload).HasColumnType("longtext").IsRequired();
            e.Property(x => x.ProcessingStatus).HasMaxLength(20).IsRequired();
            e.Property(x => x.ErrorMessage).HasMaxLength(1000);
            e.Property(x => x.ReceivedAt).HasColumnType("datetime(6)");
            e.Property(x => x.ProcessedAt).HasColumnType("datetime(6)");
            e.HasIndex(x => new { x.PaymentId, x.ProviderEventId }).IsUnique();
            e.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<UserSubscription>(e =>
        {
            e.ToTable("UserSubscriptions", t => t.HasCheckConstraint("CK_UserSubscriptions_Period", "`EndsAt` > `StartsAt`"));
            e.HasKey(x => x.SubscriptionId);
            e.Property(x => x.StartsAt).HasColumnType("datetime(6)");
            e.Property(x => x.EndsAt).HasColumnType("datetime(6)");
            e.Property(x => x.RevokedAt).HasColumnType("datetime(6)");
            e.HasIndex(x => x.OrderId).IsUnique();
            e.HasOne(x => x.Order).WithOne().HasForeignKey<UserSubscription>(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<StoryPurchase>(e =>
        {
            e.ToTable("StoryPurchases", t => t.HasCheckConstraint("CK_StoryPurchases_Period", "`ExpiresAt` IS NULL OR `ExpiresAt` > `GrantedAt`"));
            e.HasKey(x => x.PurchaseId);
            e.Property(x => x.GrantedAt).HasColumnType("datetime(6)");
            e.Property(x => x.ExpiresAt).HasColumnType("datetime(6)");
            e.Property(x => x.RevokedAt).HasColumnType("datetime(6)");
            e.HasIndex(x => x.OrderId).IsUnique();
            e.HasOne(x => x.Order).WithOne().HasForeignKey<StoryPurchase>(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
