# Đặc tả phiên bản đầu: xe bay tự hành

Trạng thái: phạm vi cơ sở cho triển khai bằng Unity theo yêu cầu bắt buộc của người dùng. Đã có mã nguồn bản lái tay và core kiểm thử; mốc A/B chưa được nghiệm thu. Xem [báo cáo môi trường](environment-validation.md). Các thông số ghi là “khởi điểm” được phép điều chỉnh sau thử nghiệm, nhưng phải lưu lại cấu hình để tái lập kết quả.

## 1. Mục tiêu sản phẩm

Xây dựng một game mô phỏng 3D trong đó người chơi hoặc AI điều khiển một xe bay cất/hạ cánh thẳng đứng từ bãi A đến bãi B, tránh chướng ngại vật và hạ cánh an toàn.

Đây là môi trường nghiên cứu trong mô phỏng; đặc tả không đưa ra khả năng điều khiển phương tiện thật. Tên dự án và phương tiện không gắn với sản phẩm hay sự bảo trợ của Elon Musk hoặc Tesla.

Người sử dụng đầu tiên là người phát triển: lái thử để kiểm tra game, chạy huấn luyện và xem lại hành vi của AI.

## 2. Hai mốc bàn giao

### Mốc A: chơi được bằng tay

- Mở được một sân thử nghiệm 3D.
- Lái xe từ A tới B, hạ cánh, nhận kết quả và chơi lại.
- Va chạm, ra ngoài vùng và hết giờ được nhận diện đúng.
- Chuyển động và đặt lại lượt ổn định qua nhiều lượt liên tiếp.

### Mốc B: AI hoàn thành nhiệm vụ

- Huấn luyện được bằng Python với môi trường game.
- Nạp được model đã lưu để xem AI điều khiển xe.
- Có kết quả đánh giá riêng, tái lập được theo seed và cấu hình.
- Mục tiêu: ít nhất 90 lượt thành công trong bộ đánh giá 100 lượt đã định nghĩa trước.

Không coi việc kết nối Python hoặc chạy được một tiến trình là đã đạt mốc B.

## 3. Phạm vi và giới hạn

| Thành phần | Có trong phiên bản đầu |
| --- | --- |
| Phương tiện | Một xe bay dạng khối, một tác nhân |
| Chuyển động | Ba chiều; giữ hướng cố định, tự cân bằng |
| Vật lý | Gia tốc, giới hạn tốc độ, lực hãm, trọng lực và hỗ trợ giữ độ cao |
| Môi trường | Mặt đất phẳng, hai bãi đáp, tòa nhà dạng khối |
| Cảm biến AI | Trạng thái chuyển động, vị trí đích tương đối, tia đo khoảng cách |
| Chế độ | Lái tay, huấn luyện, xem model chạy |
| Giao diện | Thời gian còn lại, tốc độ, độ cao, kết quả lượt, chơi lại |
| Lưu kết quả | Cấu hình, seed, model, log huấn luyện và báo cáo đánh giá |

Chưa làm: hành khách, nhiệm vụ giao hàng, nhiều xe, kinh tế trong game, thời tiết, tiêu hao pin, khí động học thực tế, xoay hướng tự do, nhận diện bằng camera, bản đồ thành phố lớn và đồ họa chi tiết.

## 4. Sân thử nghiệm và thông số khởi điểm

Quy ước tọa độ thế giới: X/Z là mặt phẳng ngang; Y là độ cao. Một đơn vị tương ứng một mét.

| Thông số | Giá trị khởi điểm |
| --- | --- |
| Vùng bay | X và Z từ -40 đến 40 m; Y từ mặt đất đến 30 m |
| Kích thước xe | Khoảng 2 × 1 × 2 m |
| Bãi đáp | 6 × 6 m, cùng cao độ mặt đất |
| Khoảng cách A–B | 20–50 m |
| Vật cản | 3–5 khối nhà tĩnh trong bài thử cuối |
| Tốc độ ngang tối đa | 8 m/s |
| Tốc độ dọc tối đa | 3 m/s |
| Gia tốc ngang tối đa | 4 m/s² |
| Gia tốc dọc điều khiển tối đa | 3 m/s², sau bù trọng lực |
| Thời lượng một lượt | 60 giây mô phỏng |
| Bước vật lý / nhịp quyết định AI | 0,02 giây / 0,1 giây mô phỏng |

