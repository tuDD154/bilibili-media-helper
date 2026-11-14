# B站跟视频助手（Bilibili Media Helper）

一个 Windows 桌面小工具：内置 B 站浏览器窗口，在《原神》保持前台时，用全局快捷键控制攻略视频的播放、暂停、快退和快进，不抢游戏焦点。

仓库同时包含一个可选的浏览器扩展版本，适合想控制现有 Edge/Chrome 标签页的场景。

> 本项目为非官方项目，与哔哩哔哩、米哈游无关联。仅用于个人学习和辅助观看，请遵守相关平台的服务条款；请勿用于绕过付费、广告、DRM 或登录限制。

## 功能

- 内置 B 站浏览器，不需要安装浏览器扩展
- 全局快捷键在游戏前台、应用失焦或最小化时仍然有效，不会激活应用窗口
- 默认单键：`F8` 播放/暂停、`F9` 快退 3 秒、`F10` 快进 3 秒
- 设置里可以自定义任意单键或组合键，并检测全局快捷键冲突
- 快进/快退步长可调（1–30 秒）
- 窗口可以自由缩放，没有额外的最小尺寸限制
- 窗口置顶、视频全屏、B 站页面自动缩放

单键会被系统全局占用，游戏也收不到该键。原神默认已用 `F1–F5`、`F7`，建议使用 `F8–F10` 或游戏未绑定的按键。

## 环境要求

- Windows 10 / 11，64 位
- .NET Framework 4.8（Windows 10/11 通常自带）
- Microsoft Edge WebView2 Runtime（Win11 通常自带）
- 仅构建需要：Windows PowerShell 和 .NET Framework 的 `csc.exe`

## 构建

在仓库根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\BilibiliMediaApp\build.ps1
```

构建脚本会自动从 NuGet 下载固定版本的 WebView2 SDK 到 `.packages` 目录，然后编译到：

```text
BilibiliMediaApp\dist\BilibiliMediaApp.exe
```

如果你已经有一份 WebView2 SDK，可以跳过下载：

```powershell
powershell -ExecutionPolicy Bypass -File .\BilibiliMediaApp\build.ps1 -WebView2Sdk "D:\path\to\webview2-sdk"
```

`-WebView2Sdk` 可以是解压后的 NuGet 包目录，也可以直接包含 `Microsoft.Web.WebView2.Core.dll`、`Microsoft.Web.WebView2.WinForms.dll`、`WebView2Loader.dll`。

## 运行

双击 `BilibiliMediaApp\dist\BilibiliMediaApp.exe`。

首次使用时在应用内登录 B 站，打开攻略视频并手动点一次播放，然后切回游戏按快捷键即可。

## 浏览器扩展（可选）

`bilibili-media-hotkeys` 是 Chrome / Edge 扩展版本，默认快捷键为 `Ctrl+Shift+1/2/3`。桌面应用默认使用 `F8/F9/F10`，两者默认不冲突。

## 目录结构

```text
.
├─ BilibiliMediaApp/
│  ├─ build.ps1
│  └─ src/
├─ bilibili-media-hotkeys/
├─ LICENSE
└─ THIRD-PARTY-NOTICES.txt
```

## 数据与隐私

应用不收集、不上传数据。WebView2 的登录状态和设置在本地 `Data` 目录中，已被 `.gitignore` 排除，不应提交到仓库。

## 许可证

本项目代码采用 MIT License，详见 [LICENSE](LICENSE)。

WebView2 SDK 使用 BSD-3-Clause 许可，相关声明见 [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)。
