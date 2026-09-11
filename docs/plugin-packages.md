# NativeTavern 插件包格式

NativeTavern 1.3 提供插件商城与插件包管理基础设施，但暂不加载或执行插件代码。该格式用于安全地分发、安装、更新、启停和卸载未来的扩展。

## 安装包

安装包扩展名为 `.ntplugin` 或 `.zip`，根目录必须包含 UTF-8 编码的 `plugin.json`：

```json
{
  "id": "author.example-plugin",
  "name": "Example Plugin",
  "version": "1.0.0",
  "author": "Author",
  "description": "A short description.",
  "homepage": "https://example.com",
  "minimumAppVersion": "1.3.0",
  "permissions": [],
  "capabilities": []
}
```

`id` 必须为 2–64 个字符，只能使用小写字母、数字、点、短横线和下划线。`version` 和 `minimumAppVersion` 使用可由 .NET `Version` 解析的版本号。

NativeTavern 不执行包内 DLL、脚本或可执行文件。插件可以通过 `capabilities` 请求应用提供的受控声明式能力；当前支持 `character-status-v1`（AI 动态角色状态）。未知能力会被保留但不会执行。`permissions` 用于向用户展示能力所需的数据访问。

## 商城目录

插件中心首次启动后会创建 `UserData/Plugins/catalog.json`。目录格式如下：

```json
{
  "formatVersion": 1,
  "plugins": [
    {
      "id": "author.example-plugin",
      "name": "Example Plugin",
      "version": "1.0.0",
      "author": "Author",
      "description": "A short description.",
      "homepage": "https://example.com",
      "packageUrl": "https://example.com/example.ntplugin",
      "sha256": "64-character lowercase or uppercase SHA-256"
    }
  ]
}
```

商城安装只接受 HTTPS 地址，并在安装前验证 SHA-256、插件 ID 和版本。默认目录为空，本项目不随应用提供具体插件或 Mod。

## 目录与安全限制

- 插件程序文件：`UserData/Plugins/<plugin-id>/`
- 插件持久数据预留目录：`UserData/PluginData/`
- 单个压缩包最大 100 MB，解压后最大 500 MB，最多 2,000 个条目
- 拒绝目录穿越、重复路径和符号链接
- 更新会先保留旧目录，只有新包完整就位后才替换
- 插件与插件数据会进入 NativeTavern 备份；旧版备份没有这些目录时仍可正常恢复
