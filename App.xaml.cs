using System.Configuration;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NativeTavern.Data;
using NativeTavern.Data.Repositories;
using NativeTavern.Helpers;
using NativeTavern.Logging;
using NativeTavern.Providers;
using NativeTavern.Security;
using NativeTavern.Services;
using NativeTavern.ViewModels;

namespace NativeTavern;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppPaths.EnsureCreated();
        var services = new ServiceCollection();
        ConfigureServices(services);
        _services = services.BuildServiceProvider();
        var logger = _services.GetRequiredService<ILogger<App>>();
        DispatcherUnhandledException += (_, args) =>
        {
            logger.LogCritical(args.Exception, "Unhandled UI exception.");
            MessageBox.Show("NativeTavern 遇到未处理错误，详情已写入日志。", "NativeTavern", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        try
        {
            logger.LogInformation("NativeTavern 1.1.0 starting.");
            await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
            var storedSettings = await _services.GetRequiredService<SettingsService>().LoadAsync();
            _services.GetRequiredService<LocalizationService>().SetLanguage(storedSettings.LanguageCode);
            var viewModel = _services.GetRequiredService<MainViewModel>();
            await viewModel.InitializeAsync();
            var window = _services.GetRequiredService<MainWindow>();
            _services.GetRequiredService<TrayService>().Initialize(() =>
            {
                window.Show(); window.WindowState = WindowState.Normal; window.Activate();
            });
            window.Show();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Application startup failed.");
            MessageBox.Show("NativeTavern 启动失败，请查看日志：" + Environment.NewLine + AppPaths.LogFile, "NativeTavern", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddProvider(new FileLoggerProvider(AppPaths.LogFile)));
        services.AddSingleton<DatabaseConnectionFactory>();
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<ChatSessionRepository>();
        services.AddSingleton<ChatMessageRepository>();
        services.AddSingleton<MessageSwipeRepository>();
        services.AddSingleton<SettingsRepository>();
        services.AddSingleton<CharacterRepository>();
        services.AddSingleton<PromptRepository>();
        services.AddSingleton<KnowledgeRepository>();
        services.AddSingleton<ChatAttachmentRepository>();
        services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<LocalizationService>();
        services.AddSingleton<Importers.CharacterCardImporter>();
        services.AddSingleton<CharacterService>();
        services.AddSingleton<PromptService>();
        services.AddSingleton<KnowledgeService>();
        services.AddSingleton<AttachmentService>();
        services.AddSingleton<ConversationSummaryService>();
        services.AddSingleton<TrayService>();
        services.AddHttpClient<OpenAICompatibleProvider>(client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddHttpClient<ClaudeProvider>(client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddSingleton<ProviderRouter>();
        services.AddSingleton<ProviderDiscoveryService>();
        services.AddSingleton<LocalModelService>();
        services.AddSingleton<ILLMProvider>(provider => provider.GetRequiredService<ProviderRouter>());
        services.AddSingleton<ChatService>();
        services.AddSingleton<ChatViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<CharactersViewModel>();
        services.AddSingleton<PromptStudioViewModel>();
        services.AddSingleton<KnowledgeViewModel>();
        services.AddSingleton<PromptInspectorViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }
}

