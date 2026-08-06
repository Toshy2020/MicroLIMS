using System.Text.Json;
using System.Text.Json.Serialization;

namespace MicroLIMS.API.Json;

// PostgreSQL's timestamptz columns only accept DateTimeKind.Utc - but a
// plain <input type="date"> sends a bare "2026-07-31" with no timezone
// marker, which System.Text.Json parses as Kind=Unspecified, and Npgsql
// then rejects at save time. Every DateTime accepted anywhere in this
// API is treated as UTC (there is no other timezone concept in this
// app), so re-stamping the Kind here - once, at the JSON boundary -
// fixes this for every endpoint instead of chasing it field by field.
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();
        return value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}

public class UtcNullableDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var value = reader.GetDateTime();
        return value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value.HasValue) writer.WriteStringValue(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
        else writer.WriteNullValue();
    }
}
