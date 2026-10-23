# Bản ghi trao đổi về dự án AeroLab — 23/10/2026

Phạm vi: các câu hỏi/yêu cầu và câu trả lời trong cuộc trò chuyện hiện tại, theo thứ tự xuất hiện, đến yêu cầu lưu bản ghi này. Giữ nguyên cách viết của người dùng và nội dung trả lời; đường dẫn file nội bộ được đổi thành liên kết tương đối để đọc trong repository. Không chép log công cụ hoặc metadata hệ thống.

Đây là bản ghi lịch sử trao đổi, không phải kết quả chạy kiểm thử mới. Những nhận định về trạng thái working tree và bằng chứng phản ánh thời điểm từng câu trả lời được đưa ra. Các giải thích bổ sung hoặc điều chỉnh ở lượt sau được giữ nguyên cùng với câu trả lời trước đó.

## Lượt 1 — Thuật toán lập lịch đang sử dụng

### Người dùng

> ở dự án này, đang sử dụng thuật toán lập lịch gì?

### Cập nhật của trợ lý trong lúc kiểm tra

Mình sẽ kiểm tra mã điều phối và cấu hình hiện tại để xác định thuật toán lập lịch đang dùng, rồi phân biệt với phần AI điều khiển bay.

### Trợ lý trả lời

Dự án hiện có **3 cách lập lịch giao hàng**, tùy chế độ chạy:

| Cách lập lịch | Nguyên tắc | Vai trò |
|---|---|---|
| **EDD — Earliest Due Date** (`EarliestDeadline`) | Ưu tiên đơn có hạn giao sớm nhất | **Mặc định** khi chạy chế độ tham chiếu |
| **FIFO — First In, First Out** | Ưu tiên đơn xuất hiện trước | Làm mốc so sánh |
| **Học tăng cường bằng PPO — Proximal Policy Optimization** | Model học cách chọn đơn tiếp theo hoặc chờ/sạc dựa trên trạng thái hiện tại | Điều phối AI trong chế độ `dispatch-eval` |

Mình đã đối chiếu trực tiếp [cấu hình mặc định trong DeliveryLab.cs](../Assets/AeroLab/Runtime/DeliveryLab.cs), [hàm chọn đơn theo EDD/FIFO](../Assets/AeroLab/Core/DeliverySimulation.cs) và [cấu hình huấn luyện PPO](../training/dispatch.yaml).

Cụ thể, **AI lập lịch quyết định “giao đơn nào tiếp theo”**: nó xem pin, thời gian, hạn giao, thời gian chờ, khối lượng, vị trí đích và năng lượng ước tính. Mỗi quyết định có **9 lựa chọn: 8 đơn đang chờ đầu tiên và 1 lựa chọn chờ/sạc**; đơn không đủ điều kiện năng lượng bị khóa.

Đây là lập lịch tuần tự cho **một drone**, chọn một đơn rồi giao và quay về kho. Phần AI điều khiển bay là tầng riêng. Vì vậy, nếu bạn hỏi **thuật toán điều phối AI của dự án**, câu trả lời là **PPO**; còn **chế độ tham chiếu mặc định dùng EDD**.

## Lượt 2 — Kiến trúc dự án và cây thư mục

### Người dùng

> tôi cần bản tổng hợp chi tiết về kiến trúc dự án và kiến trúc cây thư mục

### Các cập nhật của trợ lý trong lúc thực hiện

Mình sẽ rà soát mã nguồn, cấu hình, công cụ và tài liệu hiện có để viết bản tổng hợp kiến trúc cùng cây thư mục, trong đó làm rõ luồng chạy, hai tầng AI và vai trò của từng nhóm file.

Cấu trúc chính đã rõ: dự án tách lõi mô phỏng C# khỏi Unity, dùng hai tầng AI bay và điều phối, kèm bộ công cụ Python cho huấn luyện và đánh giá. Mình cũng thấy các thay đổi cục bộ đang bổ sung chỉnh sửa bố trí tòa nhà; bản tổng hợp sẽ ghi riêng phần này để tránh nhầm với mã và model đã được kiểm thử trước đó.

Có hai điểm cần ghi rõ trong tài liệu: A* chỉ phục vụ tuyến tham chiếu và ước tính năng lượng, còn PPO điều phối chọn đơn hàng; cấu hình nghiên cứu hiện tại dùng validation seed 11000–11099 và test seed 30000–30099, khác một số hướng dẫn cũ. Mình đang lưu bản tổng hợp vào `docs/project-architecture.md`, kèm sơ đồ phụ thuộc và cây thư mục có chú giải.

### Trợ lý trả lời

