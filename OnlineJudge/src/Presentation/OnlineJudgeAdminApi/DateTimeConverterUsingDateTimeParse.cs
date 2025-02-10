using System.Text.Json;
using System.Text.Json.Serialization;

public class DateTimeConverterUsingDateTimeParse : JsonConverter<DateTime>
{
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string dateString = reader.GetString();
        if (DateTime.TryParseExact(dateString, DateTimeFormat, null, System.Globalization.DateTimeStyles.None, out DateTime date))
        {
            return date;
        }

        throw new JsonException($"Unable to convert \"{dateString}\" to DateTime using format \"{DateTimeFormat}\".");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(DateTimeFormat));
    }
}
