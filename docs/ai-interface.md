# Giao diện mô phỏng cho bước tích hợp AI

Trạng thái: core có sẵn và đã kiểm thử. Chưa tích hợp package ML-Agents, Python hoặc policy học máy. Tài liệu này định nghĩa ranh giới để tiếp tục mốc B sau khi xác minh Unity.

## Một nguồn mô phỏng

`Assets/AeroLab/Core/FlightSimulation.cs` là mã dùng trực tiếp trong game và bộ kiểm thử. Adapter học máy sau này phải gọi `Reset`, `Begin`, `Step`, `Observe` trên lớp này. Không dựng lại luật chơi bằng Python hoặc áp dụng thêm lực PhysX.

- `Reset(seed, obstacles)` đặt lại toàn bộ trạng thái và bố trí sân.
- `Begin()` bắt đầu lượt.
- `Step(action)` tiến đúng 0,02 giây mô phỏng và trả phần thưởng gia tăng.
- `Observe()` trả 20 float theo thứ tự cố định bên dưới.
- `Result`, `Running`, `Elapsed`, `TotalReward` cho biết kết quả và trạng thái lượt.

Game hiện lấy lệnh mỗi bước vật lý. Adapter ML-Agents dự kiến quyết định mỗi 5 bước, tức 0,1 giây; phải giữ hành động giữa các quyết định và cộng đủ phần thưởng của từng bước, dừng ngay khi kết thúc. Chưa có adapter thực hiện nhịp này.

## Hành động

Ba giá trị liên tục `[X, Y, Z]` trong [-1, 1]. Core giới hạn độ lớn tổng của hai trục ngang về 1 để không tăng gia tốc khi bay chéo. Giá trị NaN/Inf bị từ chối trước khi thay đổi trạng thái.

Xe được hỗ trợ bù trọng lực. Không có lệnh dọc thì hãm vận tốc dọc về 0; không có lệnh ngang thì hãm vận tốc ngang về 0. Quy tắc này giống nhau cho người chơi, bay mẫu và adapter tương lai.

## Quan sát: 20 float

| Index | Nội dung | Chuẩn hóa |
| --- | --- | --- |
| 0–2 | Vectơ từ tâm xe tới tâm bãi B, X/Y/Z | Chia 80 |
| 3–5 | Vận tốc X/Y/Z | Chia lần lượt 8/3/8 |
| 6 | Độ cao đáy xe so với mặt đất | Chia 30 |
| 7 | Tiếp đất | 0 hoặc 1 |
| 8–11 | Khoảng trống theo +X, −X, −Z, +Z | Tầm đo 30 m, chia 30 |
| 12–15 | Khoảng trống theo (+X,−Z), (−X,−Z), (+X,+Z), (−X,+Z) | Hướng đơn vị, chia 30 |
| 16–17 | Khoảng trống phía trên / dưới | Chia 30 |
| 18 | Thời gian còn lại | Chia 60 |
| 19 | Tiến độ giữ ổn định trên B | Chia 1 giây |

Khoảng trống tính bằng tia từ tâm xe vào các hộp vật cản đã mở rộng theo kích thước nửa xe và biên bay đã thu hẹp tương ứng. Tia trả khoảng cách tâm xe có thể dịch chuyển trước khi chạm hull; không phải cảm biến ảnh hoặc LiDAR hiện thực. Trong trạng thái terminal vượt biên, không mặc định mọi quan sát vẫn nằm trong [-1, 1]; adapter không được âm thầm thay đổi bố trí quan sát.

## Phần thưởng khởi điểm đã triển khai

- Mỗi bước: `0.1 × (khoảng cách trước − khoảng cách sau) − 0.02 × 0.02`.
- Thành công: cộng 10.
- Va chạm hoặc vượt biên: trừ 5.
- Hết giờ: trừ 1.
- Sau kết thúc hoặc trước Begin: Step trả 0 và không thay đổi trạng thái.

Các trọng số chưa được đánh giá bằng huấn luyện. Kết quả nhiệm vụ là tiêu chí chính; điểm thưởng chỉ là tín hiệu học.

## Kết thúc và đánh giá

- `Success`, `Collision`, `OutOfBounds`: terminal theo luật nhiệm vụ.
- `Timeout`: giới hạn 3000 bước. Khi tích hợp PPO, phải quyết định cách bootstrap theo hợp đồng của phiên bản ML-Agents; không mặc định mọi lỗi đều là kết thúc do hết giờ.
- Tại trạng thái terminal, adapter phải ghi đúng một kết quả rồi mới Reset.

`DemoPilot` biết vị trí các bãi và sử dụng độ cao 16 m để vượt các nhà có sẵn. Kiểm tra 100 seed hiện tại chỉ xác minh điều khiển mẫu và core, không phải bộ đánh giá AI V1 hoặc bằng chứng tổng quát hóa. Các seed chỉ dịch chuyển toàn bộ sân tối đa 2 m theo mỗi trục; chưa đa dạng hóa khoảng cách A/B hoặc bố trí nhà.

Bước tiếp theo: chạy Unity smoke test, chọn bộ ML-Agents/Python tương thích, thêm Agent adapter, xác minh chuyển dữ liệu và terminal/reset rồi mới chạy PPO. Không bật chế độ “AI huấn luyện” trong giao diện khi các bước này chưa hoàn thành.
