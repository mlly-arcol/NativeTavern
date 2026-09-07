# NativeTavern

一个面向个人使用的 Windows 原生 AI 角色聊天客户端。

## 当前版本：V0.3

当前仓库已实现基础 AI 对话、角色系统和可长期使用的聊天记录管理：

- .NET 10 + WPF + CommunityToolkit.Mvvm
- OpenAI Compatible Chat Completions
- SSE 流式回复与主动停止
- SQLite 会话、消息和设置持久化
- Windows DPAPI 加密 API Key
- Chat / Characters / Settings 三页深色界面
- 本地文件日志与常见 HTTP 错误提示
- 角色创建、编辑、删除、搜索、标签与收藏
- PNG Character Card V2/V3 与 JSON Character Card 导入
- 本地角色头像管理
- 角色 First Message
- 可选的角色上下文发送（默认关闭，需在 Settings 明确启用）
- 多聊天记录创建、切换和确认删除
- 单条消息编辑、复制和确认删除
- 助手回复 Regenerate 与多候选 Swipe
- Ctrl+Enter 重新生成、Esc 停止生成

开发构建：dotnet build -c Release

Windows x64 发布：dotnet publish -c Release -r win-x64 --self-contained false -o publish

运行后数据写入 %LOCALAPPDATA%\NativeTavern\。首次使用请在 Settings 中填写 Base URL、API Key 与模型名称，并先执行连接测试。

仓库根目录的 NativeTavern.exe 是可直接启动的单文件版本，需要目标电脑安装 .NET 10 Desktop Runtime。

## 一、项目定位

NativeTavern 的目标不是完整复刻 SillyTavern 的所有功能，也不是单纯将 SillyTavern 套壳成桌面程序。

项目定位是：

> 重新设计一个只保留高频核心功能、适合个人长期使用、深度适配 Windows 的本地 AI 角色聊天客户端。

重点解决以下问题：

- 不依赖浏览器使用
- 不依赖 Node.js 环境
- 不需要通过 localhost 打开网页
- 启动方式接近普通 Windows 软件
- 界面针对鼠标、键盘和桌面窗口重新设计
- 保留 SillyTavern 中最实用的角色卡、世界书、Persona、Prompt 和模型连接能力
- 尽可能兼容现有 SillyTavern 资源
- 本地保存聊天和配置
- 优先保证简单、稳定、可维护

本项目主要供个人使用，不以商业发行、大规模用户、多平台支持或插件生态为主要目标。

---

## 二、项目核心原则

### 1. Windows First

软件首先服务 Windows 桌面环境。

优先支持：

- Windows 11
- Windows 原生窗口
- Windows 文件拖放
- Windows 剪贴板
- Windows 文件选择器
- Windows 系统托盘
- Windows 通知
- Windows 快捷键
- Windows 本地文件系统
- DPAPI 凭据加密
- 本地模型自动发现

不优先考虑：

- Linux
- macOS
- Android
- iOS
- Web 版本

### 2. 个人使用优先

不为了未来可能存在的大规模商业需求提前增加复杂架构。

因此第一阶段不考虑：

- 用户注册
- 多账户
- 云同步
- SaaS
- 商业授权系统
- 多用户权限
- 在线插件商城
- 企业级审计系统
- 遥测
- 广告
- 复杂更新服务器

软件默认所有数据都属于当前 Windows 用户。

### 3. 保留 SillyTavern 的核心体验

项目重点保留以下使用逻辑：

- Character Card
- Persona
- Lorebook / World Info
- System Prompt
- Prompt Preset
- Chat History
- Swipe
- Regenerate
- Message Edit
- 多会话
- Token 设置
- Generation Parameters
- API Provider
- Streaming
- Markdown

而不是追求所有边缘功能 1:1 复制。

### 4. 优先兼容已有资源

尽量避免创建封闭格式。

优先支持导入：

- SillyTavern PNG Character Card
- Character Card JSON
- Character Card V2
- Character Card V3
- SillyTavern Lorebook
- Preset JSON
- Chat JSONL

未来条件允许时增加对应导出。

目标是让现有 SillyTavern 用户能够直接迁移大部分资源。

---

## 三、主要使用场景

### 场景一：角色聊天

用户启动 NativeTavern。

选择一个已经导入的角色。

选择模型。

直接开始聊天。

聊天过程中可以：

- 流式查看 AI 回复
- 停止生成
- 重新生成
- Swipe 切换回复
- 修改 AI 消息
- 修改用户消息
- 删除消息
- 复制消息
- 新建聊天分支

### 场景二：导入角色卡

用户可以直接将 `character.png` 拖入软件窗口。

程序自动识别角色卡 metadata。

显示：

- 角色名称
- 头像
- Description
- Personality
- Scenario
- First Message
- Example Messages
- Alternate Greetings
- Creator
- Tags

确认后导入本地角色库。

### 场景三：连接在线模型

用户可以添加：

- OpenAI
- OpenRouter
- Claude
- Gemini
- DeepSeek
- 自定义 OpenAI Compatible API

Provider 配置主要包括：