Mình đã viết bản tổng hợp chi tiết tại **[docs/project-architecture.md](project-architecture.md)** và thêm liên kết vào README.

Tài liệu gồm **14 phần**, mô tả kiến trúc thực tế từ mã nguồn, sơ đồ phụ thuộc, luồng chạy, hai tầng AI, quy trình huấn luyện–đánh giá và cây thư mục có chú giải.

**Kiến trúc tổng thể**

Dự án AeroLab kết hợp **Unity 3D, lõi mô phỏng C# và công cụ nghiên cứu Python**. Thành phần trung tâm là lõi C# dùng chung: Unity, bộ kiểm thử và công cụ chạy ngoài Unity đều sử dụng lõi này.

| Thành phần | Trách nhiệm |
|---|---|
| **Core** | Vật lý bay, cảm biến, đơn hàng, pin, trạng thái nhiệm vụ, lập tuyến và báo cáo |
| **Runtime** | Vòng đời Unity, dựng cảnh, giao diện, nhập liệu và kết nối agent |
| **Editor** | Menu mở cảnh, validation, Play Mode smoke test và build |
| **Training** | Cấu hình PPO, học có giám sát, DAgger và quản lý thí nghiệm |
| **Tools** | Kiểm thử, checkpoint, khôi phục, đánh giá, đóng gói và tổng hợp kết quả |

Có hai môi trường cùng tồn tại:

- **FlightLab:** sân bay A–B để lái tay, chạy điều khiển mẫu và kiểm tra hồi quy.
- **DeliveryLab:** một drone, một kho/sạc, bốn điểm giao; xử lý đơn hàng, pin, tải và hạn giao.

**Hai tầng AI**

| Tầng | Câu hỏi cần giải quyết | Đầu vào → đầu ra |
|---|---|---|
| **AI bay** | Điều khiển drone đến đích thế nào? | 26 giá trị quan sát → 3 lệnh bay liên tục |
| **AI điều phối** | Giao đơn nào tiếp theo? | 60 giá trị quan sát → chọn một trong 8 đơn hoặc chờ/sạc |

Tầng bay quyết định mỗi **0,1 giây**, còn điều phối quyết định mỗi **1 giây khi drone ở kho**. Khi huấn luyện điều phối, trọng số model bay được giữ cố định.

Dự án còn có **FIFO và EDD** để chọn đơn theo luật; **A*** dùng tìm tuyến tham chiếu và hỗ trợ ước tính năng lượng. Chế độ tham chiếu mặc định dùng EDD.

**Cây thư mục chính**

```text
GameBuild-TrainAI/
├── Assets/AeroLab/
│   ├── Core/                  # Lõi C# độc lập UnityEngine
│   │   ├── FlightSimulation.cs
│   │   ├── ScenarioConfig.cs
│   │   ├── DeliverySimulation.cs
│   │   ├── RoutePlanner.cs
│   │   ├── DemoPilot.cs
│   │   ├── ClearancePilot.cs
│   │   ├── DeliveryDiagnostics.cs
│   │   ├── DeliveryReportJson.cs
│   │   └── CampusLayout.cs
│   ├── Runtime/               # Unity, agent và giao diện
│   │   ├── DeliveryLab.cs
│   │   ├── DeliveryLayoutControls.cs
│   │   ├── DeliveryFlightAgent.cs
│   │   ├── DeliveryDispatchAgent.cs
│   │   ├── FlightLab.cs
│   │   └── ...                # World, camera, HUD và smoke probe
│   ├── Editor/                # Kiểm tra và build trong Editor
│   ├── Scenes/                # FlightLab và DeliveryLab
│   └── Resources/             # Font, material, model ONNX
├── Packages/                  # Manifest và dependency lock
├── ProjectSettings/           # Cấu hình Unity
├── training/
│   ├── flight.yaml
│   ├── dispatch.yaml
│   └── studies/               # Cấu hình từng đợt nghiên cứu
├── tools/
│   ├── CoreChecks/            # Kiểm thử lõi C# dùng trong game
│   ├── DeliveryRunner/        # Chạy lõi ngoài Unity, bridge Python
│   ├── tests/                 # Kiểm thử workflow Python
│   └── ...                    # Training, evaluation, backup, report
├── docs/                      # Đặc tả, kiến trúc và bằng chứng
├── notebooks/                 # Notebook Colab
├── Builds/                    # Binary sinh ra, ngoài Git
├── results/                   # Checkpoint và run metadata, ngoài Git
└── artifacts/                 # Báo cáo, trace, cache, log, ngoài Git
```

Bản đầy đủ giải thích vai trò từng file và công cụ, kèm các luồng:

