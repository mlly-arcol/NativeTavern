using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace NativeTavern.Services;

public sealed class LocalizationService
{
    public const string Chinese = "zh-CN";
    public const string English = "en-US";

    private static readonly IReadOnlyDictionary<string, string> EnglishToChinese =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Local AI workspace"] = "本地 AI 工作区",
            ["New chat"] = "新建对话",
            ["WORKSPACE"] = "工作区",
            ["◉   Chat"] = "◉   对话",
            ["♙   Characters"] = "♙   角色",
            ["⌁   Prompt Studio"] = "⌁   提示词工作室",
            ["▤   Knowledge Base"] = "▤   知识库",
            ["⌕   Prompt Inspector"] = "⌕   提示词检查器",
            ["⚙   Settings"] = "⚙   设置",
            ["Settings"] = "设置",
            ["Interface Language"] = "界面语言",
            ["Provider"] = "模型服务商",
            ["Clear"] = "清除",
            ["Model"] = "模型",
            ["Discover Models"] = "获取模型列表",
            ["Temperature"] = "温度",
            ["Max Tokens"] = "最大生成长度",
            ["Context Length"] = "上下文长度",
            ["Character Context"] = "角色上下文",
            ["Knowledge Context"] = "知识库上下文",
            ["Image Context"] = "图片上下文",
            ["Local Models"] = "本地模型",
            ["GGUF model files"] = "GGUF 模型文件",
            ["Start and Connect"] = "启动并连接",
            ["Running local services"] = "运行中的本地服务",
            ["Include the selected character card and First Message in model requests"] = "在模型请求中包含所选角色卡和首条消息",
            ["Allow matched knowledge-base excerpts to be sent in model requests"] = "允许将匹配的知识库片段发送给模型",
            ["Allow attached images to be sent in model requests"] = "允许将附加图片发送给模型",
            ["Scan local services at startup"] = "启动时扫描本地模型服务",
            ["Scan Local"] = "扫描本地服务",
            ["Use Selected"] = "使用所选服务",
            ["Test Connection"] = "测试连接",
            ["Save"] = "保存",
            ["Reference context window; local server context size is configured by the server"] = "参考上下文窗口；本地服务的上下文大小由服务器配置",
            ["Characters"] = "角色",
            ["Create, import and manage character cards"] = "创建、导入和管理角色卡",
            ["Import Card"] = "导入角色卡",
            ["+ New Character"] = "+ 新建角色",
            ["Search name, tags or description"] = "搜索名称、标签或描述",
            ["Favorites only"] = "仅显示收藏",
            ["Choose Avatar"] = "选择头像",
            ["Favorite"] = "收藏",
            ["Name *"] = "名称 *",
            ["Name"] = "名称",
            ["Creator"] = "创建者",
            ["Tags"] = "标签",
            ["Description"] = "描述",
            ["Personality"] = "性格",
            ["Scenario"] = "场景",
            ["First Message"] = "首条消息",
            ["Example Messages"] = "示例消息",
            ["Start Chat"] = "开始对话",
            ["Delete"] = "删除",
            ["Prompt Studio"] = "提示词工作室",
            ["Manage personas, lorebooks and reusable prompt presets."] = "管理人设、世界书和可复用的提示词预设。",
            ["Manage personas, lorebooks and prompt presets used to shape each conversation."] = "管理用于塑造每次对话的人设、世界书和提示词预设。",
            ["Personas"] = "人设",
            ["Persona"] = "人设",
            ["Choose a voice for your conversations"] = "为对话选择一种表达风格",
            ["Persona library"] = "人设库",
            ["Reusable author voices"] = "可复用的作者口吻",
            ["+  New persona"] = "+  新建人设",
            ["Persona details"] = "人设详情",
            ["Define how this persona speaks and behaves."] = "定义该人设的说话方式和行为。",
            ["Define a reusable voice or point of view."] = "定义可复用的口吻或视角。",
            ["Persona content"] = "人设内容",
            ["Delete persona"] = "删除人设",
            ["Save persona"] = "保存人设",
            ["Lorebooks"] = "世界书",
            ["Lorebook"] = "世界书",
            ["Lorebook settings"] = "世界书设置",
            ["Lorebook library"] = "世界书库",
            ["Keyword-triggered context"] = "由关键词触发的上下文",
            ["+  New lorebook"] = "+  新建世界书",
            ["Lorebook details"] = "世界书详情",
            ["Create entries that activate when keywords appear."] = "创建在关键词出现时自动激活的条目。",
            ["Enabled"] = "已启用",
            ["Entries"] = "条目",
            ["+ New entry"] = "+ 新建条目",
            ["Keywords"] = "关键词",
            ["Entry content"] = "条目内容",
            ["Entry details"] = "条目详情",
            ["Entry name"] = "条目名称",
            ["Primary keywords"] = "主要关键词",
            ["Secondary keywords"] = "次要关键词",
            ["Comma separated"] = "使用英文逗号分隔",
            ["Content"] = "内容",
            ["Priority"] = "优先级",
            ["Depth"] = "深度",
            ["Constant"] = "始终启用",
            ["Selective"] = "选择性触发",
            ["Delete entry"] = "删除条目",
            ["Save entry"] = "保存条目",
            ["Prompt presets"] = "提示词预设",
            ["Prompt preset"] = "提示词预设",
            ["Reusable model instructions"] = "可复用的模型指令",
            ["+  New preset"] = "+  新建预设",
            ["Preset details"] = "预设详情",
            ["Configure instructions and optional model overrides."] = "配置指令和可选的模型参数覆盖。",
            ["System prompt"] = "系统提示词",
            ["Main prompt"] = "主提示词",
            ["Model override"] = "指定模型",
            ["Max tokens"] = "最大生成长度",
            ["Leave overrides blank to use the values from Settings."] = "留空则使用设置页中的参数。",
            ["Save preset"] = "保存预设",
            ["Knowledge Base"] = "知识库",
            ["Drop or import TXT, Markdown and PDF files. Context sharing stays disabled until enabled in Settings."] = "拖放或导入 TXT、Markdown 和 PDF 文件。只有在设置中启用后才会共享上下文。",
            ["Import Documents"] = "导入文档",
            ["Enable / Disable Selected"] = "启用 / 禁用所选项",
            ["Prompt Inspector"] = "提示词检查器",
            ["Inspect the exact text context before sending it to your Provider."] = "检查发送给模型服务商之前的完整文本上下文。",
            ["Refresh"] = "刷新",
            ["Estimated tokens: "] = "预估 token：",
            ["Lore: "] = "世界书：",
            ["Local workspace"] = "本地工作区",
            [" tokens"] = " token",
            ["Switch conversation"] = "切换对话",
            ["Inspect prompt"] = "检查提示词",
            ["Delete conversation"] = "删除对话",
            ["Author note"] = "作者备注",
            ["Apply"] = "应用",
            ["今天想聊些什么？"] = "今天想聊些什么？",
            ["选择角色或直接输入消息，开始一段新的对话"] = "选择角色或直接输入消息，开始一段新的对话",
            ["Connect a model to start chatting"] = "连接模型后开始对话",
            ["Configure a local or cloud provider in Settings."] = "请先在设置中配置本地或云端模型服务。",
            ["Open settings"] = "打开设置",
            ["Copy"] = "复制",
            ["Edit"] = "编辑",
            ["Regenerate"] = "重新生成",
            ["Cancel"] = "取消",
            ["Message NativeTavern · Enter to send · Shift+Enter for new line"] = "给 NativeTavern 发送消息 · Enter 发送 · Shift+Enter 换行",
            ["给 NativeTavern 发送消息"] = "给 NativeTavern 发送消息",
            ["Attach image"] = "添加图片",
            ["Stop"] = "停止",
            ["Send"] = "发送",
            ["NativeTavern can make mistakes. Check important information."] = "NativeTavern 可能会出错，请核查重要信息。"
        };

    private static readonly IReadOnlyDictionary<string, string> ChineseToEnglish = BuildChineseToEnglish();

    private bool _applying;

    public string CurrentLanguage { get; private set; } = Chinese;
    public event Action? LanguageChanged;

    public LocalizationService()
    {
        EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnElementLoaded));
    }

    public void SetLanguage(string? languageCode)
    {
        CurrentLanguage = Normalize(languageCode);
        ApplyToAllWindows();
        LanguageChanged?.Invoke();
    }

    public string Text(string chinese, string english) => CurrentLanguage == English ? english : chinese;

    public static string Normalize(string? languageCode) =>
        string.Equals(languageCode, English, StringComparison.OrdinalIgnoreCase) ? English : Chinese;

    private static IReadOnlyDictionary<string, string> BuildChineseToEnglish()
    {
        var translations = EnglishToChinese.GroupBy(x => x.Value)
            .ToDictionary(x => x.Key, x => x.First().Key, StringComparer.Ordinal);
        translations["今天想聊些什么？"] = "What would you like to talk about?";
        translations["选择角色或直接输入消息，开始一段新的对话"] = "Choose a character or type a message to start a new conversation.";
        translations["给 NativeTavern 发送消息"] = "Message NativeTavern";
        return translations;
    }

    private void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DependencyObject element || _applying) return;
        element.Dispatcher.BeginInvoke(() => Apply(element));
    }

    private void ApplyToAllWindows()
    {
        if (Application.Current is null) return;
        foreach (Window window in Application.Current.Windows) Apply(window);
    }

    private void Apply(DependencyObject root)
    {
        if (_applying) return;
        _applying = true;
        try { ApplyRecursive(root); }
        finally { _applying = false; }
    }

    private void ApplyRecursive(DependencyObject element)
    {
        switch (element)
        {
            case TextBlock textBlock when !BindingOperations.IsDataBound(textBlock, TextBlock.TextProperty):
                textBlock.Text = Translate(textBlock.Text);
                break;
            case HeaderedContentControl headered when headered.Header is string header:
                headered.Header = Translate(header);
                break;
            case ContentControl contentControl when contentControl.Content is string content:
                contentControl.Content = Translate(content);
                break;
        }

        if (element is FrameworkElement frameworkElement && frameworkElement.ToolTip is string tooltip)
            frameworkElement.ToolTip = Translate(tooltip);

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
            ApplyRecursive(VisualTreeHelper.GetChild(element, index));
    }

    private string Translate(string value)
    {
        if (CurrentLanguage == Chinese)
            return EnglishToChinese.TryGetValue(value, out var chinese) ? chinese : value;
        return ChineseToEnglish.TryGetValue(value, out var english) ? english : value;
    }
}
