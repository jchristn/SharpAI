namespace SharpAI.Serialization
{
    using System;
    using System.Text.Json.Serialization;
    using System.Text.Json;

    /// <summary>
    /// Strict enum converter factory.
    /// </summary>
    public class StrictEnumConverterFactory : JsonConverterFactory
    {
#pragma warning disable CS8603 // Possible null reference return.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.

        /// <summary>
        /// Can convert.
        /// </summary>
        /// <param name="typeToConvert">Type to convert.</param>
        /// <returns>True if convertible.</returns>
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsEnum;
        }

        /// <summary>
        /// Create converter.
        /// </summary>
        /// <param name="typeToConvert">Type to convert.</param>
        /// <param name="options">JSON serializer options.</param>
        /// <returns>JsonConverter.</returns>
        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            Type converterType = typeof(StrictEnumConverter<>).MakeGenericType(typeToConvert);
            return (JsonConverter)Activator.CreateInstance(converterType);
        }

#pragma warning restore CS8603 // Possible null reference return.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
    }
}
