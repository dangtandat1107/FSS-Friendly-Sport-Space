using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Models;
namespace SportBooking.Web.Data;

public static class DbSeeder
{
    public static readonly string[] Roles = { "Admin", "Owner", "Customer" };

    private record SampleCourt(SportType Sport, string Name, string District, string Address, decimal Price);

    private static readonly SampleCourt[] Samples =
    {
        new(SportType.Badminton,  "Sân cầu lông A1",       "Quận 1",  "12 Nguyễn Huệ",        80000),
        new(SportType.Badminton,  "Sân cầu lông A2",       "Quận 1",  "12 Nguyễn Huệ",        80000),
        new(SportType.Football,   "Sân bóng đá mini B1",   "Quận 7",  "45 Nguyễn Thị Thập",  350000),
        new(SportType.Tennis,     "Sân tennis C1",         "Quận 3",  "88 Võ Văn Tần",       200000),
        new(SportType.Pickleball, "Sân pickleball P1",     "Quận 2",  "25 Trần Não",         120000),
        new(SportType.Pickleball, "Sân pickleball P2",     "Quận 2",  "25 Trần Não",         120000),
        new(SportType.Basketball, "Sân bóng rổ R1",        "Quận 10", "70 Lý Thường Kiệt",   250000),
        new(SportType.Basketball, "Sân bóng rổ R2",        "Quận 10", "70 Lý Thường Kiệt",   250000),
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

        // Môn nào chưa có sân nào thì thêm sân mẫu (database cũ khi thêm môn mới cũng được bổ sung)
        foreach (var sport in Samples.Select(s => s.Sport).Distinct())
        {
            if (await db.Courts.AnyAsync(c => c.SportType == sport)) continue;
            foreach (var s in Samples.Where(x => x.Sport == sport))
                db.Courts.Add(new Court { Name = s.Name, SportType = s.Sport, District = s.District, Address = s.Address, PricePerHour = s.Price, OwnerId = owner.Id });
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
