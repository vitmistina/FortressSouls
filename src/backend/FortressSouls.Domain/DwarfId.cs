namespace FortressSouls.Domain;

using System.Text.Json;
using System.Text.Json.Serialization;

[JsonConverter(typeof(DwarfIdJsonConverter))]
public readonly record struct DwarfId
{
    public const int MaxLength = 20;

    public DwarfId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Dwarf ID is required.", nameof(value));
        }

        if (value.Length > MaxLength)
        {
            throw new ArgumentException($"Dwarf ID cannot exceed {MaxLength} characters.", nameof(value));
        }

        if (!value.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Dwarf ID must contain ASCII digits only.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public static DwarfId Parse(string value) => new(value);

    public override string ToString() => Value;

    private sealed class DwarfIdJsonConverter : JsonConverter<DwarfId>
    {
        public override DwarfId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (value is null)
            {
                throw new JsonException("Dwarf ID JSON value cannot be null.");
            }

            try
            {
                return Parse(value);
            }
            catch (ArgumentException exception)
            {
                throw new JsonException(exception.Message, exception);
            }
        }

        public override void Write(Utf8JsonWriter writer, DwarfId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }
}
