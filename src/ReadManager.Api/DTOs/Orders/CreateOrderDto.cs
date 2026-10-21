using System.ComponentModel.DataAnnotations;

namespace ReadManager.Api.DTOs.Orders;

// TẠO ĐƠN — client chỉ gửi MUA CÁI GÌ, KHÔNG gửi giá (giá luôn lấy từ database).
//   Mua truyện : { "orderType": "Story", "storyId": 5 }
//   Mua gói    : { "orderType": "Plan",  "planId": 2 }          (tự lấy phiên bản gói mới nhất đang bán)
//           hoặc { "orderType": "Plan",  "planVersionId": 7 }   (Web đang hiện phiên bản nào thì gửi phiên bản đó;
//                                                                 nếu Admin vừa đổi giá → báo lỗi để tải lại trang)
public class CreateOrderDto
{
    [Required]
    [RegularExpression("^(Plan|Story)$", ErrorMessage = "OrderType phải là Plan hoặc Story.")]
    public string OrderType { get; set; } = string.Empty;

    public int? StoryId { get; set; }
    public int? PlanId { get; set; }
    public int? PlanVersionId { get; set; }
}
