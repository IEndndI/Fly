# 飞 Fly

一部短篇视觉小说（FVN）。一个想飞的孩子，和他醒来以后的事。

> 此故事出自于作者的梦。故事与文本、立绘主要由人类创作；画面、动画、程序、音乐由 Claude Opus 5.5 完成，DeepSeek 协助文字校对与部分程序。整体而言，AI 参与约 60%。
>
> 完整的制作过程（剧本流程、动态分镜、和 AI 的对话记录）公开在 [IEndndI/Fly-MakingOf](https://github.com/IEndndI/Fly-MakingOf)。

- 引擎：Unity 2022.3（UGUI + TextMeshPro），**没有使用任何第三方 VN 插件**，整套对话系统是自己搭的
- 篇幅：约 20–30 分钟，五章，两个结局
- 平台：Windows / 安卓 / Mac

## 下载与游玩

到 [Releases](../../releases) 下载压缩包，解压后运行 `Fly.exe`。

| 操作 | 按键 |
|---|---|
| 下一句 | 鼠标左键 / 空格 / 回车 |
| 系统页（存档、读档、回想、设置） | Esc / 右键 |
| 开场视频、结局视频 4 倍速 | 长按 |
| 飞行段 1.5 倍速 | 长按 |

## 项目结构

```
Assets/
├─ Script/            运行时脚本（对话系统、演出、界面都在这里）
│  └─ Legacy/         早期练习留下的脚本，主系统不用
├─ Editor/            编辑器小工具：表格转 txt、提取汉字、生成立绘索引
├─ Data/
│  ├─ Fly.xlsx        剧本表格（改剧本改这里，再用菜单 Tools/表格转 txt）
│  ├─ Icon/           游戏图标
│  └─ Resources/      运行时加载的东西
│     ├─ Fly.txt      剧本（由 Fly.xlsx 转出来）
│     ├─ About.txt    标题「关于」页的文字（直接改，支持 TMP 富文本）
│     ├─ Sprites/     背景、立绘、界面
│     ├─ Audio/       音乐、音效
│     └─ Video/       开场、飞行段、两个结局
├─ Fonts/             字体与 TMP 字体资源
├─ Scenes/            Title（标题）、SampleScene（正篇）
└─ _Practice/         学习阶段的练习素材
```

### 剧本表格的列

| 列 | 内容 |
|---|---|
| flow | `label:名字` / `jump:名字` / `option:按钮文字>标签` / `end:a` |
| 名字 | 说话人，留空沿用 |
| 台词 | `{w}` 停住等点击，`{nw}` 打完自动下一句 |
| 立绘 / 背景 / 隐藏 | 资源名 |
| effect | `shake` `flash` `se:音效` `wait:秒` `flight:seg(a,b)` 等，逗号并列 |
| BGM / 环境音 | `名字:vol(0.35):out(3):in(3)` |

## 自己打包

1. 用 Unity 2022.3.62 打开本文件夹
2. File → Build Settings（生成设置）→ 确认 `Title`、`SampleScene` 两个场景都勾上
3. 选 Windows，Build

## 鸣谢

- 字体：HarmonyOS Sans SC、霞鹜文楷、站酷快乐体
- 制作：Endnd × Claude Opus 5.5（DeepSeek 协助文字校对与部分程序），2026 年国庆

## 许可

[CC BY-NC 4.0](LICENSE)：署名、非商业使用即可自由分享和改编（代码与素材都包括在内；字体除外，各自遵循原许可）。
