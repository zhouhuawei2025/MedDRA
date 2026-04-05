using System.Text.Json;
using System.Text.RegularExpressions;

namespace MedDRA_Backhend.Infrastructure.AI;

public static class AiJsonParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static bool TryDeserializeFromAiText<T>(string rawText, out T? result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return false;
        }

        var cleanedText = rawText.Trim('\r', '\n', '\t', ' ', '　', '"', '\'', '`', ':', '-', '=');
        if (TryExtractJson(cleanedText, out var jsonText) == false)
        {
            return false;
        }

        try
        {
            result = JsonSerializer.Deserialize<T>(jsonText, JsonOptions);
            return result is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryExtractJson(string text, out string jsonText)
    {
        var objectMatch = Regex.Match(text, @"\{(?:[^{}]|(?<open>\{)|(?<-open>\}))*(?(open)(?!))\}", RegexOptions.Singleline);
        if (objectMatch.Success)
        {
            jsonText = objectMatch.Value;
            return true;
        }

        var arrayMatch = Regex.Match(text, @"\[(?:[^\[\]]|(?<open>\[)|(?<-open>\]))*(?(open)(?!))\]", RegexOptions.Singleline);
        if (arrayMatch.Success)
        {
            jsonText = arrayMatch.Value;
            return true;
        }

        jsonText = string.Empty;
        return false;
    }
}