- API Base URL
- API Key
- Model
- Temperature
- Top P
- Max Tokens
- Context Length

API Key 使用 Windows DPAPI 加密保存。

### 场景四：连接本地模型

NativeTavern 自动检测常见本地服务。

包括：

- Ollama
- LM Studio
- KoboldCpp
- llama.cpp server
- 其他 OpenAI Compatible Server

检测到服务之后，可以直接显示可用模型。

### 场景五：世界书

用户可以创建多个 Lorebook。

每个 Lore Entry 包含：

- Name
- Keywords
- Secondary Keywords
- Content
- Priority
- Depth
- Enabled
- Constant
- Selective

Prompt 构建时，根据当前聊天内容动态激活对应条目。

### 场景六：Persona

用户可以维护多个用户 Persona。

不同聊天可以选择不同 Persona。

Persona 内容会被加入 Prompt。

---

## 四、核心功能范围

### 第一优先级

必须实现。

#### Character

- 创建角色
- 编辑角色
- 删除角色
- 搜索角色
- 标签
- 收藏
- 头像
- Character Card 导入

#### Chat

- 新建聊天
- 删除聊天
- Chat History
- 流式回复
- Stop
- Regenerate
- Swipe
- 编辑消息
- 删除消息
- 复制消息

#### Provider

至少支持：

- OpenAI Compatible
- OpenRouter
- Ollama
- LM Studio

之后再增加 Claude 和 Gemini 原生 Provider。

#### Prompt Engine

组合顺序：

```text
System Prompt
↓
Main Prompt
↓
Character
↓
Persona
↓
World Info
↓
Scenario
↓
Example Messages
↓
Chat History
↓
Author Note
↓
User Message
```

生成最终请求。

#### Lorebook

支持关键词激活。

#### Preset

保存：

- 模型
- Temperature
- Top P
- Max Tokens
- System Prompt
- Generation 参数

#### 本地数据保存

使用 SQLite。

---

## 五、第二阶段功能

在核心功能稳定之后增加。

包括：

- 群聊
- 聊天分支
- Prompt Inspector
- Token Counter
- RAG
- 文档知识库
- PDF/TXT/Markdown 导入
- 图片附件
- 图片生成
- TTS
- STT
- 自定义 CSS
- 主题
- 正则替换
- 快捷指令
- 自动摘要

---

## 六、暂不实现的功能

为了控制个人项目复杂度，早期明确不实现：

- 完整 SillyTavern Extension API
- 插件商城
- 多用户
- 在线账户
- 云端角色同步
- 内置社区
- 浏览器版本
- 手机版本
- Linux/macOS
- 企业权限系统
- Telemetry
- 商业支付系统

---

## 七、技术方案

### 开发语言

- C#

### Runtime

- .NET 10

### UI

推荐：

- WPF

原因：

- Windows 桌面环境成熟
- 稳定
- 开发资料多
- 与 C#/.NET 集成成熟
- 个人开发维护成本低
- 比较适合复杂桌面程序
- 容易接入 WebView2

UI 不追求完全传统 WPF 风格。

整体视觉采用现代化 Windows 桌面设计。

---

## 八、界面架构

主要页面：

```text
MainWindow
│
├── Chat
├── Characters
├── Lorebooks
├── Personas
├── Presets
└── Settings
```

推荐主窗口布局：

```text
┌─────────────────────────────────────────────┐
│ NativeTavern                            _ □ X│
├──────────────┬──────────────────────────────┤
│              │                              │
│ 💬 Chat       │                              │
│ 👤 Characters │          内容区域             │
│ 🌍 Lorebooks  │                              │
│ 🎭 Personas   │                              │
│ ⚙ Settings    │                              │
│              │                              │
└──────────────┴──────────────────────────────┘
```

---

## 九、聊天界面

聊天页面是整个程序最重要的界面。

每一条 AI 消息支持：

- Copy
- Edit
- Delete
- Regenerate
- Swipe Left
- Swipe Right

---

## 十、Markdown 渲染

聊天消息可能包含：

- Markdown
- Code Block
- Table
- Quote
- Spoiler
- HTML
- LaTeX

因此推荐采用：

- WPF + WebView2
- markdown-it
- KaTeX
- highlight.js

这样可以避免自己实现复杂富文本排版。

---

## 十一、项目结构

初期保持简单。

```text
NativeTavern/
│
├── App.xaml
├── MainWindow.xaml
│
├── Views/
│   ├── ChatView
│   ├── CharacterView
│   ├── LorebookView
│   ├── PersonaView
│   └── SettingsView
│
├── ViewModels/
│
├── Models/
│
├── Services/
│   ├── ChatService
│   ├── CharacterService
│   ├── LorebookService
│   └── PromptService
│
├── Providers/
│   ├── ILLMProvider
│   ├── OpenAIProvider
│   ├── OllamaProvider
│   ├── LMStudioProvider
│   └── OpenRouterProvider
│
├── Data/
│   ├── Database
│   ├── Repositories
│   └── Migrations
│
├── Prompts/
│
├── Importers/
│   ├── CharacterCardImporter
│   ├── LorebookImporter
│   └── ChatImporter
│
├── Security/
│
├── Utils/
│
└── Assets/
```

