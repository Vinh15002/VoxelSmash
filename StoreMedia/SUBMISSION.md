# Scrapyard Smash — nộp lên CrazyGames

## File cần upload

| Mục trong form | File |
|---|---|
| Game build (Unity WebGL, Brotli) | 4 file trong `Builds/WebGL/Build/` (`WebGL.loader.js`, `.framework.js.br`, `.data.br`, `.wasm.br`) — kéo thả trực tiếp, không nén zip. Tạo lại bằng menu **Voxel Smash → Build WebGL (CrazyGames)** |
| Cover 16:9 | `StoreMedia/cover-landscape-1920x1080.png` |
| Cover 2:3 | `StoreMedia/cover-portrait-800x1200.png` |
| Cover 1:1 | `StoreMedia/cover-square-800x800.png` |
| Video landscape | `StoreMedia/preview-landscape-1080p.mp4` (không tiếng) |
| Video portrait 2:3 | `StoreMedia/preview-portrait-1080x1620.mp4` (không tiếng; cắt giữa từ bản 1080x1920 vì form chỉ nhận 2:3) |

Video và ảnh bìa tạo lại được: build **Voxel Smash → Build Media Capture Player**, chạy
`ScrapyardSmash.exe -capture <thư mục> -width 1920 -height 1080 -level 20`, rồi ghép bằng ffmpeg.

## Thông tin điền form

- **Name:** Scrapyard Smash
- **Engine:** Unity (WebGL) — CrazyGames tự xử lý mute âm thanh cho Unity.
- **Orientation:** Landscape và Portrait đều hỗ trợ (UI tự đổi bố cục).
- **Progress Save:** BẬT (game lưu tiến trình, vàng, nâng cấp qua Data module của SDK).
- **Ads:** bản này KHÔNG có quảng cáo (không midgame, không rewarded). Code SDK quảng cáo vẫn còn trong
  `Platform.cs` nhưng không được gọi.
- **Category gợi ý:** Casual; tags: smash, destruction, voxel, physics, satisfying, strategy.

**Description**

> Smash junk into thousands of flying cubes! Every strike costs gold and every cube you break pays it back.
> Pick the right tool for the job: punch with a boxing glove, knock with a hammer, throw knives and
> shurikens, swing a flail or a wrecking ball, drop bombs and acid flasks, call down lightning or fire
> rockets. Topple the supports to bring the whole sculpture down for a fortune, but waste your strikes
> and you run out of gold. Bank what is left to upgrade your hits, chain combos, land critical blows and
> take down a giant boss every five levels.

**Controls**

> Click / tap the sculpture to strike. Hold to keep smashing. Drag sideways to turn it.
> Pick weapons from the panel (each shows its gold price). P pauses (desktop).

## Đã đáp ứng

- Build release, nén Brotli, đường dẫn tương đối.
- SDK: `GameplayStart/Stop` (chơi / menu / thắng / thua), `HappyTime` khi hạ boss hoặc mở vũ khí.
- Save qua `CrazySDK.Data` (không dựa vào PlayerPrefs trên web).
- Không dùng phím Esc (Esc thoát fullscreen) — dùng P; không có nút fullscreen riêng; không link ngoài.
- Vào gameplay bằng 1 click (PLAY). Tutorial trong gameplay, có nút Skip.
- Âm thanh vũ khí: Kenney.nl (CC0), license ở `Assets/_Game/Audio/Kenney-License.txt`.
- Vật lý bước cố định 60 Hz — không phụ thuộc màn 144/165 Hz.

## Chưa kiểm / cần lưu ý

- Chưa thử Safari, Chromebook 4 GB và điện thoại thật.
- Cân bằng vàng là bản đầu (giá vũ khí và túi vàng mỗi màn chỉnh trong Weapon Lab / Economy.cs).
