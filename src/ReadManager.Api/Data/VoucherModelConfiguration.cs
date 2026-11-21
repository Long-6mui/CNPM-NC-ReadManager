using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Data;

internal static class VoucherModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VoucherOffer>(e =>
        {
            e.ToTable("VoucherOffers", t =>
            {
                t.HasCheckConstraint("CK_VoucherOffers_CoinCost", "`CoinCost` > 0");
                t.HasCheckConstraint("CK_VoucherOffers_DiscountType", "`DiscountType` IN ('Percent', 'Amount')");
                t.HasCheckConstraint("CK_VoucherOffers_Value", "`Value` > 0");
                t.HasCheckConstraint("CK_VoucherOffers_ValidDays", "`ValidDays` > 0");
            });

            e.HasKey(x => x.OfferId);
            e.Property(x => x.OfferId).ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.DiscountType).HasMaxLength(20).IsRequired();
            e.Property(x => x.Value).HasPrecision(12, 2);
            e.Property(x => x.MaxDiscount).HasPrecision(12, 2);
            e.Property(x => x.MinOrderAmount).HasPrecision(12, 2);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnType("datetime(6)");
            e.HasIndex(x => new { x.IsActive, x.SortOrder });

            var seedTime = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);
            e.HasData(
                new VoucherOffer
                {
                    OfferId = 1,
                    Name = "Giảm 10% đơn truyện",
                    Description = "Áp dụng cho đơn mua trọn bộ truyện từ 20.000₫",
                    CoinCost = 30,
                    DiscountType = VoucherDiscountTypes.Percent,
                    Value = 10m,
                    MaxDiscount = 15000m,
                    MinOrderAmount = 20000m,
                    ValidDays = 30,
                    IsActive = true,
                    SortOrder = 1,
                    CreatedAt = seedTime
                },
                new VoucherOffer
                {
                    OfferId = 2,
                    Name = "Giảm 20% đơn truyện",
                    Description = "Giảm tối đa 30.000₫ cho đơn mua truyện từ 30.000₫",
                    CoinCost = 60,
                    DiscountType = VoucherDiscountTypes.Percent,
                    Value = 20m,
                    MaxDiscount = 30000m,
                    MinOrderAmount = 30000m,
                    ValidDays = 30,
                    IsActive = true,
                    SortOrder = 2,
                    CreatedAt = seedTime
                },
                new VoucherOffer
                {
                    OfferId = 3,
                    Name = "Giảm 10.000₫ mua truyện",
                    Description = "Áp dụng cho đơn mua trọn bộ truyện từ 25.000₫",
                    CoinCost = 40,
                    DiscountType = VoucherDiscountTypes.Amount,
                    Value = 10000m,
                    MaxDiscount = null,
                    MinOrderAmount = 25000m,
                    ValidDays = 30,
                    IsActive = true,
                    SortOrder = 3,
                    CreatedAt = seedTime
                },
                new VoucherOffer
                {
                    OfferId = 4,
                    Name = "Giảm 25.000₫ truyện VIP",
                    Description = "Áp dụng cho đơn mua trọn bộ truyện từ 45.000₫",
                    CoinCost = 90,
                    DiscountType = VoucherDiscountTypes.Amount,
                    Value = 25000m,
                    MaxDiscount = null,
                    MinOrderAmount = 45000m,
                    ValidDays = 30,
                    IsActive = true,
                    SortOrder = 4,
                    CreatedAt = seedTime
                }
            );
        });

        modelBuilder.Entity<Voucher>(e =>
        {
            e.ToTable("Vouchers", t =>
            {
                t.HasCheckConstraint("CK_Vouchers_DiscountType", "`DiscountType` IN ('Percent', 'Amount')");
                t.HasCheckConstraint("CK_Vouchers_Status", "`Status` IN ('Available', 'Reserved', 'Used', 'Expired')");
                t.HasCheckConstraint("CK_Vouchers_Value", "`Value` > 0");
            });

            e.HasKey(x => x.VoucherId);
            e.Property(x => x.VoucherId).ValueGeneratedOnAdd();
            e.Property(x => x.RequestId).HasColumnType("char(36)").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.DiscountType).HasMaxLength(20).IsRequired();
            e.Property(x => x.Value).HasPrecision(12, 2);
            e.Property(x => x.MaxDiscount).HasPrecision(12, 2);
            e.Property(x => x.MinOrderAmount).HasPrecision(12, 2);
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(VoucherStatuses.Available).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnType("datetime(6)");
            e.Property(x => x.ExpiresAt).HasColumnType("datetime(6)");
            e.Property(x => x.UsedAt).HasColumnType("datetime(6)");

            e.HasIndex(x => new { x.UserId, x.RequestId }).IsUnique();
            e.HasIndex(x => new { x.UserId, x.Status, x.ExpiresAt });

            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Offer).WithMany().HasForeignKey(x => x.OfferId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
