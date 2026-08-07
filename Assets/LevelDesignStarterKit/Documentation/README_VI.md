# LEVEL DESIGN STARTER KIT — HƯỚNG DẪN NHANH

Starter Kit này dành cho bài tập graybox first-person trong Unity 2022.3.
Toàn bộ hình ảnh dùng primitive có sẵn của Unity, không cần tải package hoặc asset ngoài.

## 1. Tạo toàn bộ Starter Kit

Sau khi Unity compile xong, chọn:

`Tools > Level Design Starter Kit > Build or Rebuild Complete Kit`

Unity sẽ tạo:

- `Generated/Materials`: quy ước màu cho graybox và gameplay marker.
- `Generated/Prefabs/LD_Player`: player first-person.
- `Generated/Prefabs/LD_Guard`: enemy tuần tra, phát hiện và đuổi.
- `Generated/Prefabs/LD_Checkpoint`: checkpoint.
- `Generated/Prefabs/LD_EnergyCore`: mục tiêu cần thu thập.
- `Generated/Prefabs/LD_Exit`: cổng kết thúc.
- `Generated/Scenes/LD_StarterKit_Demo`: scene demo có thể Play ngay.

Menu Build/Rebuild có thể chạy lại khi cần. Những asset trong thư mục `Generated`
sẽ được cập nhật; script runtime và documentation không bị thay đổi.

## 2. Điều khiển

- Khi scene bắt đầu, xem `CAMERA VIEWING` để quan sát cấu trúc level, sau đó click `Begin`.
- Duplicate `LD_PreviewCamera_01` để thêm điểm nhìn; nút `Back`/`Next` sẽ duyệt theo `Order`.
- `WASD` hoặc phím mũi tên: di chuyển.
- Giữ `Shift`: chạy.
- `Space`: nhảy.
- Chuột: xoay camera.
- `Esc`: thả hoặc khóa con trỏ.
- Click chuột trái trong Game View: khóa lại con trỏ.

## 3. Quy ước màu

- Xám đậm: sàn.
- Xám sáng: tường, cover và geometry.
- Xanh dương: player.
- Đỏ: enemy.
- Vàng: Energy Core/objective.
- Tím: checkpoint.
- Xanh lá: exit.

## 4. Tạo enemy và patrol path

Cách nhanh nhất:

`GameObject > Level Design Starter Kit > Create Guard With Patrol Path`

Lệnh này tạo một Guard và một Patrol Path có hai waypoint. Sau đó:

1. Chọn các object `Waypoint_01`, `Waypoint_02` và di chuyển chúng trong Scene View.
2. Thêm waypoint bằng cách tạo Empty GameObject làm con của `LD_PatrolPath`.
3. Đặt các waypoint trên mặt sàn, theo thứ tự trong Hierarchy.
4. Chọn Guard để chỉnh tốc độ, khoảng nhìn, góc nhìn và thời gian mất dấu.

Guard không dùng NavMesh. Waypoint cần nằm trên đường đi thông thoáng. Cơ chế steering
chỉ hỗ trợ né các vật cản đơn giản; không nên bắt guard tự tìm đường qua mê cung phức tạp.

## 5. Các thông số quan trọng của Guard

- `Patrol Speed`: tốc độ tuần tra.
- `Chase Speed`: tốc độ đuổi player.
- `Detection Distance`: khoảng phát hiện tối đa.
- `View Angle`: độ rộng sight cone.
- `Lose Player After`: thời gian tìm kiếm sau khi mất dấu.
- `Catch Distance`: bán kính vòng đỏ quanh guard; player đi vào vòng này sẽ bị bắt.
- `Obstacle Probe Distance`: khoảng dò vật cản đơn giản.

Sight cone và patrol path hiển thị bằng Gizmos trong Scene View. Bật nút `Gizmos`
ở góc trên bên phải nếu không nhìn thấy.

## 6. Tạo bài tập mới

Khuyến nghị giữ nguyên scene demo làm reference và tạo một scene khác cho học viên:

1. Tạo scene trống.
2. Kéo `LD_Player`, `LD_Checkpoint`, `LD_EnergyCore`, `LD_Exit` vào scene.
3. Tạo Empty GameObject và gắn component `LDGameSession`.
4. Tạo Main Camera, gắn `LDThirdPersonCamera`, kéo Player vào trường Target.
5. Kéo Main Camera vào trường Camera Transform của `LDPlayerMotor`.
6. Tạo geometry bằng Cube, Plane, Ramp hoặc ProBuilder.
7. Đặt Guard và waypoint.
8. Nhấn Play và tổ chức blind playtest.

Có thể duplicate trực tiếp `LD_StarterKit_Demo`, xóa phần graybox mẫu và giữ lại
nhóm `--- SYSTEMS ---` cùng các gameplay prefab.

## 7. Giới hạn có chủ đích

Starter Kit dùng để đánh giá Level Design, không phải làm production AI:

- Không combat.
- Không animation.
- Không inventory hoặc save game.
- Chỉ có một loại enemy.
- Enemy dùng waypoint và steering đơn giản, không dùng pathfinding phức tạp.

Giữ các giới hạn này giúp kết quả bài làm phản ánh flow, scale, sightline,
landmark, enemy placement, pacing và khả năng iteration của học viên.

