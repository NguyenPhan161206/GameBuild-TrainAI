# Cách làm việc trong dự án

## Git nhẹ nhàng, thay đổi dễ theo dõi

- Có thể sửa, thử nghiệm và hoàn thành công việc khi working tree chưa sạch. Không bắt buộc commit sau mỗi lần sửa, mỗi lượt trò chuyện, hay trước khi chạy kiểm tra.
- Commit là mốc lưu công việc có ý nghĩa; push là bước đồng bộ lên GitHub. Không xem commit/push là điều kiện để một tác vụ cục bộ được hoàn thành.
- Thực hiện commit/push khi người dùng yêu cầu hoặc đã cho phép trong phạm vi công việc; không hỏi lại nếu quyền đó đã rõ. Nếu chưa có yêu cầu thì tiếp tục làm việc cục bộ, không liên tục nhắc xin commit/push.
- Trước khi sửa, xem `git status --short --branch` và diff liên quan để nhận biết thay đổi đã có. Giữ nguyên công việc có sẵn; không coi mọi file đang thay đổi là do lượt làm việc hiện tại tạo ra.
- Chỉ stage các file/hunk thuộc phạm vi được giao khi cần commit. Không tự gom toàn bộ working tree bằng `git add .` hoặc `git add -A`.
- Không tự reset, clean, discard, stash, amend, rebase, force-push hoặc kéo thay đổi từ remote để làm sạch/sắp xếp lại công việc đang có. Những thao tác này cần nằm trong yêu cầu hoặc quyền đã được người dùng cấp.
- Không tạo hook, quy tắc nhánh hoặc kiểm tra bắt buộc mới chỉ để ép commit/push. Không đổi cấu hình Git toàn máy cho quy trình này.

## Báo cáo thay đổi

- Sau mỗi tác vụ sửa đáng kể, chạy `python3 tools/worktree-report.py --html` để cập nhật bảng xem cục bộ trong `artifacts/worktree/index.html`.
- Khi trả lời, mô tả ngắn: thay đổi gì và vì sao; file/nhóm chính vừa sửa; đã kiểm tra gì và kết quả; phần còn dở nếu có. Dẫn tới bảng xem khi hữu ích, không chép toàn bộ danh sách file.
- Phân biệt **thay đổi của tác vụ vừa làm** với **toàn bộ thay đổi đang có so với Git**. Bảng xem không tự biết tác giả hoặc lượt làm việc nào tạo ra một thay đổi.
- Bảng xem là ảnh chụp trạng thái lúc chạy lệnh, không phải bản sao lưu và không tự lưu lịch sử nội dung. File chưa được Git theo dõi chưa có bản gốc trong Git để so sánh/khôi phục.
- Kiểm tra vừa đủ theo phạm vi: tài liệu/cấu hình đơn giản thì kiểm tra diff và cú pháp/liên kết liên quan; mã hành vi thì chạy kiểm tra phù hợp. Không ép chạy lại toàn bộ Unity/training khi chỉ sửa tài liệu hoặc công cụ xem Git.

## Đặc điểm dự án cần giữ

- Mở rộng dự án Unity hiện tại; không thay thế hàng loạt cấu trúc, mã hoặc cấu hình ngoài phạm vi tác vụ.
- Giữ file `.meta` đi cùng tài nguyên Unity khi thêm, chuyển hoặc xóa tài nguyên.
- Build, cache, log, model và kết quả huấn luyện sinh tự động tiếp tục nằm ngoài Git theo `.gitignore`. Không đưa dữ liệu nhạy cảm vào báo cáo hoặc commit.
- Phân biệt kết quả kiểm tra core, Unity, chất lượng AI bay và logistics nhiều đơn. Chỉ báo cáo mức đã có bằng chứng; mô phỏng không chứng minh hoạt động drone thật.
