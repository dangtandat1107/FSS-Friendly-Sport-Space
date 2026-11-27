using System.ComponentModel.DataAnnotations;
using SportBooking.Web.Models;
namespace SportBooking.Web.ViewModels;

// ---------- Đặt sân kèm đồ uống ----------

public class BookingItemViewModel
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

// Dữ liệu form đặt sân từ hộp thoại. Items rỗng nghĩa là chỉ đặt sân (có thể vẫn nhận quà theo chuỗi tập luyện)
public class BookingViewModel
{
    public int CourtId { get; set; }
    public DateTime Date { get; set; }
    public int Hour { get; set; }
    public int Hours { get; set; } = 1;
    public List<BookingItemViewModel> Items { get; set; } = new();
}

public class ProductCardVm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; } = "";
    public string Icon { get; set; } = "";        // hiện khi chưa có ảnh
    public string? ImageUrl { get; set; }
    public int Available { get; set; }            // số còn có thể đặt (0 thì hiện "Hết hàng")
    public int MaxQty { get; set; }               // trần số lượng cho phép chọn: nhỏ hơn giữa Available và giới hạn mỗi đơn
}

public class OrderLineVm
{
    public int ProductId { get; set; }
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public bool IsFree { get; set; }
}

public class PromoBannerVm
{
    public string Kind { get; set; } = "";        // bulk hoặc streak
    public string Text { get; set; } = "";
}

// Một lớp dùng cho cả hai chiều: Date để dựng hộp thoại trên trang lịch; Products khi tải danh sách sản phẩm của sân;
// Lines, DrinksTotal, Banners, Errors khi xem trước đơn nước (server tính, trình duyệt chỉ hiển thị)
public class OrderDrinksViewModel
{
    public DateTime Date { get; set; }
    public int CourtId { get; set; }
    public int StreakDays { get; set; }
    public int MaxPerProduct { get; set; }
    public List<ProductCardVm> Products { get; set; } = new();

    public bool Ok { get; set; } = true;
    public List<OrderLineVm> Lines { get; set; } = new();
    public decimal DrinksTotal { get; set; }
    public List<PromoBannerVm> Banners { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

// ---------- Chủ sân quản lý đồ uống ----------

public class ProductFormVm
{
    public int Id { get; set; }

    [Display(Name = "Sân")] public int CourtId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm."), StringLength(120)]
    [Display(Name = "Tên sản phẩm")] public string Name { get; set; } = "";

    [StringLength(300, ErrorMessage = "Mô tả tối đa 300 ký tự.")]
    [Display(Name = "Mô tả")] public string? Description { get; set; }

    [Range(1000, 1000000, ErrorMessage = "Giá từ 1.000 đến 1.000.000 đồng.")]
    [Display(Name = "Giá (đồng)")] public decimal Price { get; set; } = 10000;

    [Display(Name = "Loại")] public ProductCategory Category { get; set; }

    [Range(0, 10000, ErrorMessage = "Tồn kho từ 0 đến 10.000.")]
    [Display(Name = "Tồn kho")] public int Stock { get; set; } = 50;

    [Display(Name = "Đang bán")] public bool IsActive { get; set; } = true;

    [Display(Name = "Ảnh sản phẩm")] public IFormFile? Image { get; set; }
    public string? ImageUrl { get; set; }
    public string? CourtName { get; set; }          // chỉ để hiển thị khi sửa
}

public class ProductIndexVm
{
    public List<Court> Courts { get; set; } = new();
    public List<Product> Products { get; set; } = new();
    public Dictionary<int, string> ImageUrls { get; set; } = new();
}

public record DrinkReportRow(string Label, decimal Revenue, int Items, int Gifts);
public record TopProductRow(string Name, int Quantity, decimal Revenue);

public class DrinkReportVm
{
    public string Range { get; set; } = "day";      // day, week hoặc month
    public List<DrinkReportRow> Rows { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public int TotalItems { get; set; }
    public int TotalGifts { get; set; }
    public List<TopProductRow> Top { get; set; } = new();
}

public class DrinkOrdersVm
{
    public DateTime Date { get; set; }
    public List<Booking> Orders { get; set; } = new();
}
