using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Models;
namespace SportBooking.Web.Data;

public static class DbSeeder
{
    public static readonly string[] Roles = { "Admin", "Owner", "Customer" };

    private record SampleCourt(SportType Sport, string Name, string District, string Address, decimal Price,
        CourtFormat Format, CourtSurface Surface, bool Indoor);

    private static readonly SampleCourt[] Samples =
    {
        // Cầu lông: chia theo mặt sân
        new(SportType.Badminton,  "Sân cầu lông A1",            "Quận 1",  "12 Nguyễn Huệ",       80000,  CourtFormat.None,      CourtSurface.PvcMat,          true),
        new(SportType.Badminton,  "Sân cầu lông A2",            "Quận 1",  "12 Nguyễn Huệ",       80000,  CourtFormat.None,      CourtSurface.Wood,            true),
        new(SportType.Badminton,  "Sân cầu lông VIP A3",        "Quận 1",  "12 Nguyễn Huệ",       140000, CourtFormat.None,      CourtSurface.Wood,            true),
        // Bóng đá: sân 5, 7, 11 người
        new(SportType.Football,   "Sân bóng đá mini B1",        "Quận 7",  "45 Nguyễn Thị Thập",  350000, CourtFormat.Five,      CourtSurface.ArtificialGrass, false),
        new(SportType.Football,   "Sân bóng đá 7 người B2",     "Quận 7",  "45 Nguyễn Thị Thập",  500000, CourtFormat.Seven,     CourtSurface.ArtificialGrass, false),
        new(SportType.Football,   "Sân bóng đá 11 người B3",    "Thủ Đức", "100 Võ Văn Ngân",     1200000, CourtFormat.Eleven,   CourtSurface.NaturalGrass,    false),
        // Tennis: mặt sân và trong nhà, ngoài trời
        new(SportType.Tennis,     "Sân tennis C1",              "Quận 3",  "88 Võ Văn Tần",       200000, CourtFormat.None,      CourtSurface.Hard,            false),
        new(SportType.Tennis,     "Sân tennis đất nện C2",      "Quận 3",  "88 Võ Văn Tần",       300000, CourtFormat.None,      CourtSurface.Clay,            false),
        new(SportType.Tennis,     "Sân tennis có mái che C3",   "Quận 3",  "88 Võ Văn Tần",       280000, CourtFormat.None,      CourtSurface.Hard,            true),
        // Pickleball: một loại sân cho cả đánh đơn và đôi
        new(SportType.Pickleball, "Sân pickleball P1",          "Quận 2",  "25 Trần Não",         120000, CourtFormat.None,      CourtSurface.Hard,            false),
        new(SportType.Pickleball, "Sân pickleball P2",          "Quận 2",  "25 Trần Não",         150000, CourtFormat.None,      CourtSurface.Wood,            true),
        // Bóng rổ: nửa sân và cả sân
        new(SportType.Basketball, "Sân bóng rổ R1",             "Quận 10", "70 Lý Thường Kiệt",   250000, CourtFormat.HalfCourt, CourtSurface.Hard,            false),
        new(SportType.Basketball, "Sân bóng rổ R2",             "Quận 10", "70 Lý Thường Kiệt",   250000, CourtFormat.FullCourt, CourtSurface.Wood,            true),
        new(SportType.Basketball, "Sân bóng rổ 3x3 R3",         "Quận 10", "70 Lý Thường Kiệt",   150000, CourtFormat.HalfCourt, CourtSurface.Hard,            false),
    };

    public static async Task InitAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var log = sp.GetRequiredService<ILogger<AppDbContext>>();

        // SQL Server trong Docker khởi động chậm -> thử lại tối đa 10 lần
        for (var i = 1; ; i++)
        {
            try { await db.Database.MigrateAsync(); break; }
            catch (Exception ex) when (i < 10)
            {
                log.LogWarning("DB chưa sẵn sàng (lần {Try}): {Msg}", i, ex.Message);
                await Task.Delay(3000);
            }
        }

        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var r in Roles)
            if (!await roleMgr.RoleExistsAsync(r)) await roleMgr.CreateAsync(new IdentityRole(r));

        var userMgr = sp.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureUser(userMgr, "admin@demo.com", "Quản trị viên", "Admin@123", "Admin");
        var owner = await EnsureUser(userMgr, "owner@demo.com", "Chủ sân Demo", "Owner@123", "Owner");
        await EnsureUser(userMgr, "customer@demo.com", "Khách Demo", "Customer@123", "Customer");

        // Thêm sân mẫu chưa có (theo tên). Sân mẫu đã có từ trước mà chưa có thông tin loại sân thì được bổ sung.
        foreach (var s in Samples)
        {
            var existing = await db.Courts.FirstOrDefaultAsync(c => c.Name == s.Name);
            if (existing == null)
            {
                db.Courts.Add(new Court
                {
                    Name = s.Name, SportType = s.Sport, District = s.District, Address = s.Address, PricePerHour = s.Price,
                    CourtFormat = s.Format, Surface = s.Surface, IsIndoor = s.Indoor, OwnerId = owner.Id
                });
            }
            else if (existing.CourtFormat == CourtFormat.None && existing.Surface == CourtSurface.Unspecified && !existing.IsIndoor)
            {
                existing.CourtFormat = s.Format; existing.Surface = s.Surface; existing.IsIndoor = s.Indoor;
            }
        }
        await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> EnsureUser(UserManager<ApplicationUser> um, string email, string name, string pwd, string role)
    {
        var u = await um.FindByEmailAsync(email);
        if (u != null) return u;
        u = new ApplicationUser { UserName = email, Email = email, FullName = name, EmailConfirmed = true };
        var r = await um.CreateAsync(u, pwd);
        if (!r.Succeeded) throw new Exception(string.Join("; ", r.Errors.Select(e => e.Description)));
        await um.AddToRoleAsync(u, role);
        return u;
    }
}
