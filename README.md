# CannonSmokeSuppressor

适用于《Sprocket》的 MelonLoader 模组，用于限制短时间内堆积的炮口烟雾特效，减少密集开火时的画面遮挡和性能压力。

## 功能

- 仅检查当前场景中车辆的 `cannon` 层级。
- 在同一 `1 × 1 × 1` 世界坐标区域内最多保留 5 个炮口烟雾特效。
- 优先保留最新生成的烟雾，并停用多余的旧特效。

## 安装

1. 安装与游戏版本匹配的 MelonLoader。
2. 构建项目。
3. 将 `CannonSmokeSuppressor.dll` 放入游戏根目录的 `Mods` 文件夹。

## 构建

项目目标框架为 .NET 6，并引用本地 Sprocket MelonLoader/IL2CPP 程序集。默认目录布局为：

```text
G:\Sprocket\
├── MelonLoader\
└── mod\CannonSmokeSuppressor\
```

```powershell
dotnet build .\CannonSmokeSuppressor\CannonSmokeSuppressor.csproj --configuration Release
```

## License

[GPL-3.0-only](LICENSE.txt)
