# Theo dõi thay đổi mà không phải commit liên tục

Cứ sửa và thử nghiệm theo từng công việc. Working tree chưa sạch là trạng thái làm việc bình thường; không cần commit/push để được tiếp tục. Quy định cho trợ lý nằm trong [AGENTS.md](../AGENTS.md).

## Xem nhanh

Từ thư mục gốc dự án:

```sh
python3 tools/worktree-report.py --html
```

Mở [bảng thay đổi cục bộ](../artifacts/worktree/index.html) bằng trình duyệt. Bảng có số file theo nhóm, bộ lọc trạng thái, ô tìm đường dẫn và diff mở rộng cho từng file. Với file mới, bảng hiển thị nội dung hiện tại vì Git chưa có bản gốc. Diff/nội dung dài được rút gọn và có thông báo.

Chạy lại lệnh và tải lại trang khi muốn cập nhật; đây không phải màn hình tự làm mới. Bảng HTML nằm trong `artifacts/` đã được ignore, không tạo thêm file cần commit. Không cần cài thư viện hoặc truy cập mạng. Chỉ chạy `python3 tools/worktree-report.py` nếu muốn xem danh sách trong terminal.

| Trạng thái | Ý nghĩa |
| --- | --- |
| `??` | File mới, Git chưa theo dõi; chưa có lịch sử khôi phục trong Git |
| ` M` | File đã được theo dõi, có sửa nhưng chưa stage |
| `M ` / `A ` | Phần sửa / file thêm đã được stage cho commit kế tiếp |
| `MM` | Có phần đã stage và phần sửa thêm chưa stage |
| ` D` / `D ` | Xóa ở working tree / đã stage việc xóa |
| `R ` | Đổi tên đã stage |
| `UU` và các dạng xung đột | Cần giải quyết xung đột trước khi commit phần đó |

**Working tree** là file đang có trên đĩa. **Stage/index** là phần bạn chọn cho commit kế tiếp. **Commit** lưu một mốc lịch sử local. **Push** đưa các commit lên GitHub. Stage không phải commit; commit local chưa đồng nghĩa đã có trên GitHub.

## Khi muốn lưu một mốc

Commit khi hoàn thành một thay đổi có ý nghĩa hoặc trước một thử nghiệm lớn mà bạn muốn có mốc quay lại. Không có quy định số lần hay thời gian bắt buộc.

```sh
git diff -- path/to/file
git add -- path/to/file
git diff --cached
git commit -m "Mô tả thay đổi vừa hoàn thành"
```

Đây là ví dụ với đường dẫn cần thay bằng file thực tế. Chỉ chọn phần liên quan; có thể dùng `git add -p -- path/to/file` để chọn từng đoạn sửa trong file đã được theo dõi. Push khi muốn đồng bộ/chia sẻ và đã kiểm tra đúng nhánh/remote, không bắt buộc sau mỗi commit.

## Giới hạn cần biết

- Bảng xem so với trạng thái Git hiện tại, không tự biết file nào do tác vụ/lượt trò chuyện nào sửa. Phần trả lời sau tác vụ phải chỉ rõ phạm vi vừa làm.
- Báo cáo không sao lưu và không khôi phục được các phiên bản trước của file mới hoặc phần chưa commit. Muốn giữ mốc có thể quay lại, cần commit phù hợp hoặc một bản sao lưu riêng.
- Ahead/behind chỉ so với thông tin remote đã có trên máy, có thể cũ. Công cụ không tự fetch/pull và không giải quyết chênh lệch nhánh.
- Build, model, cache và kết quả trong `.gitignore` không được liệt kê. Công cụ không đọc nội dung file bị ignore; nội dung file nguồn chưa bị ignore có thể xuất hiện trong báo cáo, nên giữ báo cáo cục bộ.
