<!--markdownlint-disable MD001 MD033 MD041 MD051-->

<div align="center">

# <img src="Assets/app-256.png" height="28"/> PointManager

[![Stars](https://img.shields.io/github/stars/XPSoftware/PointManager?label=Stars)](https://github.com/XPSoftware/PointManager)
[![Release](https://img.shields.io/github/v/release/XPSoftware/PointManager?style=flat-square&color=%233fb950&label=正式版)](https://github.com/XPSoftware/PointManager/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/XPSoftware/PointManager/total?style=social&label=下载量&logo=github)](https://github.com/XPSoftware/PointManager/releases/latest)

PointManager（学生积分管理）是一款轻量的本地桌面工具，用于班级学生的加减分记录与总分统计，全部数据保存在您自己的电脑上。<br/>
基于 .NET 8 WPF + Material Design 构建，常驻系统托盘，即开即用。

#### [📝 更新日志](CHANGELOG.md) ｜ [🐛 问题反馈](https://github.com/XPSoftware/PointManager/issues)

</div>

## 功能

### 应用形态

- [x] 托盘常驻：关闭窗口即隐藏到托盘，双击托盘图标打开「加减分」窗口
- [x] 四个窗口（学生及小组编辑 / 加减分 / 总分查看 / 设置）均为单例，重复打开时恢复并聚焦
- [x] 可选「启动时最小化到托盘」与「退出前需要确认」
- [x] 设置「关于」页：版本与版权、诊断信息、第三方库与许可说明
- [x] 未处理异常自动记录到 `%AppData%\PointManager\crash.log`，便于排查

### 学生及小组管理

- [x] 学生页两栏主从布局：左侧学生表格（学号 / 姓名 / 性别 / 备注），右侧详情编辑（姓名 / 小组 / 性别 / 备注）
- [x] 学号按列表顺序自动编号，「调整顺序」对话框上移 / 下移批量重排；「批量导入学生」一行一个姓名
- [x] 学生头像：默认取姓名首字，可修改自定义图片，头像文件随数据目录保存
- [x] 学生存档 / 取消存档：存档学生信息只读、仅可删除，不再出现在加减分列表，头像显示为存档图标
- [x] 小组新建 / 删除，学生分组 / 移出小组，小组详情查看成员列表
- [x] 多档案支持：新建、复制、重命名、删除档案，随时热切换

### 加减分

- [x] 多选学生批量加减分，支持全选 / 清空选择
- [x] 按小组筛选（全部 / 指定小组 / 未分组）与姓名搜索
- [x] 快捷分值按钮（默认 1 / 2 / 3 / 5 / 10，可增删）与自定义分值
- [x] 加减分备注，可配置默认备注，操作结果以 Snackbar 提示

### 总分查看

- [x] 统计卡片：小组数、学生数、总积分、记录数
- [x] 学生总分表（点击列标题排序）、小组总分与人均分、最近积分记录
- [x] 校验总分：按历史记录核对学生总分，列出不一致项并可一键修正

### 个性化

- [x] 深色 / 浅色主题切换，跟随 Windows 系统强调色
- [x] 自定义主题色 / 强调色：预设色板 + 最近使用颜色 + HSV 全色取色器
- [x] 全局字体选择（实时预览），标题栏文字颜色按背景亮度自动取黑 / 白
- [x] 标签页 Material Design 波纹点击反馈

### 数据与存储

- [x] 数据拆分本地存储：设置、档案、积分记录（按月分文件），均为 JSON
- [x] 存储位置切换（用户目录 / 程序所在目录），切换时完整复制数据
- [x] 旧版单文件 `data.json` 非破坏性迁移，旧文件自动备份
- [x] 历史记录上限、清空积分记录、重置所有积分、一键打开数据目录

## 待办

### 安全

- [ ] 密码保护

### 功能重构

- [ ] 重构小组
- [ ] 重构加减分理由

### 界面

- [ ] 重构设置页面
- [ ] 给所有设置页面添加分割标签（参照存储页的「备份」「积分」）

## 开始使用

**请确保您的设备满足以下推荐需求：**

- Windows 10 及以上版本的系统，x64 架构
- 安装 [.NET 8 桌面运行时](https://dotnet.microsoft.com/zh-cn/download/dotnet/thank-you/runtime-desktop-8.0-latest-windows-x64-installer)（下载自包含包则无需安装）

从 [Releases](https://github.com/XPSoftware/PointManager/releases/latest) 下载最新版本的压缩包，解压到一个**独立的文件夹**后运行 `PointManager.exe` 即可。解压时请不要解压到网盘同步文件夹或【下载】文件夹中，否则可能出现文件无法读写、记录丢失等问题。

首次运行会引导您创建第一个档案；历史版本升级时会在启动时询问是否迁移旧数据，旧文件将备份到 `Backup` 目录，不会删除。

## 获取帮助

- 遇到 Bug 或想提出新功能，请提交 [Issue](https://github.com/XPSoftware/PointManager/issues/new)
- 程序异常时请先查看 `%AppData%\PointManager\crash.log`，并将其内容附在 Issue 中

## 开发

编译本项目需要：

- [.NET 8 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0)
- 支持 WPF 的 IDE（如 Visual Studio 2022 并勾选「.NET 桌面开发」工作负载，或 JetBrains Rider）

```bash
dotnet build -c Release
```

欢迎提交 Pull Request 改进本应用。

## 致谢

> [!IMPORTANT]
> 本项目在界面设计（设置「关于」页、取色器色板等）上参考了 [ClassIsland](https://github.com/ClassIsland/ClassIsland) 的源代码。所参考的 ClassIsland 源代码版本处于 **MIT 许可时期**，以 [MIT License](https://github.com/ClassIsland/ClassIsland/blob/master/LICENSE.txt) 发布；本项目的实现为独立的 WPF 代码，特此说明并致谢。

本项目使用了以下第三方库和框架：

- [MaterialDesignThemes](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit) / [MaterialDesignColors](https://github.com/MaterialDesignInXAML/MaterialDesignColors)（MIT）
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)（MIT，© .NET Foundation and Contributors）
- [Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon)（MIT，© Philipp Sumi）
- [.NET / WPF](https://dotnet.microsoft.com/)（MIT，© .NET Foundation and Contributors）

## 许可证

本项目基于 [MIT License](LICENSE.txt) 发布。

## Stars 历史

[![Star 历史](https://starchart.cc/XPSoftware/PointManager.svg?variant=adaptive)](https://starchart.cc/XPSoftware/PointManager)

<div align="center">

如果这个项目对您有帮助，请点亮 Star ⭐

</div>
