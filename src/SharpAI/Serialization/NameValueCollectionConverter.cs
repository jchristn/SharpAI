namespace SharpAI.Serialization
{
    using System;
    using System.Linq;
    using System.Text.Json.Serialization;
    using System.Text.Json;
    using System.Collections.Specialized;

    /// <summary>
    /// NameValueCollection converter.
    /// </summary>
    public class NameValueCollectionConverter : JsonConverter<NameValueCollection>
    {
#pragma warning disable CS8600

        /// <summary>
        /// Read.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <param name="typeToConvert">Type to convert.</param>
        /// <param name="options">JSON serializer options.</param>
        /// <returns>NameValueCollection.</returns>
        public override NameValueCollection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected start of object");
            }

            NameValueCollection collection = new NameValueCollection();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return collection;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Expected property name");
                }

                string propertyName = reader.GetString();

                reader.Read();
                switch (reader.TokenType)
                {
                    case JsonTokenType.Null:
                        collection.Add(propertyName, null);
                        break;
                    case JsonTokenType.String:
                        collection.Add(propertyName, reader.GetString());
                        break;
                    case JsonTokenType.StartArray:
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            if (reader.TokenType == JsonTokenType.String)
                            {
                                collection.Add(propertyName, reader.GetString());
                            }
                            else
                            {
                                throw new JsonException("Expected string value in array");
                            }
                        }
                        break;
                    default:
                        throw new JsonException($"Unexpected token type: {reader.TokenType}");
                }
            }

            throw new JsonException("Expected end of object");
        }

        /// <summary>
        /// Write.
        /// </summary>
        /// <param name="writer">Writer.</param>
        /// <param name="value">Value.</param>
        /// <param name="options">JSON serializer options.</param>
        public override void Write(Utf8JsonWriter writer, NameValueCollection value, JsonSerializerOptions options)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();

            foreach (string key in value.Keys)
            {
                writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(key) ?? key);

                string[] values = value.GetValues(key);
                if (values == null || values.Length == 0)
                {
                    writer.WriteNullValue();
                }
                else if (values.Length == 1)
                {
                    writer.WriteStringValue(values[0]);
                }
                else
                {
                    writer.WriteStartArray();
                    foreach (string v in values)
                    {
                        writer.WriteStringValue(v);
                    }
                    writer.WriteEndArray();
                }
            }

            writer.WriteEndObject();
        }

#pragma warning restore CS8600
    }
}
