# Môi trường phát triển

Bộ công cụ dùng để build, test và phát hành CaptureImage. Tham chiếu phần cứng:
[`pc_spec.md`](pc_spec.md). Bản tiếng Anh: [`dev_env.md`](../../dev_env.md).

## Công cụ

| Công cụ | Phiên bản | Mục đích |
|---|---|---|
| **.NET SDK** | 10.0 (feature band cụ thể được pin trong [`global.json`](../../../global.json)) | Build, test, chạy |
| **Windows** | 11 22H2 trở lên | Bắt buộc cho app WPF và `Windows.Graphics.Capture` |
| **Git** | 2.40 trở lên | Quản lý mã nguồn |
| **GPG** | có hỗ trợ Ed25519 | Ký commit và tag (bắt buộc trên `main` và cho tag phát hành) |
| **GitHub CLI** (`gh`) | bản mới | Pull request, phát hành, kiểm tra attestation |
| **Node.js** | 22 LTS trở lên | `markdownlint-cli2` cho bước kiểm tra docs (không bắt buộc khi làm local) |

Các projection của Windows SDK được lấy từ NuGet theo target framework
(`net10.0-windows10.0.22621.0`); không cần cài Windows SDK riêng. CI dùng `actions/setup-dotnet` với
`global-json-file: global.json`, nên SDK ở máy local và CI luôn khớp nhau.

Các .NET tool mà CI dùng được pin trong
[`.config/dotnet-tools.json`](../../../.config/dotnet-tools.json) và cài bằng `dotnet tool restore`:
`vpk` (Velopack), JetBrains InspectCode, `nuget-license`, ReportGenerator và CycloneDX.

## Trình soạn thảo

- **JetBrains Rider** (chính) — các inspection của ReSharper khớp với bước kiểm tra `deadcode`
  trên CI.
- **Visual Studio 2026** hoặc **Visual Studio Code** với C# Dev Kit cũng dùng được.

## Build, test, chạy

```powershell
git clone https://github.com/poli0981/captureimg.git
cd captureimg
dotnet build CaptureImage.sln -c Release
dotnet test --solution CaptureImage.sln -c Release
dotnet run --project src/CaptureImage.App -c Release -- --data-dir .\.devdata --instance-id dev
```

Warning được coi là lỗi và `src/` bật bộ analyzer khuyến nghị của .NET, nên build sạch ở local nghĩa
là bước build trên CI cũng biên dịch được. Xem [`CONTRIBUTING.md`](../../../CONTRIBUTING.md) để biết
các lệnh format và toàn bộ danh sách check bắt buộc.

## Công cụ phát hành

Bản phát hành được GitHub Actions tạo từ tag đã ký — xem [`RELEASING.md`](../../RELEASING.md).

## Tài liệu liên quan

- [`pc_spec.md`](pc_spec.md) — cấu hình phần cứng.
- [`ARCHITECTURE.md`](../../ARCHITECTURE.md) — kiến trúc của app.
- [`CONTRIBUTORS.md`](../../CONTRIBUTORS.md) — người đóng góp và chính sách hỗ trợ AI.
- [`README.md`](../../../README.md) — tổng quan dự án.
