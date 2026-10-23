# AeroLab — GameBuild-TrainAI

Dự án Unity 3D nghiên cứu drone không camera và AI giao hàng trong khuôn viên.

- **DeliveryLab:** một kho/sạc, bốn điểm giao, pin, tải trọng, hạn giao, cảm biến khoảng cách và hai tầng AI bay/điều phối.
- **FlightLab:** giữ sân xe bay A–B trong 60 giây để lái tay và kiểm tra hồi quy.

Đợt nghiên cứu `continuity-v2-r2` đã hoàn tất 2.400 lượt kiểm thử: 600 lượt một đơn đều giao–quay về thành công; 1.800 phiên nhiều đơn đều đạt tiêu chí vận hành mô phỏng, không ghi nhận sự cố bay. Phiên nhiều đơn dừng ở mốc 900 giây và vẫn có đơn chưa hoàn tất.

AI điều phối đạt 39,00% đúng hạn ở cấu hình lý tưởng và 38,72% khi có nhiễu; FIFO lần lượt 17,28% và 17,00%. AI giao được ít đơn hơn tổng thể. Xem [báo cáo kết quả và giới hạn](docs/delivery-progress.md).

## Theo dõi thay đổi cục bộ

Chạy `python3 tools/worktree-report.py --html`, rồi mở [bảng thay đổi](artifacts/worktree/index.html) để xem file theo nhóm, lọc trạng thái và đọc diff. Chạy lại lệnh và tải lại trang khi cần cập nhật. Không bắt buộc commit/push sau mỗi lần sửa; xem [quy trình Git](docs/git-workflow.md) và [quy định làm việc](AGENTS.md).

## DeliveryLab — drone không camera và logistics

Mở **AeroLab → Open delivery campus**, bấm **Play → Bắt đầu / Tiếp tục**. Một drone giao từ kho tới bốn điểm và quay về sạc; có hạn giao, tải trọng, pin và cảm biến khoảng cách. Chế độ mặc định là **tham chiếu theo luật**, không phải AI được huấn luyện.

Chạy `bash tools/setup-delivery-ai.sh` trước khi Unity import trong checkout mới. Xem [hướng dẫn DeliveryLab](docs/delivery-lab.md) để kiểm thử, build, huấn luyện hai tầng PPO, đóng băng model và đánh giá theo seed. Package ML-Agents 4.0.0 cần bản vá API Match3 có điều kiện và cấu hình importer gRPC Linux để biên dịch với Unity 6000.6; setup tái tạo bản vá từ archive chính thức đã kiểm checksum.

## Tiếp tục huấn luyện và dùng Colab

Xem [quy trình lưu–khôi phục, chuyển máy và nghiên cứu nhiều chuyến](docs/delivery-continuation.md). Mỗi phiên mặc định tối đa hai giờ; dùng lại đúng cấu hình với `--resume` để tiếp tục checkpoint đã kiểm tra. [Notebook Colab](notebooks/AeroLab-Colab.ipynb) hỗ trợ phiên mới và khôi phục snapshot từ Drive; chưa được chạy trên Colab thật. Quy trình đóng gói–chuyển thư mục–tiếp tục–nhập lại checkpoint đã qua kiểm tra cục bộ.

## Mở và chơi bằng Unity

1. Cài Unity Hub và **Unity 6000.6.7f1**, kích hoạt giấy phép hợp lệ trong Hub. Đây là phiên bản đã dùng để xác minh trên máy cục bộ; cloud được kiểm tra riêng.
2. Trong Hub chọn **Add → Add project from disk**, chọn thư mục gốc repository, nơi có `Assets`, `Packages`, `ProjectSettings`.
3. Đợi Unity import và biên dịch. Mở `Assets/AeroLab/Scenes/FlightLab.unity`, hoặc chọn menu **AeroLab → Open flight arena**.
4. Bấm **Play**. Cảnh được dựng bằng mã khi chạy; Game view sẽ có sân thử, xe bay và bảng điều khiển tiếng Việt.
5. Bấm **Bắt đầu bay** hoặc Enter. Có thể bật/tắt vật cản, đổi seed ở bảng trái trước khi bắt đầu.

### Mở trực tiếp trên Linux khi Hub không nhận ra dự án

Tại thư mục repository, chạy:

```sh
bash tools/open-unity.sh
```

Script tìm Editor đúng phiên bản trong các thư mục Unity Hub thông thường, mở checkout bằng `-projectPath`, mở cảnh FlightLab và yêu cầu vào Play Mode. Nó không cài Editor, kích hoạt giấy phép, xóa khóa hay ghi đè dữ liệu dự án. Nếu Editor nằm ở vị trí tùy chỉnh:

```sh
UNITY_EDITOR='/đường/dẫn/tới/Editor/Unity' bash tools/open-unity.sh
```

Nếu máy chỉ có Unity Hub, cần cài thêm **Unity Editor 6000.6.7f1** trong mục Installs. Nếu script báo không tìm thấy Editor, kiểm tra mục này trước. Đường mở trực tiếp chưa được thử với Editor thật trên cloud.

| Phím | Chức năng |
| --- | --- |
| W / S | Di chuyển theo Z − / + |
| A / D | Di chuyển theo X − / + |
| Space / Ctrl | Lên / xuống |
| Thả phím | Hãm rồi giữ độ cao |
| C | Đổi camera toàn cảnh / theo xe |
| Chuột phải / cuộn chuột | Xoay / zoom camera |
| Esc | Tạm dừng / tiếp tục |
| R | Đặt lại lượt, về màn bắt đầu |

