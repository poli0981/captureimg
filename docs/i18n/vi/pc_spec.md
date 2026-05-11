# Cấu hình máy phát triển

Tài liệu này mô tả phần cứng tham chiếu mà CaptureImage được phát
triển và kiểm thử trên đó. Đây là snapshot của máy chính của
maintainer — **không** phải yêu cầu runtime cho end-user. Yêu cầu
cho người dùng cuối nằm trong
[README § Requirements](../../../README.md#requirements).

Bản tiếng Anh: [`docs/pc_spec.md`](../../pc_spec.md).

## Máy phát triển chính

| Component   | Details                                                           |
|-------------|-------------------------------------------------------------------|
| **OS**      | Windows 11 Pro 25H2 Insider Preview (Dev Channel), build 26300.8376 |
| **CPU**     | Intel Core i7-14700KF                                             |
| **GPU**     | NVIDIA GeForce RTX 5080 (16 GB VRAM)                              |
| **RAM**     | 32 GB DDR5                                                        |
| **Storage** | 1 TB SSD                                                          |
| **IDE**     | JetBrains 2026.x paid lineup (Rider cho repo này; PyCharm / WebStorm / RustRover cho các project anh em) + Visual Studio Code |

Việc dùng Windows Insider Dev Channel là có chủ đích — CaptureImage
khai thác bề mặt API mới nhất của `Windows.Graphics.Capture` và
hưởng lợi khi chạy trên build đi trước stable, nhờ vậy regression
trên các phiên bản public sắp tới được phát hiện sớm.

## Thiết bị test mobile

Một số project lân cận / tương lai sẽ ship một web companion nhỏ.
Khi phần đó được triển khai, browser test trên mobile sẽ chạy trên:

- **iPhone 14 Pro** (iOS 26.x)
- **iPhone 13 Pro Max** (iOS 26.x)

Browser test trên iOS: **Chrome**, **Brave**.

> **Ghi chú.** Bản thân CaptureImage là một desktop application
> Windows 11 (WinUI 3 / Windows App SDK), không có build iOS / web.
> Các thiết bị mobile ở trên được liệt kê để minh bạch về test fleet
> tổng thể của maintainer; không dùng để validate CaptureImage.

## Tài liệu liên quan

- [`dev_env.md`](dev_env.md) — phiên bản toolchain (Python, Node,
  Rust, Git) và cấu hình IDE (bản VI).
- [`README.md` § Requirements](../../../README.md#requirements) —
  yêu cầu runtime cho người dùng cuối (Windows 11 22H2+, không yêu
  cầu GPU cụ thể).
- [`docs/ARCHITECTURE.md`](../../ARCHITECTURE.md) — kiến trúc phần
  mềm (EN, chưa có bản VI).
- [`docs/RELEASING.md`](../../RELEASING.md) — quy trình release
  (EN, chưa có bản VI).
