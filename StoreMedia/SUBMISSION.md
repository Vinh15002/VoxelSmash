# Scrapyard Smash — nộp lên CrazyGames

## File cần upload

| Mục trong form | File |
|---|---|
| Game build (Unity WebGL, Brotli) | `Builds/WebGL.zip` (12 MB, 6 file) — tạo lại bằng menu **Voxel Smash → Build WebGL (CrazyGames)** |
| Cover 16:9 | `StoreMedia/cover-landscape-1920x1080.png` |
| Cover 2:3 | `StoreMedia/cover-portrait-800x1200.png` |
| Cover 1:1 | `StoreMedia/cover-square-800x800.png` |
| Video landscape | `StoreMedia/preview-landscape-1080p.mp4` (18.9 s, không tiếng) |
| Video portrait | `StoreMedia/preview-portrait-1080p.mp4` (14 s, không tiếng) |

Video và ảnh bìa tạo lại được: build **Voxel Smash → Build Media Capture Player**, chạy
`ScrapyardSmash.exe -capture <thư mục> -width 1920 -height 1080 -level 20`, rồi ghép bằng ffmpeg.

## Thông tin điền form

- **Name:** Scrapyard Smash
- **Engine:** Unity (WebGL) — CrazyGames tự xử lý mute âm thanh cho Unity.
- **Orientation:** Landscape và Portrait đều hỗ trợ (UI tự đổi bố cục).
- **Progress Save:** BẬT (game lưu tiến trình, xu, nâng cấp qua Data module của SDK).
- **Category gợi ý:** Casual / Clicker; tags: smash, destruction, voxel, physics, idle, satisfying.

**Description**

> Smash junk into thousands of flying cubes! Tap or click a voxel sculpture to strike it with a boxing glove,
> hammer, bombs, lightning, a wrecking ball or rockets. Knock out the supports and watch the whole thing
> collapse. Earn coins from every cube, upgrade your hits, chain combos, land critical blows and take down
> a giant boss every five levels in a scrapyard full of junk.

**Controls**

> Click / tap the sculpture to strike. Hold to keep smashing. Drag sideways to turn it.
> Pick weapons from the panel. P pauses (desktop).

## Đã đáp ứng

- Build release, nén Brotli, 12 MB tải ban đầu (< 20 MB: đủ điều kiện trang chủ mobile), 6 file, đường dẫn tương đối.
- SDK v5.36: `GameplayStart/Stop` (chơi / menu / thắng / quảng cáo), `HappyTime` khi hạ boss hoặc mở vũ khí.
- Save qua `CrazySDK.Data` (không dựa vào PlayerPrefs trên web).
- Quảng cáo chỉ qua SDK: midgame khi bấm NEXT (từ màn 2, SDK tự giới hạn 3 phút/lần);
  rewarded "3 power hits" là tuỳ chọn, có cách mua bằng xu thay thế, không thưởng khi lỗi,
  nút tự ẩn khi bị chặn quảng cáo / Basic Launch tắt quảng cáo; bỏ midgame nếu vừa xem rewarded.
- Không dùng phím Esc (Esc thoát fullscreen) — dùng P; không có nút fullscreen riêng; không link ngoài.
- Vào gameplay bằng 1 click (PLAY). Tutorial trong gameplay, có nút Skip, hướng dẫn bằng hình.
- Bố cục ngang cho desktop; chữ đọc được ở 1216×684 (đã thử trong trình duyệt).
- Vật lý bước cố định 60 Hz — không phụ thuộc màn 144/165 Hz.

## Chưa kiểm / cần lưu ý

- Quảng cáo thật chỉ chạy trên domain CrazyGames; đã thử bản demo trên localhost (rewarded cộng thưởng đúng).
- Chưa thử Safari, Chromebook 4 GB và điện thoại thật.
- Hai cú click tự động cực nhanh (nhấn-nhả cùng 1 frame) đôi khi không được UI nhận; click thật thì ổn.
