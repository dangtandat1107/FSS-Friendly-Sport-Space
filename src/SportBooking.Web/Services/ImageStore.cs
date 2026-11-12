namespace SportBooking.Web.Services;

public interface IImageStore
{
    /// <summary>Đường dẫn ảnh, null nếu chưa có.</summary>
    string? GetUrl(string folder, string key);

    /// <summary>Kiểm tra tệp tải lên có đúng là ảnh JPG, PNG, WEBP và không quá lớn.</summary>
    Task<(bool Ok, string? Error)> ValidateAsync(IFormFile file, long maxBytes);

    /// <summary>Lưu ảnh (thay ảnh cũ nếu có).</summary>
    Task<(bool Ok, string? Error)> SaveAsync(string folder, string key, IFormFile file, long maxBytes);

    void Delete(string folder, string key);
}

// Ảnh lưu trong wwwroot/uploads/<thư mục>/<mã>.<đuôi>. Không cần thêm cột nào trong database.
public class ImageStore : IImageStore
{
    private static readonly string[] Folders = { "avatars", "courts" };
    private static readonly string[] Extensions = { ".jpg", ".png", ".webp" };
    private readonly string _root;

    public ImageStore(IWebHostEnvironment env)
    {
        var web = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        _root = Path.Combine(web, "uploads");
        foreach (var f in Folders) Directory.CreateDirectory(Path.Combine(_root, f));
    }

    private string? Dir(string folder) => Folders.Contains(folder) ? Path.Combine(_root, folder) : null;

    // Mã chỉ gồm chữ, số, dấu gạch ngang: chặn mọi ký tự có thể làm lệch đường dẫn
    private static bool SafeKey(string key) => key.Length is > 0 and <= 64 && key.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    public string? GetUrl(string folder, string key)
    {
        var dir = Dir(folder);
        if (dir == null || !SafeKey(key)) return null;
        foreach (var ext in Extensions)
        {
            var path = Path.Combine(dir, key + ext);
            if (File.Exists(path))
                return $"/uploads/{folder}/{key}{ext}?v={File.GetLastWriteTimeUtc(path).Ticks}";   // ?v= để trình duyệt tải lại khi đổi ảnh
        }
        return null;
    }

    public async Task<(bool Ok, string? Error)> ValidateAsync(IFormFile file, long maxBytes)
    {
        var (ok, error, _) = await InspectAsync(file, maxBytes);
        return (ok, error);
    }

    public async Task<(bool Ok, string? Error)> SaveAsync(string folder, string key, IFormFile file, long maxBytes)
    {
        var dir = Dir(folder);
        if (dir == null || !SafeKey(key)) return (false, "Đường dẫn lưu ảnh không hợp lệ.");

        var (ok, error, ext) = await InspectAsync(file, maxBytes);
        if (!ok) return (false, error);

        Delete(folder, key);
        await using var fs = new FileStream(Path.Combine(dir, key + ext), FileMode.Create);
        await file.CopyToAsync(fs);
        return (true, null);
    }

    public void Delete(string folder, string key)
    {
        var dir = Dir(folder);
        if (dir == null || !SafeKey(key)) return;
        foreach (var ext in Extensions)
        {
            var path = Path.Combine(dir, key + ext);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // Kiểm tra loại ảnh theo nội dung thật của tệp (không tin vào đuôi tệp hay loại do trình duyệt báo)
    private static async Task<(bool Ok, string? Error, string? Ext)> InspectAsync(IFormFile file, long maxBytes)
    {
        if (file.Length == 0) return (false, "Tệp rỗng.", null);
        if (file.Length > maxBytes) return (false, $"Ảnh quá lớn, tối đa {maxBytes / 1024 / 1024} MB.", null);

        var h = new byte[12];
        await using (var stream = file.OpenReadStream())
        {
            var read = await stream.ReadAtLeastAsync(h, 12, throwOnEndOfStream: false);
            if (read < 12) return (false, "Tệp không phải ảnh hợp lệ.", null);
        }

        string? ext = null;
        if (h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) ext = ".jpg";
        else if (h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) ext = ".png";
        else if (h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P') ext = ".webp";

        return ext == null ? (false, "Chỉ nhận ảnh JPG, PNG hoặc WEBP.", null) : (true, null, ext);
    }
}
