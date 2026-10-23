# Reset và chỉnh tòa nhà trong DeliveryLab

Chức năng này sửa vị trí X/Z và chiều cao tòa nhà trên **nền phẳng**. Kho, bốn điểm giao, chiều rộng/sâu của nhà và luồng đơn hàng được giữ nguyên khi chỉnh bằng giao diện.

1. Mở DeliveryLab bằng Unity hoặc bản Linux, ở chế độ tham chiếu hay suy luận model đã lưu.
2. Bấm **Reset lượt (R)** để đưa drone về kho, pin đầy, xóa trạng thái giao hàng và bắt đầu lại cùng bố trí/luồng đơn. Chế độ AI cũng reset bộ nhớ hành động của cả hai agent. Sau reset, mô phỏng tạm dừng.
3. **Đổi bố trí tòa nhà** tạo lại vị trí, kích thước và chiều cao theo bộ sinh hiện có. Seed bố trí đổi riêng; seed và dữ liệu đơn không đổi.
4. **Chỉnh vị trí / chiều cao** mở bảng bên phải và tạm dừng. Chọn **Nhà 1–6**, dùng thanh trượt hoặc nhập X, Z, chiều cao theo mét; dấu chấm hoặc dấu phẩy thập phân đều được chấp nhận.
5. **Áp dụng và reset** cài bố trí mới vào cả cảm biến, kiểm tra va chạm và hình ảnh. **Hủy thay đổi đang nhập** bỏ phần nháp. **Khôi phục bố trí gốc** tạo lại bố trí theo seed đơn ban đầu.
6. Bấm **Bắt đầu / Tiếp tục** để thử. Lượt suy luận tương tác kết thúc sẽ giữ nguyên kết quả và tạm dừng để bạn xem, rồi reset khi cần.

Chiều cao cho phép nhập là 0,5–30 m. Toàn bộ nhà phải nằm trong sân 80 × 80 m; không chồng nhà hoặc che kho/điểm giao và khoảng tiếp cận tối thiểu 3,5 m theo mỗi trục. Kiểm tra này xác nhận bố trí hợp lệ về hình học, không bảo đảm AI có đường bay an toàn. Dữ liệu không hợp lệ được từ chối trước khi thay lượt đang chạy. Đổi nhà không tự huấn luyện lại model.

**Lưu bố trí / Nạp bố trí** dùng `artifacts/layouts/campus.json`, tính từ thư mục khởi chạy ứng dụng. File chỉ lưu hình học và seed bố trí; nạp bố trí không thay luồng đơn. Lưu lại sẽ thay file bố trí đã lưu trước đó. Có thể đặt đường dẫn khác bằng trường **Layout Storage Path** trong Inspector. Reset giữ bố trí tự chỉnh; muốn trở lại bản gốc phải bấm nút khôi phục. Trong Editor, bố trí áp dụng được giữ qua lần nạp lại assembly trong cùng Play Mode; dùng lưu file nếu muốn dùng lại ở phiên sau.

Chức năng chỉnh sửa chỉ mở ở chế độ tương tác `reference`, `flight-eval`, `dispatch-eval`, khi không có `--episodes`. Huấn luyện, ghi demonstration và đánh giá batch không nhận lệnh chỉnh/reset này. Báo cáo tương tác ghi `RunPurpose=interactive`, `Config.LayoutOrigin` và `Config.LayoutSeed`; không gộp chúng vào kết quả nghiên cứu.

Model đã nghiên cứu với nhà **dưới 24 m**, mặt đất phẳng và lối tiếp cận các bãi trống. Nhà cao hơn hoặc nằm sát đường hạ cánh có thể khiến model thất bại. Kết quả trong `docs/delivery-progress.md` thuộc bố trí sinh tự động và bản chạy được ghi trong báo cáo; không tự áp dụng cho bố trí bạn chỉnh. Phiên bản cấu hình hiện tại là `delivery-v3`, bổ sung nguồn/seed bố trí; kích thước đầu vào/hành động AI không đổi. Bundle nghiên cứu cũ vẫn được giữ nguyên. Khi tiếp tục checkpoint cũ, dùng bundle tương ứng thay vì bỏ qua kiểm tra phiên bản nguồn.

Kiểm tra tính năng:

```sh
bash tools/check-core.sh
AEROLAB_CHECK_PROJECT="$PWD/artifacts/unity-check" bash tools/check-delivery-unity.sh layout-smoke
```

Play Mode smoke dùng hai model `Models/campus-flight-s11` / `Models/campus-dispatch-s11`. Nó kiểm tra reset giữa chuyến và sau khi kết thúc, chỉnh hình học, từ chối dữ liệu lỗi, đổi bố trí giữ đơn, lưu/nạp/khôi phục và tiếp tục suy luận. File thử được ghi riêng trong `artifacts/layout-checks/`, không ghi đè bố trí người dùng.

Xác minh cục bộ ngày 23/10/2026: **35 kiểm thử core đạt**, kiểm tra Play Mode hai model đạt `AEROLAB_LAYOUT_PLAYMODE_PASS`; bản Linux `92ffc0b9689c429e9974ad35e9801ea2` build thành công và giao diện đã được xem ảnh thực tế. Một lượt batch seed 30000 giao–quay về thành công, có cùng kết quả, đơn, chặng, thời gian, quãng đường và năng lượng với bản nghiên cứu cũ; chỉ khác thông tin nguồn/phiên bản và các trường bố trí mới. Đây là kiểm tra hồi quy tính năng, không phải nghiệm thu lại AI trên bản đồ tự chỉnh. Bằng chứng ở `artifacts/layout-core-checks.log`, `artifacts/delivery-layout-smoke.log`, `artifacts/previews/layout-editor/preview.png` và `artifacts/evaluations/layout-editor-batch-regression/comparison.json`.
