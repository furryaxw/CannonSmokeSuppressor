# Smoke Accumulation Suppressor

适用于《Sprocket》的 MelonLoader 模组，只移除造成长期堆积的烟雾输出层。

## 屏蔽范围

- `ExhaustSmoke/System (5)`：长期残留的引擎尾烟堆积层。
- `MediumCannonFire/System (14)`：长期残留的炮口烟堆积层。

正常引擎尾烟、瞬时炮口烟、炮口焰、火花、冲击波、扰动、命中烟尘和车辆
起火效果都会保留。

模组在原生特效初始化入口中只清空上述两个 VFX renderer material slot，
不再持续扫描车辆或场景。

## 构建

```powershell
dotnet build .\CannonSmokeSuppressor\CannonSmokeSuppressor.csproj -c Release
```

使用 `-p:SkipModDeploy=true` 可只构建而不复制到 `G:\Sprocket\Mods`。

## 许可证

[GPL-3.0-only](LICENSE.txt)
