# Kết quả triển khai và kiểm tra môi trường

Ngày ghi nhận: 22/10/2026. Unity là yêu cầu bắt buộc của người dùng. Các file thử Godot đã tạo trong lúc thăm dò đã được gỡ khỏi repository.

## Phạm vi đã triển khai

- Dự án Unity dùng Built-in Render Pipeline, cảnh `FlightLab`, script bootstrap và GUID tài nguyên cố định.
- Sân 80 × 80 m, hai bãi đáp, bốn tòa nhà và xe bay hình khối có rotor.
- Mã điều khiển bàn phím, giữ độ cao, camera toàn cảnh/theo xe, HUD tiếng Việt, minimap, tạm dừng và chơi lại.
- Luật hạ cánh, va chạm, vượt biên, hết giờ; cảm biến khoảng cách và quan sát cố định cho tích hợp AI sau này.
- Bộ điều khiển bay mẫu theo luật, không phải policy học máy.
- Công cụ Editor để kiểm tra scene, chạy Play Mode smoke và build Linux/Windows/WebGL.

Danh sách trên mô tả mã đã viết. Những phần phụ thuộc UnityEngine chưa được biên dịch hoặc chạy bằng Editor trong máy này.

## Tài nguyên và công cụ quan sát thực tế

| Hạng mục | Kết quả |
| --- | --- |
| Hệ điều hành | Debian GNU/Linux 13.6, x86_64 |
| CPU | 5 logical CPU được công bố; quota container tương đương 4 CPU |
| RAM | Giới hạn container 32 GiB |
| Đĩa trống lúc bắt đầu | Khoảng 30 GiB |
| Hiển thị | Không có DISPLAY |
| Unity Editor / Hub | Không tìm thấy trong công cụ hiện có |
| Giấy phép Unity | Không thấy các file giấy phép ở các vị trí Linux phổ biến; chưa chứng minh khả năng kích hoạt bằng Hub |
| .NET kiểm thử | SDK 8.0.426, cài trong /workspace/.tools/dotnet |
| Python | 3.12.14, chưa chọn làm phiên bản cho ML-Agents |
| ML-Agents / PyTorch | Chưa cài |

Unity 6000.0.62f1 là phiên bản dự án nhắm tới và được pin trong `ProjectVersion.txt`, chưa được xác minh thực tế trên Debian/cloud này. Không coi Debian là nền tảng Editor được hỗ trợ trước khi đối chiếu tài liệu và chạy thử.

## Kiểm tra đã chạy

Lệnh `bash tools/check-core.sh` chạy trực tiếp mã core trong Assets bằng .NET. Bộ kiểm tra gồm:

- Đặt lại trạng thái; không di chuyển trước khi bắt đầu.
- Cất cánh, hãm về hover, giới hạn tốc độ và chuẩn hóa bay chéo.
- Đủ một giây ổn định, tiếp xúc mặt đất và nằm trọn trong bãi đích mới thành công.
- Rời bãi xóa tiến độ; tiếp đất nhẹ ngoài bãi không thành công.
- Tiếp đất mạnh, va chạm cạnh xe, ray quét không xuyên hộp.
- Giới hạn toàn bộ hull, trần cao, đúng 3000 bước thì hết giờ.
- Ưu tiên kết quả đồng thời, đóng băng lượt đã kết thúc.
- Cảm biến, kích thước quan sát, từ chối NaN, tái lập seed.
- Điều khiển mẫu hoàn thành 100 seed qua cùng core.
- Phân tích cú pháp C# 9 cho tất cả script Unity; kiểm tra liên kết GUID/scene/build settings/assembly.

Kết quả lần chạy cuối: **20 passed, 0 failed**. Không dùng bộ kiểm tra .NET để chứng nhận Unity API, hệ thống nhập liệu, shader, hiển thị tiếng Việt, camera hoặc chất lượng giao diện.

Đã thử đường cài mới của `tools/install-check-tools.sh` vào thư mục probe riêng: tải SDK chính thức, xác minh SHA-512, giải nén và chạy được `dotnet --version` = 8.0.426. Metadata hash từ:

`https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json`

Lần khởi tạo .NET đầu tiên trong sandbox bị chặn tạo mutex `NuGet-Migrations`. Chạy khởi tạo kiểm thử với escalation được hệ thống chấp nhận đã giải quyết bước này; các lượt chạy tiếp theo hoạt động trong sandbox thông thường.

## Blocker Unity và cấu hình đã lưu

Yêu cầu đọc metadata phiên bản tại `services.api.unity.com` nhận `CONNECT tunnel failed, response 403` từ proxy. Đây là chặn kết nối trước khi nhận phản hồi từ Unity, không phải bằng chứng tài khoản Unity bị từ chối.

Đã lưu bản nháp thêm bốn hostname: `services.api.unity.com`, `download.unity3d.com`, `packages.unity.com`, `docs.unity3d.com`. Giữ nguyên preset package managers. Việc lưu draft chưa áp dụng runtime và chưa publish.

Để tiếp tục trên cloud, cần review/save cấu hình trong environment settings rồi publish theo UI, sau đó kiểm tra lại kết nối. Chỉ sau khi tải được Editor và xác minh cách kích hoạt hợp lệ mới có thể chạy import, Play Mode và build. Không yêu cầu gửi mật khẩu, key hoặc file giấy phép trong chat.

Đường thay thế để thử ngay: mở source bằng Unity Hub trên máy có Editor và giấy phép hợp lệ, theo README. Điều này không thay đổi yêu cầu dùng Unity.

## Chưa chạy và chưa đạt nghiệm thu

- Unity import/biên dịch assembly, Play Mode, hình ảnh và điều khiển phím thật.
- Ba lượt hoàn thành bằng người chơi để nghiệm thu mốc A.
- Build và chạy binary hoặc WebGL; chưa tạo bản game chạy độc lập.
- Unity–Python, cập nhật policy PPO, checkpoint và suy luận model; mốc B chưa bắt đầu.
- Bộ đánh giá AI riêng 100 lượt; kiểm tra demo hiện tại không thay thế mục này.

Không gọi toàn bộ môi trường sẵn sàng hoặc mốc A/B hoàn thành khi các mục bắt buộc trên còn chưa chạy.
