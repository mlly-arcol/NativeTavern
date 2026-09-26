using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NativeTavern.ViewModels;
using Xunit;

namespace NativeTavern.Tests;

/// ChatViewModel is only built when a conversation is opened, so a dependency nobody registered would
/// show up as a crash in the middle of someone's writing session instead of during startup.
public class DependencyRegistrationTests
{
    public static TheoryData<Type> ViewModels => new()
    {
        typeof(ChatViewModel),
        typeof(MainViewModel),
        typeof(SettingsViewModel),
        typeof(CharactersViewModel),
        typeof(PromptStudioViewModel),
        typeof(KnowledgeViewModel),
        typeof(PromptInspectorViewModel),
        typeof(PluginsViewModel)
    };

    [Theory]
    [MemberData(nameof(ViewModels))]
    public void EveryConstructorDependencyIsRegistered(Type viewModelType)
    {
        var services = new ServiceCollection();
        App.ConfigureServices(services);

        var constructor = Assert.Single(viewModelType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        foreach (var parameter in constructor.GetParameters())
            Assert.True(IsRegistered(services, parameter.ParameterType),
                $"{viewModelType.Name} 需要 {parameter.ParameterType.Name}，但容器里没有注册。");
    }

    /// AddLogging and friends register open generics, so ILogger&lt;ChatViewModel&gt; is satisfied by a
    /// registration for ILogger&lt;&gt;.
    private static bool IsRegistered(IServiceCollection services, Type type) => services.Any(descriptor =>
        descriptor.ServiceType == type ||
        (type.IsGenericType && descriptor.ServiceType == type.GetGenericTypeDefinition()));
}
