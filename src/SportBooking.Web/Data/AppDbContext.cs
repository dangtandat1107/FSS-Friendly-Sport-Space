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
    }
}
