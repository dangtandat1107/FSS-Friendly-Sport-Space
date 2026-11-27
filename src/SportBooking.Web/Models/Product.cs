namespace SportBooking.Web.Models;

// Sản phẩm trong danh mục "Đồ uống & Phụ kiện" của một sân. Ảnh lưu theo mã sản phẩm trong wwwroot/uploads/products (xem ImageStore),
// không có cột ảnh trong database giống ảnh sân và ảnh đại diện.
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public ProductCategory Category { get; set; }
    public int CourtId { get; set; }
    public Court? Court { get; set; }
    public bool IsActive { get; set; } = true;      // tắt thì ẩn khỏi khách; sản phẩm đã từng được đặt chỉ ẩn chứ không xóa
    public int Stock { get; set; }                  // số lượng thực còn trong kho (trừ khi lịch được xác nhận)
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

// Một dòng trong đơn nước của lịch đặt. Quà tặng (IsFreeGift) có TotalPrice = 0 nhưng vẫn ghi đơn giá gốc để chủ sân biết giá trị đã tặng.
public class BookingItem
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking? Booking { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public string ProductName { get; set; } = "";   // chép lại tên lúc đặt, để lịch sử và phiếu chuẩn bị không đổi khi chủ sân đổi tên sản phẩm
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public bool IsFreeGift { get; set; }
}

// Khuyến mãi lưu trong database để đổi ngưỡng mà không phải sửa code (xem PromotionService)
public class Promotion
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public PromotionType Type { get; set; }
    public int Condition { get; set; }               // ngưỡng áp dụng (số chai, số ngày trong chuỗi)
    public int Reward { get; set; } = 1;             // số chai được tặng
    public bool IsActive { get; set; } = true;
}
