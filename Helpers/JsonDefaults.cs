using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NativeTavern.Helpers;

public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Chinese roleplay text stays readable in exported files instead of turning into escape sequences.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
