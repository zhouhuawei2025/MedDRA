using System.Text.Json;
using MedDRA_Backhend.Domain;

namespace MedDRA_Backhend.Infrastructure.Qdrant;

public static class QdrantRestPayloadMapper
{
    public static MedDraTerm ToMedDraTerm(IReadOnlyDictionary<string, object?> payload)
    {
        return new MedDraTerm
        {
            LltCode = ReadString(payload, "llt_code"),
            LltName = ReadString(payload, "llt_name"),
            PtCode = ReadString(payload, "pt_code"),
            PtName = ReadString(payload, "pt_name"),
            HltCode = ReadString(payload, "hlt_code"),
            Hlt = ReadString(payload, "hlt"),
            HgltCode = ReadString(payload, "hglt_code"),
            Hglt = ReadString(payload, "hglt"),
            SocCode = ReadString(payload, "soc_code"),
            Soc = ReadString(payload, "soc"),
            SearchText = ReadString(payload, "search_text"),
            Version = ReadString(payload, "version"),
            IsCurrent = ReadBool(payload, "is_current")
        };
    }

    private static string ReadString(IReadOnlyDictionary<string, object?> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || value is null)
        {
            return string.Empty;
        }

        return value switch
        {
            string s => s,
            JsonElement json when json.ValueKind == JsonValueKind.String => json.GetString() ?? string.Empty,
            JsonElement json => json.ToString(),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        return value switch
        {
            bool b => b,
            JsonElement json when json.ValueKind is JsonValueKind.True or JsonValueKind.False => json.GetBoolean(),
            JsonElement json when json.ValueKind == JsonValueKind.String && bool.TryParse(json.GetString(), out var parsed) => parsed,
            _ when bool.TryParse(value.ToString(), out var parsed) => parsed,
            _ => false
        };
    }
}