项目增长以后再进行拆分。

---

## 十二、MVVM

采用：

- CommunityToolkit.Mvvm

基本结构：

```text
View
↓
ViewModel
↓
Service
↓
Repository / Provider
```

---

## 十三、LLM Provider 架构

所有模型统一接口。

```csharp
public interface ILLMProvider
{
    string Id { get; }

    string DisplayName { get; }

    Task<IReadOnlyList<ModelInfo>> GetModelsAsync(
        CancellationToken cancellationToken);

    IAsyncEnumerable<string> StreamAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken);

    Task<bool> TestConnectionAsync(
        CancellationToken cancellationToken);
}
```

各 Provider 单独实现。

业务代码不得直接依赖具体 Provider。

---

## 十四、Prompt Engine

Prompt Engine 独立于 UI 和具体模型。

主要负责：

- 角色信息
- Persona
- World Info
- Chat History
- Author Note
- System Prompt
- Token Budget
- Prompt 顺序
- 上下文裁剪

输入：

```text
Character
Persona
Lorebook
Chat
Preset
Current Message
```

输出：

```text
ChatCompletionRequest
```

Prompt Engine 是整个项目的核心模块之一。

---

## 十五、数据库

数据库：

- SQLite

主要表：

```text
Characters
Chats
Messages
MessageSwipes
Personas
Lorebooks
LoreEntries
Presets
Providers
Models
Settings
Attachments
```

文件类数据，例如头像、背景、生成图片、附件，不直接放数据库。

默认存放：

```text
%LOCALAPPDATA%\NativeTavern\
```

目录：

```text
NativeTavern
├── Data
├── Avatars
├── Attachments
├── Images
├── Backgrounds
├── Cache
└── Logs
```

---

## 十六、数据访问

个人项目优先简单。

推荐：

- SQLite + Dapper

---

## 十七、安全

API Key 不保存为明文。

推荐：

- DPAPI

处理流程：

```text
用户输入 API Key
↓
DPAPI 加密
↓
SQLite
```

日志中禁止输出 API Key。

---

## 十八、本地模型

程序启动后尝试检测：

- Ollama
- LM Studio
- KoboldCpp

允许用户关闭自动扫描。

---

## 十九、RAG

第一版不实现。

后续需要时：

- SQLite + sqlite-vec

---

## 二十、日志

使用：

- Microsoft.Extensions.Logging

日志写入：

```text
%LOCALAPPDATA%\NativeTavern\Logs
```

主要记录：

- 启动异常
- 数据库异常
- Provider 网络错误
- Character Card 解析错误
- Prompt 构建异常

默认不记录完整私人聊天内容。

---

## 二十一、发布方式

个人使用阶段不强制制作安装程序。

首先使用：

```text
dotnet publish
```

发布为 Windows x64 应用。

后续如有需要再增加：

- Velopack
- MSIX

---

## 二十二、版本开发计划

### V0.1

目标：

让 AI 真正回复。

实现：

- WPF 主窗口
- Chat UI
- OpenAI Compatible Provider
- 流式输出
- 设置
- 简单 SQLite

### V0.2

目标：

角色系统可用。

实现：

- Character
- 角色头像
- 创建/编辑角色
- PNG Character Card
- JSON Character Card
- First Message

### V0.3

目标：

可以长期聊天。

实现：

- Chat History
- 多聊天
- 消息编辑
- 删除
- Regenerate
- Swipe

状态：已完成。

### V0.4

目标：

拥有基本 SillyTavern 使用体验。

实现：

- Persona
- Lorebook
- Prompt Preset
- Author Note
- Prompt Engine

### V0.5

增加模型生态。

实现：

- Ollama
- LM Studio
- OpenRouter
- Claude
- Gemini

### V0.6

改善使用体验。

实现：

- Token Counter
- Prompt Inspector
- 文件拖放
- Windows 通知
- 系统托盘
- 快捷键

### V0.7

高级能力。

实现：

- RAG
- 文档知识库
- 图片附件
- 自动摘要

### V1.0

目标：

成为可以完全替代本人日常 SillyTavern 使用的 Windows 客户端。

V1.0 不要求拥有 SillyTavern 的所有功能。

判断是否完成的标准是：

> 自己日常使用过程中已经不再需要打开 SillyTavern。

---

## 二十三、项目最终目标

NativeTavern 不追求成为功能最多的 AI 聊天软件。

最终目标是：

> 一个启动快速、界面干净、完全本地、兼容 SillyTavern 资源、适合 Windows 桌面环境，并且拥有足够角色扮演能力的私人 AI 客户端。

核心价值不是功能数量，而是：

```text
简单
稳定
本地
兼容
易维护
Windows 原生体验
```

项目的成功标准不是与 SillyTavern 功能数量完全一致，而是能够稳定覆盖个人真正使用的功能，并且使用体验比通过浏览器运行 SillyTavern 更自然。