Xe tự cân bằng, giữ hướng cố định. Bay lên đủ cao để vượt các nhà trước khi tiếp cận B. Hạ xuống nhẹ: tiếp đất nhanh hơn 1 m/s sẽ thất bại. Toàn bộ xe phải nằm trong bãi B, tiếp đất và giữ tốc độ không quá 0,5 m/s trong 1 giây để thắng.

Nút **Bay mẫu** chạy bộ điều khiển theo luật trên sân đã biết; có thể chuyển sang lái tay giữa lượt. **Đây chưa phải AI được huấn luyện.** Mất focus cửa sổ sẽ tạm dừng; quay lại bấm Tiếp tục.

## Kiểm tra

Phần mô phỏng C# độc lập Unity, có thể kiểm thử bằng .NET SDK 8:

```sh
bash tools/check-core.sh
```

Hoặc trên máy có .NET SDK 8:

```sh
dotnet run --project tools/CoreChecks/CoreChecks.csproj --configuration Release
```

Nếu cloud Linux x64 chưa có SDK, chạy `bash tools/install-check-tools.sh`. Script dùng bản .NET cố định và kiểm tra SHA-512 từ metadata chính thức. Các kiểm tra này không thay thế việc biên dịch API Unity, chạy Play Mode hoặc kiểm tra đồ họa.

Khi có Editor đã kích hoạt:

- **AeroLab → Validate project**: kiểm tra scene, tài nguyên và lượt bay mẫu.
- **AeroLab → Run Play Mode smoke test**: chạy bay mẫu qua vòng đời game thật, thử đặt lại và tạm dừng.
- Dòng lệnh Linux/macOS: đặt `UNITY_EDITOR` trỏ tới executable rồi chạy `bash tools/check-unity.sh`.

Smoke test mất khoảng một phút. Không thêm `-quit` vào lệnh smoke vì kiểm tra Play Mode kết thúc bất đồng bộ. Trong cloud hiện tại, kiểm tra Editor này **chưa được chạy**.

## Build

Sau khi kiểm thử trong Editor, cài module nền tảng tương ứng qua Hub rồi dùng menu **AeroLab → Build → Linux x64 / Windows x64 / WebGL**. Đầu ra nằm trong `Builds/` và không được đưa vào Git. Nên build Linux/Windows trước; WebGL còn cần kiểm tra riêng trong trình duyệt và phải được phục vụ bằng HTTP.

Ví dụ build Linux bằng dòng lệnh khi đã có Editor, giấy phép và module:

```sh
"$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PWD" \
  -executeMethod AeroLab.Editor.BuildTools.Linux -logFile artifacts/unity-build.log
```

Tạo thư mục `artifacts/` trước khi dùng ví dụ này. Chưa có binary hoặc WebGL build nào được xác minh trong cloud hiện tại.

## Cấu trúc

- `Assets/AeroLab/Core`: trạng thái bay, luật lượt chơi, cảm biến và bộ điều khiển mẫu; không phụ thuộc UnityEngine.
- `Assets/AeroLab/Runtime`: nhập phím, dựng cảnh 3D, camera và HUD.
- `Assets/AeroLab/Editor`: mở cảnh, kiểm tra Play Mode và build.
- `Assets/AeroLab/Scenes/FlightLab.unity`: cảnh khởi động đã đưa vào build settings.
- `tools/CoreChecks`: kiểm thử trực tiếp mã C# dùng trong game, không dùng bản sao mô phỏng.

Mô phỏng chạy bước cố định 0,02 giây; hình khối và va chạm AABB quét được tính trong core. Không dùng Rigidbody/PhysX song song, tránh hai bộ vật lý cho kết quả khác nhau.

## Tài liệu

- [Đặc tả phiên bản đầu — bước 1](docs/v1-spec.md)
- [Kế hoạch kiểm tra công nghệ và môi trường — bước 2](docs/step-2-environment-plan.md)
- [Kết quả kiểm tra và các phần còn bị chặn](docs/environment-validation.md)
- [Giao diện mô phỏng cho bước tích hợp AI](docs/ai-interface.md)

## Trạng thái

DeliveryLab có mô phỏng giao hàng, giao diện tiếng Việt, bản Linux và hai tầng model đã được kiểm thử trên ba seed độc lập. Kết quả cuối đạt các mốc ổn định đã định nghĩa trong môi trường tổng hợp; không chứng minh khả năng điều khiển drone thật hoặc tránh mọi loại vật cản.

Model bàn giao dùng các resource `Models/campus-flight-s11` và `Models/campus-dispatch-s11` (có bản tương ứng seed 29, 47). Để xem AI trong Editor, đặt Mode thành `dispatch-eval`, Flight Model và Dispatch Model theo hai resource này trước khi Play. Chế độ mặc định vẫn ghi rõ **tham chiếu theo luật**.

Xem [bảng kết quả](docs/delivery-progress.md), [hướng dẫn tiếp tục/Colab](docs/delivery-continuation.md) và [tài liệu mô phỏng](docs/delivery-lab.md). Tài liệu cloud và các lần huấn luyện thất bại ban đầu được giữ dưới dạng lịch sử, phân biệt với kết quả cuối.
