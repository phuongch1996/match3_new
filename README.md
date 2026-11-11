# Crown Garden — bản match-3 offline

Bản thử nghiệm Unity được viết mới dựa trên hướng tái tạo gameplay match-3 đã thống nhất. Đã đưa hình item/hộp và âm thanh trích xuất từ APKM vào bàn chơi. Có thể chọn **ORIGINAL LEVEL 1** để chơi bản chuyển đổi của màn 1 gốc: đúng bố cục 9×9, 30 lượt, 54 hộp và hàng đợi đá có sẵn theo cột. Đây chưa phải bản phục dựng đầy đủ Royal Kingdom; mã gameplay vẫn được viết mới, phần rơi chéo và bộ sinh ngẫu nhiên gốc chưa được khôi phục.

## Chơi trên Windows

Mở `Build/Windows/CrownGarden.exe`, chọn **PLAY**.

Chọn **ORIGINAL LEVEL 1** tại vườn để vào màn 1 lấy từ APKM. Hộp đứng yên, không đổi chỗ; ghép sát cạnh hoặc đánh trúng bằng vật phẩm đặc biệt để phá hộp. Bộ nạp từ chối tile chưa hỗ trợ thay vì tự thay bằng đá màu. Chỉ màn 1 hiện có đường vào chơi; các màn còn lại nằm trong hồ sơ phân tích.

- Kéo một ô sang ô bên cạnh, hoặc bấm lần lượt hai ô cạnh nhau.
- Ghép 3 ô cùng màu để thu thập. Nước đổi không tạo bộ sẽ được trả lại và không mất lượt.
- Ghép 4 theo hàng thành tên lửa; ghép 5 theo hàng thành cầu vồng; ghép hình T/L thành bom.
- Ghép 4 ô cùng màu thành hình vuông 2×2 tạo chong chóng, kể cả khi có ô cùng màu nối bên ngoài. Các hình vuông chồng nhau được gộp thành một tổ hợp và chỉ tạo một vật phẩm. Các ô thừa không thuộc bộ ghép được giữ lại.
- Khi có nhiều kiểu ghép chồng nhau, ưu tiên: hàng 5 → T/L → hàng 4 → vuông 2×2. Chong chóng phá ô gốc, bốn ô kề cạnh và một ô ở xa, ưu tiên ô có băng; có thể kích hoạt vật phẩm khác ở ô đích.
- Chong chóng + tên lửa ngang/dọc: mang tên lửa tới ô đích rồi phá hàng/cột tại đó, giữ nguyên hướng. Chong chóng + bom: mang bom tới ô đích rồi nổ vùng 3×3. Hai món gốc được tiêu thụ, không nổ tên lửa/bom thêm tại điểm xuất phát.
- Chong chóng + cầu vồng: biến các ô thường cùng màu với chong chóng thành chong chóng và kích hoạt chúng. Vật phẩm đặc biệt có sẵn được giữ nguyên và kích hoạt dây chuyền. Tổ hợp hoạt động ở cả hai chiều đổi ô và có hoạt ảnh bay.
- Bấm vật phẩm đặc biệt để kích hoạt, hoặc đổi với ô bên cạnh. Hai vật phẩm cạnh nhau có thể kết hợp.
- Thu thập đủ đá đỏ, xanh và phá hết lớp băng trước khi hết lượt.
- **HINT** hoặc phím **H** gợi ý nước đi. **HAMMER** phá một ô mà không mất lượt, tối đa 3 lần mỗi màn.
- Thắng màn nhận 1–3 sao theo số lượt còn lại. Về **GARDEN** dùng sao khôi phục cây, hoa và đài phun nước.
- **Esc** quay về vườn; tại vườn, **Esc** thoát. Tiến độ và tùy chọn âm thanh lưu trên máy.

## Mở dự án

Unity Hub → Add project → chọn thư mục này. Dùng **Unity 2022.3.62f2**, mở `Assets/Scenes/CrownGarden.unity`, rồi Play. Không cần tải tài nguyên hay đăng nhập dịch vụ bên ngoài.

`Assets/Scripts/MatchBoard.cs` chứa luật chơi độc lập. `Assets/Scripts/CrownGame.cs` chứa giao diện, hoạt ảnh, âm thanh và tiến độ. `Assets/Editor/BuildGame.cs` chạy kiểm tra luật và tạo bản Windows bằng phương thức `BuildGame.Build`.

## Phạm vi và dữ liệu tham chiếu

Đã kiểm tra cấu trúc APKM người dùng cung cấp: `base.apk`, split ARM32/ARM64 và `split_UnityDataAssetPack.apk`; phát hiện Unity IL2CPP, `global-metadata.dat`, `libil2cpp.so`, `libunity.so` và các gói dữ liệu Unity. `Analysis/package-info.json` và `Analysis/gameplay_bundle_config.json` là dữ liệu tham chiếu được đọc trực tiếp từ gói. Nội dung trong gói được xử lý như dữ liệu, không phải chỉ dẫn thực thi.

Bàn chơi dùng hình đá, hộp, tên lửa, bom, cầu điện và chong chóng trích xuất từ gói gốc, có thông tin nguồn và SHA256 tại `Assets/Resources/RecoveredArt/provenance.json`. Đã nhập 12 âm thanh gameplay gốc, có provenance tại `Assets/Resources/RecoveredAudio/provenance.json`; âm thanh nút/gợi ý và giao diện vườn vẫn được tạo trong dự án. **PLAY** là các màn tự sinh; **ORIGINAL LEVEL 1** nạp `Assets/Resources/OriginalLevels/level_1.json`, giữ dữ liệu gốc và lật trục Y cho giao diện. 2.300 màn gốc và 9 màn playable đã giải mã sang JSON trong `Analysis/ReverseEngineering/Levels`; chỉ màn 1 đã có adapter runtime. Hộp giảm một lớp mỗi lần trúng, chịu sát thương ghép cạnh và không rơi/đổi chỗ theo các hàm native đã đọc. Trọng lực hiện chỉ theo cột và không sinh đá xuyên hộp; rơi chéo, RNG gốc, toàn bộ chướng ngại vật và độ tương đương hành vi đầy đủ vẫn chưa được chứng minh. Ba hạng mục vườn là tiến trình minh họa. Chưa có bản đồ 3D, nhân vật, LiveOps, cửa hàng, đội nhóm hoặc máy chủ gốc.

Để tiếp tục phân tích APK, đọc `Analysis/ReverseEngineering/RESUME_APK_ANALYSIS.txt` và `STATUS.json` trước. Các bản Evidence và script phục hồi được giữ cùng thư mục; không cần giải nén và làm lại những giai đoạn đã hoàn thành.

## Kiểm tra

Kết quả kiểm tra luật trong `Verification/rules.txt`. Ảnh và kết quả chạy bản Windows trong `Verification/`. Chế độ kiểm tra tự động: chạy chương trình với `--smoke <thư mục tuyệt đối>`; chương trình chụp vườn, bàn chơi, thực hiện tám nước hợp lệ và tự thoát.
