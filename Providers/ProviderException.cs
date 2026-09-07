namespace NativeTavern.Providers;

public sealed class ProviderException(string message, Exception? inner = null) : Exception(message, inner);
