# Môi trường phát triển

Tài liệu này liệt kê các phiên bản toolchain dùng để build, test
và release CaptureImage. Tham chiếu phần cứng:
[`pc_spec.md`](pc_spec.md). Bản tiếng Anh:
[`docs/dev_env.md`](../../dev_env.md).

## Toolchain

| Toolchain   | Version                                | Purpose                                       |
|-------------|----------------------------------------|-----------------------------------------------|
| **.NET SDK**| 9.0 (pin chính xác trong [`global.json`](../../../global.json)) | Build + test runtime chính |
| **Python**  | 3.12                                   | Release scripting, Discord notification helper (`.github/scripts/discord_notify.py`) |
| **Node.js** | ≥ 22 LTS                               | Docs site / web companion tooling tương lai   |
| **Rust**    | stable, qua `rustup`                   | Dự phòng cho native helper tương lai (chưa có trong v1.5.x) |
| **Git**     | mới (≥ 2.40)                           | Source control                                 |
| **GPG**     | bất kỳ phiên bản nào hỗ trợ Ed25519    | Ký commit + tag — `commit.gpgsign=true` được bật repo-wide; tag signing là bắt buộc theo [`docs/RELEASING.md`](../../RELEASING.md) |

CI runner dùng `actions/setup-dotnet@v5` với `global-json-file:
global.json`, nên phiên bản .NET local và CI đảm bảo khớp nhau —
xem [`.github/workflows/ci.yml`](../../../.github/workflows/ci.yml).

## Cấu hình IDE

Chính: **JetBrains Rider 2026.x** (paid lineup). Plugin Rider khuyến
nghị:

- *.NET Core User Secrets* (có sẵn)
- *Markdown* (có sẵn)
- *Resource Bundle Editor* — tiện khi edit 11 file `.resx` locale
  cùng lúc
  ([`src/CaptureImage.UI/Resources/Strings/`](../../../src/CaptureImage.UI/Resources/Strings/))

JetBrains IDE anh em dùng trên các project lân cận: **PyCharm**,
**WebStorm**, **RustRover**.

Phụ: **Visual Studio Code** với extension C# Dev Kit cho các edit
nhanh.

## Windows SDK

WinUI 3 + projection của `Windows.Graphics.Capture` yêu cầu
**Windows 11 22H2 SDK** (10.0.22621). Cài qua Visual Studio
Installer ("Windows 11 SDK (10.0.22621.x)") hoặc trực tiếp từ
<https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/>.

## Build / test / run

```powershell
git clone https://github.com/poli0981/captureimg.git
cd captureimg
dotnet restore
dotnet build CaptureImage.sln -c Release
dotnet test  CaptureImage.sln -c Release
dotnet run --project src/CaptureImage.App -c Release
```

Lệnh trên CI là mirror chính xác của các lệnh trên, có thêm
`/p:TreatWarningsAsErrors=true /p:WarningsAsErrors=nullable` —
chạy y hệt ở local trước khi mở PR.

## Release tooling

- **Velopack** (`vpk` CLI) — packaging + delta update.
- **`gh`** (GitHub CLI) — tạo Release, upload artifact, fetch SHA
  (lookup SHA của reusable-workflow pin trong
  [`docs/RELEASING.md`](../../RELEASING.md)).
- Xem [`docs/RELEASING.md`](../../RELEASING.md) cho chu trình end-to-end.

## Tài liệu liên quan

- [`pc_spec.md`](pc_spec.md) — tham chiếu phần cứng (bản VI).
- [`docs/ARCHITECTURE.md`](../../ARCHITECTURE.md) — kiến trúc phần
  mềm (EN, chưa có bản VI).
- [`docs/CONTRIBUTORS.md`](../../CONTRIBUTORS.md) — chính sách đóng
  góp (EN, chưa có bản VI).
- [`README.md`](../../../README.md) — tổng quan project.
