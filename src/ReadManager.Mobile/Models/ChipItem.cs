using CommunityToolkit.Mvvm.ComponentModel;

namespace ReadManager.Mobile.Models;

// Một chip lọc (thể loại, tiêu chí xếp hạng, sắp xếp...). IsSelected đổi thì giao diện đổi màu ngay.
public partial class ChipItem : ObservableObject
{
    public ChipItem(string key, string name, int? id = null)
    {
        Key = key;
        Name = name;
        Id = id;
    }

    public string Key { get; }   // mã dùng khi gọi API (vd "reads", "week")
    public string Name { get; }  // chữ hiển thị
    public int? Id { get; }      // id thể loại (nếu là chip thể loại; null = "Tất cả")

    [ObservableProperty] private bool isSelected;
}
