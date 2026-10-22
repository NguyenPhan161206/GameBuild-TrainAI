# Bước 2: kế hoạch kiểm tra công nghệ và môi trường

Trạng thái: đã kiểm kê cloud và triển khai source Unity; cài Editor bị chặn truy cập mạng, Play Mode/ML-Agents/huấn luyện chưa được xác minh. Xem [bằng chứng và phần còn thiếu](environment-validation.md). Kế hoạch bên dưới vẫn là điều kiện nghiệm thu đầy đủ cho bước 2; kiểm thử core C# độc lập chưa hoàn thành toàn bộ bước này.

## Mục tiêu và điều kiện kết thúc

Quyết định có dùng Unity + ML-Agents + Python cho [đặc tả V1](v1-spec.md) hay không, dựa trên một thử nghiệm nhỏ chạy thực tế.

Kết thúc bước 2 cần có bằng chứng cho toàn bộ chuỗi: build môi trường mẫu → chạy không giao diện → Python nhận quan sát/gửi hành động → huấn luyện ngắn → lưu và nạp lại model → chạy chế độ suy luận trong game.

Thử nghiệm này kiểm tra đường đi của công nghệ; chưa cần tạo xe bay hoặc đạt tỷ lệ thành công của V1.

## Nguyên tắc làm việc

- Dùng checkout hiện có trong /workspace; mỗi cloud task đã được cô lập. Không tạo Git worktree trừ khi người dùng yêu cầu.
- Đọc hướng dẫn repository và kiểm tra thay đổi hiện có trước khi thực hiện.
- Dùng vùng thử nghiệm ngoài mã nguồn chính, chẳng hạn /workspace/setup-probes/gamebuild-trainai; kiểm tra thư mục chưa có dữ liệu trước khi tạo file.
- Không ghi token, thông tin tài khoản hoặc nội dung giấy phép vào log, Git hay hướng dẫn tái sử dụng.
- Giữ xác minh TLS, chữ ký và checksum khi tải công cụ; không tắt kiểm tra để vượt lỗi cài đặt.
- Chỉ tải bộ cài lớn sau các kiểm tra tương thích, tài nguyên, mạng và khả năng cấp phép.

## 2.1. Kiểm kê máy và checkout

### Thực hiện

1. Ghi hệ điều hành, kiến trúc CPU, số lõi, RAM và dung lượng đĩa trống.
2. Kiểm tra giới hạn CPU/RAM thực tế của container nếu có, không chỉ số phần cứng host.
3. Kiểm tra GPU và driver bằng công cụ có sẵn; không mặc định có GPU.
4. Kiểm tra phiên bản Python, Git, các trình quản lý môi trường Python và Unity/Unity Hub nếu đã có.
5. Kiểm tra khả năng tạo tiến trình, chạy file thực thi và mở cổng loopback phục vụ Unity–Python.
6. Ghi trạng thái Git, các file hiện có và nơi đặt công cụ/cache được phép ghi.

### Đầu ra

Bảng `hạng mục / quan sát thực tế / phù hợp hay thiếu / hành động`. Chỉ ghi tên và trạng thái có/không của biến môi trường liên quan, không dump môi trường hoặc file credential.

### Qua bước khi

Biết rõ máy thuộc nền tảng nào, có bao nhiêu tài nguyên dùng được và công cụ nào có thể tái sử dụng. Thiếu GPU không tự động chặn thử nghiệm nhỏ bằng CPU.

## 2.2. Xác định vai trò Editor và cloud

### Thực hiện

1. Phân biệt ba nhu cầu: chỉnh cảnh bằng Editor, build bằng Editor ở batch mode, chạy bản game headless để huấn luyện.
2. Kiểm tra cloud có hỗ trợ Editor trên hệ điều hành/kiến trúc đó hay không; có môi trường đồ họa hay chỉ dòng lệnh.
3. Kiểm tra cách kích hoạt giấy phép hợp lệ theo tài liệu Unity hiện hành, bao gồm khả năng dùng Editor không tương tác trong môi trường này.
4. Kiểm tra quyền và thông tin kích hoạt đã có trước khi yêu cầu thêm. Giấy phép Editor và khả năng chạy bản game đã build phải được đánh giá riêng.
5. Nếu cloud không thể chạy Editor, xác định có thể chỉnh/build trên máy phát triển rồi chuyển bản Linux tương thích lên cloud hay không. Chỉ hỏi thông tin máy cá nhân khi nhánh này thực sự cần.

### Đầu ra

Một trong ba kết luận: toàn bộ quy trình trên cloud; Editor/build ngoài cloud và train trên cloud; hoặc Unity chưa khả thi. Ghi rõ thao tác nào còn phụ thuộc người dùng.

### Điểm quyết định A

Nếu không có đường cấp phép/build hợp lệ, dừng việc cài Unity Editor trên cloud và hoàn thành các kiểm tra độc lập. Không tìm cách bỏ qua kích hoạt.

## 2.3. Chọn và khóa bộ phiên bản tương thích

