# AI chọn độ cao và hướng bay dựa vào đâu?

Model bay đang phát hành nhận **26 số**, không nhận ảnh camera hoặc danh sách tọa độ/chiều cao từng nhà. Bộ `continuity-v2` không bật mặt nạ loại bỏ đặc trưng (`feature_mask=false`). Việc một đầu vào được cung cấp không chứng minh nó có tác động lớn đến quyết định.

| Chỉ số | Dữ liệu đầu vào | Quy ước |
| --- | --- | --- |
| 0–2 | Vector từ drone đến đích X/Y/Z | Chia 80 m |
| 3–5 | Vận tốc X/Y/Z | Chia lần lượt 8, 3, 8 m/s |
| 6 | Độ cao đáy drone so với mặt đất phẳng | Chia 30 m |
| 7 | Đang chạm đất | 0 hoặc 1 |
| 8–17 | 10 khoảng cách: +X, −X, −Z, +Z, bốn chéo ngang, trên, dưới | Khoảng trống tính theo hull drone; tối đa 30 m, chia 30; mất mẫu = −1 |
| 18 | Thời gian còn lại của chặng | Tỷ lệ so với giới hạn chặng |
| 19 | Tiến trình giữ trạng thái hạ cánh ổn định | Tối đa 1 giây |
| 20 | Pin còn lại | Tỷ lệ dung lượng |
| 21 | Tải trọng | Tỷ lệ tải tối đa |
| 22–23 | Đang quay về; cờ khẩn cấp | 0 hoặc 1 |
| 24 | Thời gian đến hạn giao trừ thời điểm hiện tại | Chia thời lượng kịch bản; có thể âm |
| 25 | Mẫu dữ liệu hợp lệ | 0 ở mẫu khởi đầu chưa có do độ trễ, sau đó 1 |

Vị trí tương đối được tính từ vị trí/đích trong hệ tọa độ mô phỏng. Đây là chức năng định vị tương tự đầu ra định vị cục bộ; không mô phỏng tín hiệu vệ tinh hay bộ thu GPS thật. Cấu hình nhiễu thêm sai số vị trí/độ cao, sai số và mất mẫu khoảng cách, cùng độ trễ toàn bộ quan sát. Vận tốc và tiếp đất vẫn lý tưởng trong mô hình hiện tại.

Mỗi **5 tick = 0,1 giây**, mạng xuất ba lệnh liên tục X/Y/Z. Core tích phân chuyển động ở 50 Hz; các lệnh nối tiếp tạo thành vệt bay. Model không xuất một danh sách waypoint hay một độ cao hành trình cố định. Tầng điều phối chọn đơn; quy trình giao hàng đặt đích là điểm giao hoặc kho.

Hành vi còn chịu ảnh hưởng của **cách huấn luyện**, không chỉ đầu vào hiện tại. Bộ điều khiển mẫu `ClearancePilot` dùng vị trí ngang tương đối, vận tốc và độ cao; mục tiêu độ cao tăng dần theo khoảng cách ngang đến đích và đạt khoảng 26 m khi ở xa. Bộ sinh nhà giới hạn dưới 24 m và giữ lối tiếp cận bãi trống. Dữ liệu học có giám sát/DAgger tạo thiên hướng bay cao rồi tiến đến đích; PPO tinh chỉnh tiếp bằng phần thưởng tiến gần đích, hạ cánh/giao hàng và hình phạt va chạm, vượt biên, hết thời gian hoặc thất bại nhiệm vụ.

Vì vậy, mô hình đã chạy tốt trên nhiều bố trí trong phạm vi đó, nhưng chưa có bằng chứng nó suy luận tránh vật cản tổng quát từ cảm biến hoặc chọn độ cao tiết kiệm nhất theo mái từng nhà. Chưa đo mức quan trọng từng đầu vào bằng thí nghiệm loại bỏ/thay đổi đặc trưng. Nhà 28 m hay vật cản chắn đường hạ cánh cần thử nghiệm riêng; kết quả cũ không bảo đảm thành công.

**Tuyến xanh** trong giao diện là tham chiếu A* ở độ cao lập kế hoạch 16 m, dùng bản đồ và trạng thái chính xác. Nó không phải tuyến mà mạng AI bắt buộc đi theo. **Vệt trắng** là quỹ đạo thực tế. Bộ kiểm tra năng lượng trước chuyến và nhánh khẩn cấp vẫn có sử dụng bộ lập đường theo bản đồ; toàn hệ thống vì thế chưa phải một hệ tự hành hoàn toàn chỉ dựa vào cảm biến cục bộ.

Nguồn mã: `Assets/AeroLab/Core/FlightSimulation.cs` (`Observe`, `Sensors`, `Step`), `DeliverySimulation.cs` (`ObserveFlight`, `Step`, `RequiredEnergy`), `ClearancePilot.cs`, `RoutePlanner.cs`, `Assets/AeroLab/Runtime/DeliveryFlightAgent.cs` và `training/studies/continuity-v2.json`.
