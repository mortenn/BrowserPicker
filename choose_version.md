# 我应该选择哪个版本？

你可在 [GitHub](https://github.com/XTsat/BrowserPicker_i18n/releases) 上获取最新版本

## 依赖 .NET 运行时的安装包

`NoDeps` 版本为即时编译（JIT）版本，需安装 [.NET 9.0 桌面运行时](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) 才能使用。
直接下载链接：[64 位系统](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-9.0.3-windows-x64-installer)、[32 位系统](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-9.0.3-windows-x86-installer)。

## 便携版安装包

`Portable` 版本包含适用于 win-x64 系统的可执行文件，可以不用安装直接使用。

## 原生镜像生成

安装过程中，`BrowserPicker.msi` 会执行 ngen 工具为你的电脑生成原生镜像，这能显著提升可执行文件的启动速度。若你选择便携版本，可运行命令 `ngen install BrowserPicker.exe` 以获得相同优化效果。

<!-- ### 签名证书

为避免 “未知发布者” 警告，可先将提供的证书导入你的证书存储区，导入方法参考 [此处](https://stackoverflow.com/questions/49039136/powershell-script-to-install-trusted-publisher-certificates)。 -->

## 手动编译

`VS` 版本是手动编译的最小文件版本