### Thực hiện

1. Đối chiếu tài liệu và release chính thức của Unity ML-Agents.
2. Chọn một Unity LTS được bản ML-Agents đó hỗ trợ; không chọn tất cả gói theo nhãn “mới nhất”.
3. Ghi phiên bản Editor đầy đủ và Linux build support/module cần thiết.
4. Ghép đúng phiên bản package Unity `com.unity.ml-agents`, package Python `mlagents`/`mlagents-envs`, Python và PyTorch theo bộ tương thích được công bố.
5. Kiểm tra wheel Python/PyTorch có cho hệ điều hành và kiến trúc của máy. Bắt đầu với CPU nếu chưa có GPU hoạt động và nhu cầu thử nghiệm nhỏ.
6. Xác định cách khóa phụ thuộc phù hợp; không sửa checksum hay nâng phụ thuộc tùy tiện khi gặp xung đột.

### Đầu ra

Bảng phiên bản chính xác, URL tài liệu/release, module nền tảng và lý do lựa chọn. Mọi mục chưa xác minh được đánh dấu rõ, không điền phiên bản phỏng đoán.

### Qua bước khi

Có một bộ phiên bản được hỗ trợ và có đường cài đặt cho máy hiện tại.

## 2.4. Kiểm tra mạng, xác thực và dung lượng trước cài

### Thực hiện

1. Đọc cấu hình mạng và yêu cầu credential hiện có nếu công cụ cung cấp.
2. Kiểm tra đọc GitHub qua HTTPS proxy có sẵn; không yêu cầu PAT chỉ vì thiếu GH_TOKEN.
3. Kiểm tra các đích thực sự cần từ bộ phiên bản đã chọn: bộ cài Unity, Unity Package Manager, GitHub, PyPI và nguồn wheel PyTorch.
4. Theo dõi đích redirect cần thiết; phân biệt lỗi DNS, bị proxy chặn, chứng chỉ, quyền truy cập và lỗi phiên bản.
5. Chỉ đề xuất thêm hostname đã xác định là cần; giữ nguyên các allowlist khác. Yêu cầu credential qua environment settings nếu thực sự thiếu, không qua chat.
6. Ước tính dung lượng download, giải nén, Editor, build và cache dựa trên artifact đã chọn; đối chiếu đĩa trống trước khi tải.

### Đầu ra

Danh sách nguồn tải truy cập được, đích còn bị chặn, dung lượng cần và cách xử lý từng thiếu sót. Không cài nếu chưa bảo đảm tính toàn vẹn hoặc không đủ dung lượng.

## 2.5. Cài bộ thử nghiệm tối thiểu

Phụ thuộc: hoàn tất 2.1–2.4 cho nhánh công nghệ đã chọn.

### Thực hiện

1. Tái sử dụng bộ công cụ đúng phiên bản đã cài nếu kiểm tra hợp lệ.
2. Cài Editor và module build chỉ khi cần và được môi trường hỗ trợ.
3. Tạo Python virtual environment riêng ở vị trí được phép ghi.
4. Cài bộ phụ thuộc đã khóa, kiểm tra xung đột bằng trình quản lý gói.
5. Xác minh import ML-Agents/PyTorch, lệnh huấn luyện và thông tin phiên bản.
6. Nếu dự định dùng GPU, chạy phép tính nhỏ và xác minh thiết bị thực tế; không chỉ kiểm tra tên driver.

### Đầu ra

Lệnh cài đầy đủ, thư mục thực thi, phiên bản thực tế, log cài đặt đã lọc thông tin nhạy cảm và trạng thái phụ thuộc.

## 2.6. Chạy thử Unity không giao diện

### Thực hiện

1. Dùng ví dụ nhỏ từ đúng release ML-Agents hoặc tạo cảnh probe ngoài dự án chính. Nếu dùng ví dụ chính thức, giữ nguyên task và tiêu chí đánh giá của ví dụ.
2. Xác minh Editor mở/import được cảnh ở batch mode và kết thúc với mã thành công nếu nhánh cloud có Editor.
3. Build executable Linux tương thích theo hướng dẫn của bản đã chọn; ghi log và mã thoát. Nhánh build ngoài cloud phải nhận và kiểm tra artifact thực tế.
4. Chạy executable ở chế độ không đồ họa với một instance, một môi trường.
5. Kiểm tra log khởi tạo vật lý, agent và môi trường; không coi PID tồn tại là bằng chứng hoạt động.

### Qua bước khi

Bản build hoạt động trên cloud, không gặp lỗi thư viện nền tảng/hiển thị bắt buộc, và sẵn sàng kết nối Python. Không cần camera trong probe này.

## 2.7. Kiểm tra trao đổi Unity–Python

### Thực hiện

