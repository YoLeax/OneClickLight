# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

**OneClickLight** 是一个 Beat Saber (v1.39.1 Steam) 的 BSIPA 插件，提供一键切换灯光/环境/颜色/Chroma 配置的功能。玩家在 Gameplay 设置页中可以快速在 "ON" 和 "OFF" 两套配置之间切换。

## 构建命令

```bash
# 构建项目（需要 BeatSaber 安装路径通过 $(BeatSaberDir) 提供）
dotnet build

# Release 构建
dotnet build -c Release
```

项目使用 `BeatSaberModdingTools.Tasks` NuGet 包，构建后会自动将输出复制到 Beat Saber 的 `Plugins` 目录。需要在 `OneClickLight.csproj.user` 中设置 `<BeatSaberDir>` 路径。

## 技术栈

- **目标框架**: .NET Framework 4.7.2 (Unity 2022.3)
- **依赖注入**: Zenject (通过 SiraUtil)
- **UI**: BeatSaberMarkupLanguage (BSML)
- **配置存储**: BSIPA Config Store

## 架构结构

### 入口点

- **`Plugin.cs`** — 插件主入口，`[Plugin(RuntimeOptions.DynamicInit), NoEnableDisable]`。在 `[Init]` 方法中：
  1. 创建 `PluginConfig` 实例
  2. 注册 `AppInstaller` (Location.App) 和 `MenuInstaller` (Location.Menu)

### 依赖注入

- **`Installers/AppInstaller.cs`** — App 级别安装器，将 `PluginConfig` 注册为全局单例
- **`Installers/MenuInstaller.cs`** — Menu 级别安装器，绑定 `GameplayMenu` 为单例

### 配置

- **`PluginConfig.cs`** — BSIPA 配置类，包含两个 `LightConfig` 实例 (`CfgOn` / `CfgOff`)
  - `LightConfig` 嵌套类包含所有灯光/环境相关配置项，每项都有一个 "O" 前缀的 override 布尔值和实际值
  - 涵盖 BaseGame、SongCore、Chroma、JDFixer 四个模块的配置
  - `Init()` 方法为 `CfgOff` 设置默认值（关闭所有特效）

### UI

- **`Menu/GameplayMenu.cs`** — Gameplay 设置页控制器，实现 `IInitializable`, `IDisposable`, `INotifyPropertyChanged`
  - 使用 BSML 属性标记 (`[UIValue]`, `[UIAction]`, `[UIComponent]`) 绑定 UI
  - 两页视图：主页 (ON/OFF 切换按钮) 和编辑配置页
  - 通过 `GameplaySetup.Instance.AddTab()` 注册到 Beat Saber 的 Gameplay 设置页
- **`Menu/gameplayMenu.bsml`** — BSML 模板，定义了主页面和编辑配置页面的布局

### 示例代码

- **`ExampleController.cs`** — 示例控制器，演示 Zenject 注入和 `ColorSchemesSettings` 的使用（仅做日志输出）

## 关键依赖

- **BSIPA** ^4.3.0 — 插件加载器
- **BeatSaberMarkupLanguage** ^1.12.0 — UI 框架
- **SiraUtil** ^3.1.0 — Zenject 注入桥接

## Beat Saber 游戏程序集

项目引用了多个 Beat Saber 游戏程序集（通过 `$(BeatSaberDir)` 路径），包括 `Main.dll`, `HMUI.dll`, `Zenject.dll`, `BSML.dll`, `SiraUtil.dll` 等。