```text
Kịch bản → mô phỏng → quan sát → quyết định → hành động → báo cáo

Dữ liệu teacher → học có giám sát → DAgger → PPO
→ đóng băng ONNX → Unity build → đánh giá → tổng hợp bằng chứng
```

Tài liệu cũng ghi riêng **phần chỉnh bố trí tòa nhà đang thay đổi cục bộ**, phân biệt với model/build và kết quả nghiên cứu đã lưu.

Trong tác vụ này mình chỉ tạo tài liệu và thêm liên kết README. Đã kiểm tra diff, liên kết nội bộ và cấu trúc Markdown; không chạy lại Unity hoặc training. [Bảng thay đổi cục bộ đã cập nhật](../artifacts/worktree/index.html) hiển thị cả các thay đổi có sẵn của dự án.

## Lượt 3 — Cảm biến, địa hình, độ cao và né vật cản

### Người dùng

> sao có thể chắc chắn được view của drone chỉ có tính hiệu radar?
>  ,tôi có thể lấy và sử dụng được địa hình có sẵn trong game tinh vi hơn địa hình hiện tại thế nào ?, tôi có thể giảm độ cao bay của drone thể nào, tôi có thể thêm vào tính năng dùng AI để tính ra độ cao bay tối ưu và kẻm né tránh vật thể ở mức tinh vi hơn hay không?

### Các cập nhật của trợ lý trong lúc kiểm tra

Mình sẽ kiểm tra chính xác dữ liệu mà agent bay nhận và cách mã hiện tại chọn độ cao. Từ đó mình sẽ giải thích cách kiểm chứng đầu vào “không camera”, cách dùng địa hình phức tạp hơn và những phần cần đổi để AI học bay thấp, né vật cản tốt hơn.

Mình đã kiểm tra model `campus-flight-s11.onnx`: đầu vào duy nhất là vector 26 số, không có đầu vào ảnh. Nhưng “chỉ nhận tín hiệu radar” chưa chính xác: vector còn chứa hướng đến đích, vận tốc, độ cao, pin và trạng thái nhiệm vụ. Mình cũng xác nhận bộ điều khiển mẫu dùng để dạy AI có thiên hướng bay khoảng 26 m; vì vậy, bay thành công hiện tại chưa chứng minh AI đã biết chọn độ cao tối ưu hoặc dựa nhiều vào tia đo để né vật cản.

### Trợ lý trả lời

**Có thể dùng địa hình phức tạp hơn, cho drone bay thấp và học chọn độ cao kết hợp né vật cản.** Tuy nhiên, khả năng hiện tại còn đơn giản hơn điều đó. Mình cần làm rõ trước: **“không dùng camera” là đúng với đầu vào model đã kiểm tra; “chỉ dùng radar” thì chưa chính xác.**

**1. Làm sao biết AI thực sự nhận những gì?**

Mình đã kiểm tra hai nơi:

- Trong [DeliveryFlightAgent.cs](../Assets/AeroLab/Runtime/DeliveryFlightAgent.cs), agent lấy dữ liệu từ `ObserveFlight()`.
- Trong model phát hành `campus-flight-s11.onnx`, đầu vào duy nhất là `obs_0`, kích thước **26 số**; đầu ra là **3 lệnh bay**. Không có đầu vào ảnh.

26 số đó gồm:

| Nhóm dữ liệu | AI được cung cấp |
|---|---|
| Định vị tương đối | Vector từ drone đến điểm đích |
| Trạng thái chuyển động | Vận tốc, độ cao, tiếp đất |
| Khoảng cách vật cản | 10 tia: tám hướng ngang, một hướng lên và một hướng xuống |
| Trạng thái nhiệm vụ | Thời gian chặng, tiến độ hạ cánh |
| Logistics | Pin, tải, quay về, khẩn cấp, thời gian đến hạn |

Vì vậy, mô tả chính xác là: **AI bay dùng dữ liệu dạng số, gồm định vị, trạng thái và cảm biến khoảng cách giả lập; không nhận hình ảnh.**

Các tia này hiện được tính bằng hình học trong [FlightSimulation.cs](../Assets/AeroLab/Core/FlightSimulation.cs). Chúng chưa mô phỏng radar vật lý với tín hiệu phản xạ, Doppler hay đặc tính vật liệu. Gọi chúng là **cảm biến đo khoảng cách giả lập** sẽ chính xác hơn.

Cũng cần phân biệt hai điều:

- **AI được nhận khoảng cách:** đã xác nhận trong mã.
- **AI thực sự dựa vào khoảng cách để né vật cản:** chưa được chứng minh.