1. Khởi chạy bằng API chính thức của phiên bản `mlagents-envs` đã chọn, dùng cổng loopback còn trống.
2. Kiểm tra handshake, tên behavior, kích thước và kiểu dữ liệu quan sát/hành động.
3. Reset môi trường, gửi hành động hợp lệ và tiến ít nhất 200 bước có quyết định tác nhân.
4. Xác nhận trạng thái quan sát thay đổi theo hành động; giá trị số hữu hạn, không NaN/Inf.
5. Chạy đến ít nhất một lượt kết thúc tự nhiên và xác minh có thể bắt đầu lượt mới. Reset cưỡng bức không thay thế kiểm tra kết thúc lượt.
6. Đóng và khởi động lại tiến trình do mình tạo; kiểm tra không còn chiếm cổng hoặc treo tiến trình.

### Qua bước khi

Trao đổi hai chiều hoạt động, có chuyển trạng thái và kết thúc/đặt lại lượt thực tế. Lưu số bước, số lượt và mã thoát.

## 2.8. Thử đường huấn luyện và suy luận

### Thực hiện

1. Dùng cấu hình PPO mẫu tương ứng ví dụ và bộ phiên bản đã chọn.
2. Chạy một job ngắn, một môi trường, trên CPU trước; đặt ngân sách ban đầu tối đa 10 phút và giới hạn bước cụ thể trong cấu hình sau khi đo tốc độ probe.
3. Xác nhận có episode, reward và cập nhật policy thực tế trong log; không coi file cấu hình được đọc là đã huấn luyện.
4. Dừng đúng cách theo tài liệu runner; kiểm tra checkpoint và model suy luận sinh từ lần chạy hiện tại.
5. Nạp lại checkpoint để xác minh tiếp tục huấn luyện được.
6. Nạp model đã xuất vào cảnh thử và chạy suy luận bằng đường dự kiến dùng trong game; ghi rõ thao tác Editor/build bổ sung nếu cần.
7. Đo bước mô phỏng/giây, RAM cao nhất và dung lượng đầu ra; dùng số đo để đề xuất số môi trường song song cho V1.

### Qua bước khi

Có bằng chứng job thực sự cập nhật policy, lưu/nạp lại được checkpoint và model chạy suy luận. Không yêu cầu probe ngắn đạt mức thành thạo và không báo đây là model xe bay.

Nếu job hết ngân sách mà chưa đủ bằng chứng, ghi là chưa xác minh và chẩn đoán. Chỉ tăng ngân sách khi có lý do và tài nguyên phù hợp.

## 2.9. Ghi kết quả, lưu cấu hình và quyết định công nghệ

### Đầu ra bàn giao

- `docs/environment-validation.md`: phần cứng, phần mềm, bộ phiên bản, nguồn chính thức, lệnh kiểm tra và kết quả thực tế.
- Cấu hình probe và cách tái lập, cùng nơi lưu log/model ngoài Git nếu file lớn.
- Danh sách kiểm tra: đã qua, thất bại có nguyên nhân, bị chặn và chưa chạy.
- Quyết định công nghệ và giới hạn còn lại.
- Trong luồng onboarding, lưu `install_script` khi cần tái tạo cài đặt và `start_skill` khi cần kích hoạt môi trường/chạy tiến trình. Chỉ lưu lệnh đã thực hiện thành công, có thư mục và kiểm tra readiness; không phụ thuộc tiến trình còn sống sau snapshot.
- Kiểm tra Git trước/sau để tách file tài liệu có chủ đích khỏi cache và đầu ra không nên đưa vào repository.

Lưu cấu hình draft không thực thi lệnh, không áp dụng thay đổi runtime và không tự publish. Ghi rõ trạng thái từng phần, không tuyên bố đã xác minh máy mới nếu chỉ thử trên máy hiện tại.

### Điểm quyết định B

| Kết quả | Bước tiếp theo |
| --- | --- |
| Toàn chuỗi chạy trên cloud | Chốt phiên bản; sang bước 3 tạo dự án Unity chính |
| Editor/build ngoài cloud, train trên cloud đã kiểm chứng | Ghi rõ quy trình chuyển artifact; sang bước 3 khi luồng này tái lập được |
| Thiếu quyền, giấy phép hoặc đích mạng | Hoàn tất kiểm tra độc lập; báo thao tác cụ thể cần người dùng thực hiện rồi thử lại phần bị chặn |
| Unity không khả thi trên hạ tầng hiện có | So sánh một phương án như Godot + Python hoặc mô phỏng Python trước; thống nhất thay đổi trước khi xây game |

## Thứ tự thực hiện và kiểm soát phạm vi

Thực hiện 2.1 → 2.2 → 2.3 → 2.4 → 2.5 → 2.6 → 2.7 → 2.8 → 2.9. Có thể tra cứu tương thích và kiểm tra nguồn tải độc lập sau khi biết nền tảng, nhưng không chạy cài đặt lớn trước các điểm quyết định tương ứng.

Bước 2 không xây bản đồ xe bay, không viết bộ điều khiển V1, không mở rộng nhiều môi trường song song hoặc huấn luyện dài. Các phần đó bắt đầu sau khi đường công nghệ đã được chứng minh.
