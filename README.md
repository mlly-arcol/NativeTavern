# NativeTavern

NativeTavern 是一款面向个人使用的 Windows 原生 AI 角色聊天客户端。它使用 WPF 构建，不依赖浏览器或 Node.js，支持在线 API、本地模型、角色卡、世界书、Persona、提示词预设、知识库、多角色群聊和插件包管理。

当前版本：**2.0.0**

[下载最新版本](https://github.com/mlly-arcol/NativeTavern/releases/latest)

## 主要功能

### 角色与分组

- 创建、编辑、删除和收藏角色
- 按名称、标签或描述搜索角色
- 为角色设置本地头像
- 导入 PNG Character Card V2/V3 和 JSON 角色卡
- 使用角色卡中的 Description、Personality、Scenario、First Message 和 Example Messages
- 创建、重命名和删除角色分组
- 通过复选框调整分组成员
- 折叠或展开分组；已分组角色不再重复出现在未分组列表
- 删除分组只会解除角色归属，不会删除角色

每个角色最多属于一个角色分组。

### 单角色聊天

- 为指定角色创建独立会话
- 流式显示模型回复，可随时停止生成
- 编辑、复制和删除单条消息
- 重新生成助手回复
- 保存并左右切换多个候选回复（Swipe）
- 可在任意消息处创建独立聊天分支，并从分支返回源对话
- 可直接重命名当前对话
- 切换或删除历史会话
- First Message 自动作为角色的首条消息显示
- 长会话达到 20 条消息后生成本地早期上下文摘要，并保留最近消息

### 多角色群聊

- 从至少包含两个角色的分组创建群聊
- 创建时保存独立的成员快照；之后修改原分组不会影响已有群聊
- 在群聊中独立修改标题和成员
- 每条助手消息显示实际发言角色的名称和头像
- 发送用户消息后，根据角色名称、标签和最近发言情况选择发言角色
- 允许同一角色连续发言，但会降低连续选中的概率
- 点击“下一位”可自动选择或手动指定角色，在没有新用户消息时继续生成一条回复
- 请求中包含群聊成员资料，并要求模型只扮演本轮选中的角色

群聊发言者选择是本地启发式规则，不会为了选择发言者额外调用一次模型。

### 对话上下文

聊天页顶部的“对话设置”用于配置当前会话：

- **Persona**：用户身份、口吻或视角
- **Lorebook**：按关键词激活的世界信息
- **Prompt Preset**：System Prompt、Main Prompt、模型和生成参数覆盖
- **Author Note**：仅作用于当前会话的补充指令

点击“应用”保存当前配置；点击“清除”解除该会话的全部上下文绑定；关闭或取消弹窗不会保存修改。

“提示词检查器”可查看最终消息结构、估算 Token 数和已激活的 Lorebook 条目。

### Prompt Studio

- 创建、编辑和删除 Persona
- 创建、启用、禁用和删除 Lorebook
- 管理 Lore Entry 的关键词、次级关键词、优先级、扫描深度和激活方式
- 创建 Prompt Preset，并覆盖模型、Temperature、Top P 和 Max Tokens

Lore Entry 支持：

- 普通关键词触发
- Constant 始终启用
- Selective 主关键词与次级关键词联合触发
- Priority 排序
- Depth 控制检索最近多少条聊天消息

### 模型服务

内置以下配置模板：

- OpenAI
- OpenRouter
- DeepSeek
- Claude（Anthropic Messages API）
- Gemini OpenAI compatibility API
- 自定义 OpenAI-compatible API
- Ollama
- LM Studio
- llama.cpp server

设置页支持：

- Base URL、API Key 和模型名称
- 获取远程或本地服务的模型列表
- 连接测试
- Temperature、Top P、Max Tokens 和参考 Context Length
- 扫描正在运行的 Ollama、LM Studio 和 llama.cpp 服务
- 中文与 English 界面切换

API Key 使用 Windows DPAPI 加密，通常只能由保存它的同一 Windows 用户解密。

### 本地 GGUF 模型

NativeTavern 可以扫描指定目录顶层的 `.gguf` 文件，并启动外部 `llama.cpp` server。

默认位置：

```text
NativeTavern/
├── llama.cpp/
│   └── llama-server.exe
└── LocalModels/
    └── Qwen3-8B-Q4_K_M.gguf
```

默认 llama.cpp 启动配置为：

- 地址：`127.0.0.1:8080`
- Context Size：8192
- GPU Layers：35
- Jinja 模板：启用
- 模型别名：`NativeTavern-Qwen3`

`llama-server.exe` 和 GGUF 模型不包含在 GitHub Release 中，需要用户自行准备，也可以在设置页改为其他路径。

### 本地知识库

- 导入 TXT、Markdown 和 PDF 文件
- 单个文件最大 20 MB
- 文档复制到应用数据目录后分块保存
- 启用或禁用单个文档
- 根据当前用户消息进行本地关键词检索，最多选取 4 个匹配片段

当前知识库使用本地关键词匹配，不是向量数据库或语义嵌入检索。

### 图片附件

- 支持 PNG、JPG/JPEG、WEBP 和 GIF
- 单张图片最大 10 MB
- 可通过按钮选择、拖入输入框或拖入应用窗口
- 附件保存在本地 `UserData/Attachments`
- 只有在设置中启用“图片上下文”后，图片才会编码并发送给模型

### 数据导出

- 角色页可将当前角色导出为 Character Card V3 JSON
- 导出的角色卡可重新导入 NativeTavern，也可用于兼容 V3 角色卡的工具
- 聊天页可将当前对话导出为便于阅读的 Markdown 或结构化 JSON
- 单聊与群聊导出都会保存时间信息；群聊会保留每条回复的实际发言角色
- 导出采用同目录临时文件替换，失败或取消时不会留下半写入文件

### 桌面体验

- Windows 系统托盘
- 模型回复完成通知
- 主窗口淡入、轻微缩放和位移动画
- 遵循 Windows“在 Windows 中显示动画”的系统设置
- 中文和 English 界面
- 角色卡、图片和知识库文档拖放

### 插件中心

- 插件商城与已安装插件分栏
- 按名称、ID、作者或描述搜索
- 导入 `.ntplugin` 或 `.zip` 安装包，也可直接拖放安装
- 启用、停用、升级替换和卸载插件
- 商城下载强制使用 HTTPS，并校验 SHA-256、插件 ID 与版本
- 限制压缩包大小、文件数量和解压体积，拦截目录穿越、重复路径与符号链接
- 插件程序文件与持久数据分离，卸载不会删除插件数据

当前版本只提供插件包和商城管理基础设施，暂不加载或执行插件代码，也不随应用附带具体 Mod。插件包及商城目录规范见 [`docs/plugin-packages.md`](docs/plugin-packages.md)。

## 快速开始

1. 从 [GitHub Releases](https://github.com/mlly-arcol/NativeTavern/releases) 下载 `NativeTavern.exe`。
2. 将 EXE 放进一个可写的独立文件夹。程序会在它旁边创建 `UserData`，建议不要直接放在临时目录。
3. 启动程序并打开“设置”。
4. 选择 Provider，填写 Base URL、API Key（如需要）和模型名称。
5. 点击“测试连接”，成功后点击“保存”。
6. 前往“角色”创建或导入角色，然后点击“开始对话”。

使用本地服务时，可以先启动 Ollama、LM Studio 或 llama.cpp，再在设置页点击“扫描本地服务”；也可以选择 GGUF 文件后由 NativeTavern 启动 llama.cpp。

## 创建群聊

1. 在“角色”页面点击“创建分组”。
2. 输入分组名称，并勾选至少两个角色。
3. 保存后，在分组卡片上点击“群聊”。
4. 输入消息，系统会自动选择一个角色回复。
5. 使用聊天页的“成员”修改群聊标题或成员，使用“下一位”继续角色间的对话。

## 数据与备份

所有主要数据保存在 EXE 所在目录旁的 `UserData`：

```text
UserData/
├── Data/
│   └── NativeTavern.db
├── Avatars/
├── Attachments/
├── Documents/
├── Plugins/
├── PluginData/
├── Cache/
└── Logs/
    └── NativeTavern.log
```

- SQLite 数据库保存角色、分组、会话、消息、Swipe、Persona、Lorebook、Preset 和设置。
- 头像、附件及知识库原始文件以普通文件形式保存。
- 备份时请先退出 NativeTavern，然后复制整个 `UserData` 文件夹。
- 不要只复制数据库，否则头像和附件可能缺失。
- 从旧版本迁移且当前目录还没有数据库时，程序会尝试从旧的 `%LOCALAPPDATA%\NativeTavern` 或 Codex 沙盒数据目录复制旧数据。

设置页提供“一键创建备份”和“恢复备份”：

- 备份文件为 ZIP，包含数据库、头像、附件、知识库、插件和插件数据。
- 恢复前会校验备份数据库完整性。
- 恢复前自动创建一份当前数据的安全备份。
- 恢复完成后应用会关闭，需要手动重新启动。

在源码仓库中，从根目录或 `publish` 目录启动都会共用项目根目录的同一份 `UserData`，避免产生重复数据目录。

## 隐私设置

下列内容默认不会自动加入模型请求，必须在设置页明确启用：

- 角色卡上下文
- 本地知识库匹配片段
- 图片附件数据

Persona、Lorebook、Prompt Preset 和 Author Note 属于用户主动绑定到当前会话的提示词资源。使用在线 Provider 时，实际加入请求的内容会发送给对应服务商；发送前可使用“提示词检查器”查看文本结构。

## 快捷键

| 快捷键 | 功能 |
| --- | --- |
| `Ctrl+N` | 新建对话 |
| `Ctrl+Shift+P` | 打开提示词检查器 |
| `Ctrl+,` | 打开设置 |
| `Enter` | 发送消息 |
| `Shift+Enter` | 输入换行 |
| `Ctrl+Enter` | 重新生成最后一条助手消息 |
| `Esc` | 停止当前生成 |

快捷键在 NativeTavern 窗口获得焦点时生效，不是系统级全局热键。

## 系统要求

使用 Release 中的自包含版本：

- Windows x64
- 无需单独安装 .NET Desktop Runtime
- 在线模型需要网络连接和相应服务凭据
- 本地模型的内存、显存和磁盘要求取决于所选 GGUF 模型

从源码构建：

- Windows
- .NET 10 SDK
- Git

## 从源码构建

```powershell
git clone https://github.com/mlly-arcol/NativeTavern.git
cd NativeTavern
dotnet restore
dotnet build NativeTavern.csproj -c Release
```

运行测试：

```powershell
dotnet test Tests\NativeTavern.Tests\NativeTavern.Tests.csproj
```

生成 Windows x64 自包含单文件版本：

```powershell
dotnet publish NativeTavern.csproj -c Release -r win-x64 --self-contained true -o publish
```

发布完成后，构建目标会自动把 `publish/NativeTavern.exe` 同步为项目根目录的 `NativeTavern.exe`。该大型构建产物已被 Git 忽略，正式二进制文件通过 GitHub Releases 分发。

## 技术栈

- C# / .NET 10
- WPF
- CommunityToolkit.Mvvm
- SQLite + Dapper
- Microsoft.Extensions.DependencyInjection / Http / Logging
- Windows DPAPI
- PdfPig

核心分层：

```text
Views → ViewModels → Services → Repositories / Providers
```

## 当前限制

- 仅支持 Windows x64，没有 Web、Linux、macOS 或移动版本。
- 聊天消息支持常用 Markdown（标题、粗体、斜体、列表、引用、链接、行内代码与代码块）；暂不包含代码语法高亮、LaTeX、HTML 和远程图片渲染。
- 当前没有云同步或多账户。
- 插件中心目前只负责插件包管理；插件运行时 API、权限授权和代码加载尚未开放。
- 知识库是关键词检索，不是向量 RAG。
- Release 是便携式单文件程序，没有安装器和在线自动更新器。
- 本地发布时自动替换根目录 EXE 是开发构建步骤，不是客户端在线更新功能。

## V2.0.0 更新内容

- 新增任意消息处创建聊天分支，分支保留当时的消息、Swipe、群聊成员和图片附件
- 保存分支来源关系，可从分支快速返回仍然存在的源对话
- 删除源对话后，分支继续独立保留，不产生悬空引用
- 新增当前对话标题编辑
- 对话导出增加分支元数据，并改为原子文件写入
- 为聊天输入、发送、停止、导出、删除和提示词检查等关键控件补充辅助功能名称
- 增加分支生命周期、数据库迁移、导出替换等回归测试
- 冻结 2.0 产品范围；后续以缺陷修复、安全更新和小型体验改进为主

架构和维护边界见 [`docs/architecture.md`](docs/architecture.md)。

## V1.4.0 更新内容

- 新增 Character Card V3 JSON 角色卡导出
- 新增当前聊天记录的 Markdown 和 JSON 导出
- 群聊导出保留每条助手消息的实际角色名称
- 导出文件名会自动替换 Windows 不允许使用的字符
- 增加角色卡往返兼容和群聊导出测试

## V1.3.0 更新内容

- 新增插件中心，包含插件商城和已安装插件管理
- 支持导入、拖放安装、启停、升级替换与卸载插件包
- 新增 HTTPS 商城下载、SHA-256、插件身份和版本校验
- 加入压缩包路径、大小、条目数量与解压体积安全限制
- 插件程序与持久数据分目录保存，并纳入备份和恢复
- 发布插件包与商城目录格式文档；默认商城为空，不附带具体 Mod

## V1.2.3 更新内容

- 修复本地模型启动失败或取消后残留后台进程的问题
- 删除消息或会话时同步清理其图片附件
- 修复角色头像、知识库导入失败时可能残留孤儿文件的问题
- 修复快速切换会话时旧加载结果覆盖当前会话的问题
- 修复聊天视图消息事件未完整解除导致的内存泄漏
- 生成取消或服务立即失败时不再保留空白助手消息
- Provider Base URL 仅接受 HTTP/HTTPS，附件读取限定在受管理目录内
- 备份改为原子写入，恢复时校验归档并清除不属于备份的旧文件
- 本地 GGUF 启动现在使用设置中的上下文长度

## V1.2.2 更新内容

- 聊天消息支持常用 Markdown 排版
- 代码块和行内代码使用等宽字体显示
- Markdown 渲染针对模型流式输出做了防抖处理
- 外部链接仅允许通过 HTTP/HTTPS 打开

## V1.2.1 更新内容

- 一键备份和恢复 `UserData`
- 恢复前数据库完整性检查与自动安全备份
- 群聊“下一位”支持自动选择或手动指定角色
- GitHub Actions 自动测试、构建、生成 SHA-256 并发布 EXE

## V1.2 更新内容

- 角色分组与折叠显示
- 分组成员多选、重命名和安全删除
- 多角色群聊与独立成员快照
- 自动选择发言角色及“下一位”回复
- 群聊标题和成员管理
- 对话上下文设置弹窗
- 主窗口启动动画
- 中英文界面文本补充
- 单一 `UserData` 开发路径
- 发布后自动更新本地启动文件

## 项目定位

NativeTavern 不是 SillyTavern 的完整复刻，也不追求覆盖所有扩展能力。它专注于一个范围更小的目标：提供干净、原生、可离线保存数据，并适合个人长期使用的 Windows 角色聊天工作区。

2.0 是这一目标下的功能完整版本。插件代码执行、云同步、多账户和跨平台客户端不属于当前核心范围。