Muốn kiểm chứng điều thứ hai, cần chạy cùng các kịch bản với cảm biến bình thường, bị che hoặc bị thay đổi có kiểm soát; sau đó so sánh hành động, va chạm và khoảng cách an toàn. Đồng thời, có thể đổi ánh sáng/camera mà giữ nguyên đầu vào số để kiểm tra hành động có giữ nguyên hay không. **Mình chưa chạy các phép thử này trong lượt kiểm tra vừa rồi.**

Ngoài ra, bộ kiểm tra năng lượng và nhánh quay về khẩn cấp vẫn sử dụng tuyến tính từ bản đồ. Do đó, toàn hệ thống hiện chưa phải hệ tự hành hoàn toàn chỉ dựa vào cảm biến cục bộ.

**2. Có thể sử dụng địa hình có sẵn trong game như thế nào?**

Có ba nguồn phù hợp:

| Nguồn | Phù hợp với |
|---|---|
| Scene/prefab Unity bạn đã có | Khuôn viên, phố, nhà xưởng |
| Unity Terrain hoặc heightmap | Đồi, dốc, thung lũng |
| Mesh môi trường như FBX | Công trình, cầu, đường hầm, cấu trúc nhiều tầng |

Unity hỗ trợ nhập/xuất heightmap RAW để tạo Terrain. Với địa hình dạng lưới, có thể dùng MeshCollider; với Terrain, có TerrainCollider tương ứng. [Tài liệu heightmap Unity](https://docs.unity3d.com/6000.0/Documentation/Manual/terrain-Heightmaps.html), [MeshCollider](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/meshcollider).

**Nhưng nhập cảnh đẹp vào Unity mới giải quyết phần hiển thị.** Để drone học đúng, ba phần phải thống nhất:

```text
Hình học hiển thị
       ↓
Hình học cảm biến nhìn thấy
       ↓
Hình học kiểm tra va chạm
```

Trong dự án hiện tại, cảm biến và va chạm chỉ biết **nền phẳng cùng danh sách hộp tòa nhà trong Core**. Nếu thêm một quả đồi chỉ vào scene, drone có thể nhìn thấy trên màn hình nhưng lõi mô phỏng chưa biết quả đồi tồn tại.

Mình đề xuất mở rộng theo hướng giữ lõi hiện có:

1. Nhập một khu vực nhỏ từ scene/địa hình có sẵn.
2. Chuyển hình học cần thiết thành dữ liệu lõi đọc được: bản đồ cao độ cho mặt đất và hình khối hoặc lưới chiếm chỗ 3D cho vật cản.
3. Dùng cùng dữ liệu đó cho cảm biến, va chạm và lập đường.
4. Đặt lại kho, điểm giao, giới hạn sân và điều kiện hạ cánh theo địa hình mới.

Heightmap phù hợp với mặt đất lên xuống; cầu vượt, mái che và đường hầm cần biểu diễn 3D bổ sung.

Một phương án khác là dùng trực tiếp truy vấn collider của Unity. Khi đó vẫn nên giữ **một nơi duy nhất cập nhật chuyển động**, đồng thời điều chỉnh công cụ kiểm thử ngoài Unity để tránh hai môi trường mô phỏng khác nhau.

**3. Giảm độ cao bay hiện tại bằng cách nào?**

Hiện có **hai cơ chế độ cao khác nhau**:

| Chế độ | Độ cao đến từ đâu? |
|---|---|
| Bay tham chiếu | `RoutePlanner.Cruise = 16` m |
| Model AI đã huấn luyện | Chuỗi hành động do mạng xuất ra, chịu ảnh hưởng từ dữ liệu teacher |

Teacher trong [ClearancePilot.cs](../Assets/AeroLab/Core/ClearancePilot.cs) hướng drone lên khoảng **26 m khi còn xa đích**. Bộ sinh nhà huấn luyện giới hạn dưới 24 m. Cách dạy này tạo thiên hướng **bay cao hơn nhà rồi tiến đến đích**.

Điều đó giải thích vì sao model có thể chạy tốt mà chưa cần học cách luồn giữa vật cản.

Để giảm độ cao:

- **Với bộ tham chiếu:** tham số hóa độ cao hành trình và kiểm tra lại tuyến tại độ cao thấp hơn.
- **Với AI:** thay cách tạo dữ liệu mẫu, mục tiêu huấn luyện và kịch bản; sau đó huấn luyện lại hoặc fine-tune có đánh giá riêng.
- **Với địa hình đồi dốc:** dùng độ cao so với mặt đất ngay bên dưới drone, thay cho độ cao so với mặt phẳng `Y = 0`.

Ví dụ, với hull hiện tại:

```text
Khoảng cách đáy drone tới đất
    = độ cao tâm drone − cao độ đất tại vị trí đó − 0,5 m
```

**Đổi `Cruise` không thay hành vi của model ONNX đã học.** Tương tự, hạ trần bay từ 30 m xuống thấp hơn chỉ làm model dễ vi phạm giới hạn nếu nó vẫn muốn leo cao.

**4. Có thể cho AI chọn độ cao tối ưu và né tránh tinh vi hơn không?**

**Có. Model hiện đã điều khiển được trục lên/xuống; phần còn thiếu là thông tin quan sát, cách huấn luyện và tiêu chí đánh giá phù hợp.** Không nhất thiết phải thêm ngay một model AI thứ ba.

Mình đề xuất kiến trúc mở rộng như sau:

```text
Cảm biến khoảng cách 3D + định vị + vận tốc
                    ↓
          Biểu diễn vùng xung quanh
                    ↓
     Chọn hướng đi, tốc độ và độ cao
                    ↓
       Kiểm tra hành động an toàn
                    ↓
             Điều khiển drone
```

Những thay đổi cần thiết:

| Phần | Hướng mở rộng |
|---|---|
| **Cảm biến** | Thêm tia chếch lên/xuống ở phía trước và hai bên; hiện tám tia ngang dễ bỏ sót cấu trúc ở cao độ khác |
| **Thông tin theo thời gian** | Ghép các lần đo liên tiếp để biết vật đang tiến lại gần và giảm phản ứng muộn |
| **Môi trường học** | Có tình huống buộc phải vòng ngang, chui dưới hoặc leo qua; tránh việc luôn bay cao là đủ |
| **Mục tiêu học** | Cân bằng giao hàng, thời gian, năng lượng, khoảng cách an toàn và độ mượt |
| **Lớp bảo vệ** | Kiểm tra thể tích drone và quãng đường phanh trước khi chấp nhận lệnh |
| **Đánh giá** | Đo va chạm, khoảng hở nhỏ nhất, độ cao, năng lượng, thời gian và tỷ lệ hoàn thành trên bản đồ chưa học |

ML-Agents hỗ trợ cảm biến ray/sphere cast với các độ lệch theo chiều cao, phù hợp để mở rộng quan sát không dùng ảnh. [Tài liệu cảm biến ML-Agents](https://github.com/Unity-Technologies/ml-agents/blob/develop/com.unity.ml-agents/Documentation~/Learning-Environment-Design-Agents.md). Unity cũng có phép quét theo thể tích như SphereCast để kiểm tra vật có kích thước đi qua được hay không. [Tài liệu SphereCast](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.SphereCast.html).

“Độ cao tối ưu” cần gắn với mục tiêu cụ thể. Chẳng hạn, gặp một bức tường, bay vòng thấp có thể tốt hơn leo qua nếu tiết kiệm năng lượng; nhưng leo qua có thể tốt hơn nếu cần giao gấp. **Độ cao thấp nhất không tự động là độ cao tốt nhất.** Mô hình năng lượng hiện tại cũng cần cải tiến nếu muốn đánh giá đáng tin chi phí leo cao.

Nếu chỉ dùng cảm biến cục bộ, AI còn bị giới hạn bởi vùng đã quan sát; không thể bảo đảm tìm ra tuyến tối ưu toàn bản đồ chưa biết. Nếu cho bộ lập đường dùng bản đồ toàn cục, cần ghi rõ đó là hệ kết hợp bản đồ và cảm biến.

Với dự án này, bước phát triển phù hợp nhất là **mở rộng cảm biến 3D và cho độ cao hành trình thay đổi trên sân hiện tại trước**, chứng minh drone biết lựa chọn giữa vòng–leo–hạ; sau đó tích hợp một địa hình có sẵn. Cách này giúp xác định khả năng mới đến từ AI và cảm biến, rồi mới tăng độ phức tạp của môi trường.

## Lượt 4 — Yêu cầu lưu bản ghi

### Người dùng

> ghi lại tất cả những gì tôi đẫ hỏi và bạn đã trả lời

### Cập nhật của trợ lý trong lúc thực hiện

Mình sẽ lưu các câu hỏi và câu trả lời của hai bên trong cuộc trò chuyện này vào một file Markdown trong dự án, giữ nguyên thứ tự trao đổi.

### Kết quả lưu

Đã lưu toàn bộ câu hỏi và câu trả lời trong cuộc trò chuyện này tại [docs/conversation-2026-10-23.md](conversation-2026-10-23.md), theo đúng thứ tự, gồm cả các cập nhật tiến độ và nguồn tham khảo.
