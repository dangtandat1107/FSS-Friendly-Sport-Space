namespace SportBooking.Web.Services;

public interface IAvatarService
{
    /// <summary>Đường dẫn ảnh đại diện của người dùng, null nếu chưa có.</summary>
    string? GetUrl(string userId);

    /// <summary>Lưu ảnh mới (thay ảnh cũ). Chỉ nhận JPG, PNG, WEBP, tối đa 2 MB.</summary>
    Task<(bool Ok, string? Error)> SaveAsync(string userId, IFormFile file);

    void Delete(string userId);
}

// Ảnh lưu trong wwwroot/uploads/avatars/<mã người dùng>.<đuôi>, không cần thêm cột trong database.
public class AvatarService : IAvatarService
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private static readonly string[] Extensions = { ".jpg", ".png", ".webp" };
    private readonly string _dir;

    public AvatarService(IWebHostEnvironment env)
    {
        var root = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        _dir = Path.Combine(root, "uploads", "avatars");
        Directory.CreateDirectory(_dir);
    }

    // Mã người dùng của Identity chỉ gồm chữ, số, dấu gạch ngang: chặn mọi ký tự có thể làm lệch đường dẫn
    private static bool SafeId(string id) => id.Length > 0 && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    public string? GetUrl(string userId)
    {
        if (!SafeId(userId)) return null;
        foreach (var ext in Extensions)
        {
            var path = Path.Combine(_dir, userId + ext);
            if (File.Exists(path))
                return $"/uploads/avatars/{userId}{ext}?v={File.GetLastWriteTimeUtc(path).Ticks}";   // ?v= để trình duyệt tải lại khi đổi ảnh
        }
        return null;
    }

    public async Task<(bool Ok, string? Error)> SaveAsync(string userId, IFormFile file)
    {
        if (!SafeId(userId)) return (false, "Tài khoản không hợp lệ.");
        if (file.Length == 0) return (false, "Tệp rỗng.");
        if (file.Length > MaxBytes) return (false, "Ảnh quá lớn, tối đa 2 MB.");

        // Kiểm tra loại ảnh theo nội dung thật của tệp (không tin vào đuôi tệp hay loại do trình duyệt báo)
        var header = new byte[12];
        await using (var stream = file.OpenReadStream())
        {
            var read = await stream.ReadAtLeastAsync(header, 12, throwOnEndOfStream: false);
            if (read < 12) return (false, "Tệp không phải ảnh hợp lệ.");
        }
        var ext = DetectExtension(header);
        if (ext == null) return (false, "Chỉ nhận ảnh JPG, PNG hoặc WEBP.");

        Delete(userId);
        await using var fs = new FileStream(Path.Combine(_dir, userId + ext), FileMode.Create);
        await file.CopyToAsync(fs);
        return (true, null);
    }

    public void Delete(string userId)
    {
        if (!SafeId(userId)) return;
        foreach (var ext in Extensions)
        {
            var path = Path.Combine(_dir, userId + ext);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static string? DetectExtension(byte[] h)
    {
        if (h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ".jpg";
        if (h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) return ".png";
        if (h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P') return ".webp";
        return null;
    }
}