Vùng bay được kiểm tra theo toàn bộ collider của xe. Tiếp xúc mặt đất được xử lý theo luật hạ cánh, không tự động coi là vượt giới hạn độ cao.

Vị trí sinh xe phải đứng yên trên A, không chồng vật cản. Bãi A/B không chồng nhau hoặc chồng nhà. Các cấu hình sân phải có đường bay khả thi, đủ khoảng hở cho xe; giai đoạn đầu dùng các bố trí được kiểm tra trước thay vì sinh vật cản tùy ý.

## 5. Luồng một lượt chơi

1. Đặt lại xe, vận tốc, bộ đếm, cảm biến, trạng thái tiếp xúc và phần thưởng tích lũy.
2. Chọn bài tập và seed; đặt A, B và vật cản tương ứng.
3. Hiện trạng thái chờ. Khi bắt đầu, chuyển sang đang bay và tính giờ.
4. Người chơi hoặc AI gửi lệnh qua cùng một bộ điều khiển.
5. Khi đạt điều kiện kết thúc, chốt đúng một kết quả và ngừng nhận lệnh cho lượt đó.
6. Hiển thị kết quả; chơi lại tạo lượt mới. Huấn luyện tự đặt lại lượt.

### Điều kiện kết thúc

**Thành công** yêu cầu đồng thời:

- Toàn bộ đáy xe nằm trong vùng bãi B và có tiếp xúc với mặt bãi.
- Tốc độ ngang không quá 0,5 m/s, tốc độ dọc tuyệt đối không quá 0,5 m/s.
- Các điều kiện trên được duy trì liên tục 1 giây mô phỏng.

Bay ngang qua hoặc lơ lửng trên B không được tính thành công.

**Thất bại** được phân loại:

- `collision`: chạm tòa nhà; hoặc chạm mặt đất/bãi với tốc độ va chạm theo phương pháp tuyến lớn hơn 1 m/s.
- `out_of_bounds`: bất kỳ phần nào của xe vượt biên ngang hoặc trần vùng bay.
- `timeout`: hết 60 giây mà chưa hoàn thành.

Tiếp đất nhẹ ngoài B được phép, nhưng không hoàn thành nhiệm vụ. Va chạm/tốc độ va chạm phải dùng trạng thái ngay trước khi xử lý tiếp xúc, tránh đọc vận tốc đã bị bộ giải vật lý làm giảm.

Nếu có nhiều điều kiện trong cùng bước, ưu tiên `collision`, rồi `out_of_bounds`, rồi thành công, rồi `timeout`. Sau khi chốt kết quả, không cộng thêm thưởng hoặc ghi nhận kết quả lần hai.

## 6. Điều khiển tay và AI

### Điều khiển tay

- W/S: tiến/lùi theo trục Z.
- A/D: trái/phải theo trục X.
- Space/Ctrl: lên/xuống theo trục Y.
- R: bắt đầu lượt mới.

Khi không có lệnh, xe hãm chuyển động ngang và giữ độ cao ở mức đơn giản. Lệnh được chuẩn hóa để nhấn chéo không tạo gia tốc ngang lớn hơn giới hạn.

### Giao diện tác nhân

Hành động gồm ba giá trị liên tục trong [-1, 1] cho điều khiển X, Y, Z. Người chơi và AI dùng chung giới hạn gia tốc, tốc độ và cơ chế giữ độ cao.

Quan sát dự kiến:

