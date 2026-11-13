using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Models;
namespace SportBooking.Web.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Court> Courts => Set<Court>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Staff> Staffs => Set<Staff>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentLog> IncidentLogs => Set<IncidentLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Court>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.District).HasMaxLength(50);
            e.Property(x => x.Address).HasMaxLength(200);
            e.Property(x => x.PricePerHour).HasPrecision(18, 0);
            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Booking>(e =>
        {
            e.Property(x => x.TotalPrice).HasPrecision(18, 0);
            e.HasOne(x => x.Court).WithMany(c => c.Bookings).HasForeignKey(x => x.CourtId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.CourtId, x.StartTime, x.EndTime });
        });

        b.Entity<Payment>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 0);
            e.Property(x => x.TransactionCode).HasMaxLength(50);
            e.HasOne(x => x.Booking).WithMany(x => x.Payments).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.TransactionCode);
        });

        b.Entity<Review>(e =>
        {
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.HasOne(x => x.Court).WithMany().HasForeignKey(x => x.CourtId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            // Quan hệ 1-1 với lịch đặt: BookingId có chỉ mục duy nhất nên mỗi lịch chỉ có một đánh giá
            e.HasOne(x => x.Booking).WithOne(x => x.Review).HasForeignKey<Review>(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.CourtId);
            e.ToTable(t => t.HasCheckConstraint("CK_Reviews_Rating", "[Rating] BETWEEN 1 AND 5"));
        });

        b.Entity<Staff>(e =>
        {
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Court).WithMany().HasForeignKey(x => x.CourtId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.UserId, x.CourtId }).IsUnique();   // một nhân viên chỉ được giao một lần cho mỗi sân
        });

        b.Entity<Incident>(e =>
        {
            e.Property(x => x.Description).HasMaxLength(1000).IsRequired();
            e.Property(x => x.ResolutionNote).HasMaxLength(1000);
            e.Property(x => x.OwnerResponse).HasMaxLength(1000);
            e.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Court).WithMany().HasForeignKey(x => x.CourtId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.HandledBy).WithMany().HasForeignKey(x => x.HandledById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.DuplicateOf).WithMany().HasForeignKey(x => x.DuplicateOfId).OnDelete(DeleteBehavior.Restrict);
            // Mỗi lịch đặt tối đa 2 báo cáo: 1 SOS (IsRealTime = true) và 1 phản hồi (IsRealTime = false). Chặn cả khi bấm gửi hai lần cùng lúc
            e.HasIndex(x => new { x.BookingId, x.IsRealTime }).IsUnique();
            e.HasIndex(x => new { x.CourtId, x.Status });
            e.HasIndex(x => new { x.Status, x.SlaDeadline });           // tiến trình nền quét các báo cáo quá hạn
            e.HasIndex(x => x.ReporterId);
            e.HasIndex(x => x.DuplicateOfId);
        });

        b.Entity<IncidentLog>(e =>
        {
            e.Property(x => x.Action).HasMaxLength(60).IsRequired();
            e.Property(x => x.Note).HasMaxLength(1000);
            e.Property(x => x.ActorName).HasMaxLength(120);
            e.Property(x => x.ActorRole).HasMaxLength(20);
            e.HasOne(x => x.Incident).WithMany(x => x.Logs).HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.IncidentId);
        });
    }
}
