# AeroLab — GameBuild-TrainAI

Game Unity 3D mô phỏng xe bay tự hành, hướng tới môi trường huấn luyện và đánh giá AI.

Nhiệm vụ: cất cánh từ bãi A, tránh các tòa nhà và hạ cánh an toàn tại bãi B trong 60 giây.

## Mở và chơi bằng Unity

1. Cài Unity Hub và **Unity 6 LTS — 6000.0.62f1**, kích hoạt giấy phép hợp lệ trong Hub. Đây là phiên bản dự án nhắm tới; Editor chưa chạy được trên cloud hiện tại.
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

Nếu máy chỉ có Unity Hub, cần cài thêm **Unity Editor 6000.0.62f1** trong mục Installs. Nếu script báo không tìm thấy Editor, kiểm tra mục này trước. Đường mở trực tiếp chưa được thử với Editor thật trên cloud.

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

Đã có mã nguồn bản lái tay, cảnh khởi động và chế độ bay mẫu. Core được kiểm thử trên cloud. Unity Editor và Play Mode chưa được kiểm chứng do thiếu Editor và truy cập tới máy chủ Unity đang bị proxy chặn. Chưa có ML-Agents, cầu nối Python, model học máy hoặc kết quả nghiệm thu mốc B. Xem báo cáo môi trường để phân biệt phần đã chạy và phần chưa chạy.