- Vectơ từ xe đến tâm bãi B, chuẩn hóa theo kích thước sân.
- Vận tốc X/Y/Z, chuẩn hóa theo giới hạn tốc độ.
- Khoảng cách tới mặt đất và trạng thái tiếp đất.
- Khoảng cách tới chướng ngại vật, mặt đất và biên vùng bay bằng tia cảm biến theo các hướng cố định, gồm phía trên và phía dưới.

Cấu hình số tia, tầm đo, thứ tự và kích thước vectơ quan sát phải được cố định trước lần huấn luyện đầu tiên. Không dùng ảnh camera trong phiên bản này.

## 7. Phần thưởng và lộ trình học

Phần thưởng cơ sở gồm:

- Tiến gần tâm bãi B: thưởng theo chênh lệch khoảng cách giữa hai quyết định liên tiếp.
- Thành công: thưởng kết thúc lớn.
- Va chạm hoặc ra ngoài vùng: phạt kết thúc lớn.
- Thời gian: phạt nhỏ theo thời gian mô phỏng, độc lập tốc độ chạy máy.

Các trọng số sẽ được chọn và lưu trong cấu hình khi triển khai; chưa có giá trị nào đã được kiểm nghiệm. Phần thưởng khoảng cách phải có dấu, không cắt bỏ phần âm, để không khuyến khích bay ra xa rồi quay lại kiếm điểm. Đánh giá chất lượng theo kết quả nhiệm vụ, không chỉ tổng thưởng.

Thứ tự bài tập:

1. Không vật cản, A/B cố định: học cất cánh, tiếp cận và hạ cánh.
2. Không vật cản, A/B thay đổi trong phạm vi hợp lệ.
3. Có vật cản, dùng các bố trí hợp lệ và thay đổi A/B.

PPO là thuật toán khởi đầu dự kiến. Chỉ tăng độ khó sau khi kiểm tra được hành vi và kết quả của bài trước; chưa đặt cam kết thời gian hoặc số bước huấn luyện.

## 8. Nghiệm thu

### Mốc A

- Hoàn thành bằng tay ít nhất 3 lượt liên tiếp trên sân cố định.
- Kiểm tra riêng: lướt qua B, lơ lửng trên B, tiếp đất ngoài B, tiếp đất mạnh, đâm nhà, vượt biên và hết giờ.
- Chơi lại không giữ vận tốc, thời gian, phần thưởng hoặc trạng thái tiếp xúc của lượt trước.

### Mốc B

- Đóng băng model và cấu hình trước khi chạy đánh giá.
- Dùng 100 seed đánh giá cố định, tách khỏi seed huấn luyện; lưu cả bố trí sinh ra và phiên bản môi trường.
- Bộ thử cuối có vật cản và A/B thay đổi trong phạm vi đã huấn luyện. Bộ seed dùng để điều chỉnh model là bộ kiểm định riêng; không dùng bộ thử cuối để liên tục chọn model.
- Mục tiêu: thành công ít nhất 90/100 lượt. Báo đủ số thành công, va chạm, vượt biên và hết giờ; tổng phải bằng 100.
- Báo thời gian hoàn thành trung bình trên các lượt thành công, kèm số lượng lượt đó.
- Model nạp lại trong chế độ xem phải điều khiển được xe; có hướng dẫn chạy lại bài đánh giá.

Kết quả này chỉ áp dụng cho sân và phạm vi đã thử, không chứng minh khả năng lái trong thành phố bất kỳ.

## 9. Bàn giao và thứ tự tiếp theo

Các đầu ra cần có khi hoàn tất phiên bản đầu: dự án game, bản chạy phù hợp môi trường, cấu hình huấn luyện/đánh giá, model mẫu, báo cáo 100 lượt và hướng dẫn tái lập.

Bước tiếp theo là thực hiện [kế hoạch kiểm tra công nghệ](step-2-environment-plan.md). Chưa tạo dự án Unity hoặc tải bộ công cụ lớn cho đến khi xác minh được khả năng chạy và yêu cầu cấp phép.
