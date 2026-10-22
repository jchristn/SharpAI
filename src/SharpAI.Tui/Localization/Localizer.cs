namespace SharpAI.Tui.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Text.Json.Nodes;
    using SharpAI.Tui.Services;

    internal sealed class Localizer
    {
        internal static readonly string[] Languages = { "en", "es", "pt", "fr", "it", "de", "zh", "ru", "ms", "hi", "ja", "qps-ploc" };
        private readonly JsonNode _English;
        private readonly JsonNode _Catalog;
        private readonly JsonNode _Operator;
        private readonly Dictionary<string, string> _PhraseKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        internal string Locale { get; }
        internal CultureInfo Culture { get; }
        internal Localizer(string locale)
        {
            if (Array.IndexOf(Languages, locale) < 0) throw new ArgumentException("Unsupported terminal locale.", nameof(locale));
            Locale = locale;
            Culture = CultureInfo.GetCultureInfo(locale == "qps-ploc" ? "en" : locale);
            _English = Read("en"); Index(_English, string.Empty);
            _Operator = Read("operator");
            foreach (KeyValuePair<string, JsonNode?> entry in (JsonObject)_Operator) _PhraseKeys[entry.Value!["en"]!.ToString()] = "operator." + entry.Key;
            _Catalog = locale == "qps-ploc" ? _English : Read(locale);
        }
        internal string Text(string key, IDictionary<string, object?>? values = null, int? count = null)
        {
            string suffix = count.HasValue ? "_" + PluralCategory(Locale, count.Value) : string.Empty;
            string? operatorText = key.StartsWith("operator.", StringComparison.Ordinal) ? _Operator[key.Substring(9)]?[Locale == "qps-ploc" ? "en" : Locale]?.ToString() : null;
            string result = operatorText ?? Lookup(_Catalog, key + suffix) ?? Lookup(_Catalog, key) ?? Lookup(_English, key + suffix) ?? Lookup(_English, key) ?? key;
            if (values != null) foreach (KeyValuePair<string, object?> value in values) result = result.Replace("{{" + value.Key + "}}", Convert.ToString(value.Value, Culture));
            if (count.HasValue) result = result.Replace("{{count}}", count.Value.ToString(Culture));
            return SafeText.Clean(Locale == "qps-ploc" ? "[" + result + " ~~~~~]" : result);
        }
        internal string Phrase(string phrase)
        {
            if (_PhraseKeys.TryGetValue(phrase, out string? operatorKey) && operatorKey.StartsWith("operator.", StringComparison.Ordinal)) return Text(operatorKey);
            if (OperatorKeys.Aliases.TryGetValue(phrase, out string? alias))
            {
                string translated = Text(alias);
                if (translated != alias) return translated + (phrase.EndsWith(' ') ? " " : string.Empty);
            }
            return _PhraseKeys.TryGetValue(phrase, out string? key) ? Text(key) : SafeText.Clean(Locale == "qps-ploc" ? "[" + phrase + " ~~~~~]" : phrase);
        }
        private void Index(JsonNode node, string prefix)
        {
            if (node is not JsonObject obj) return;
            foreach (KeyValuePair<string, JsonNode?> item in obj)
            {
                string key = prefix + item.Key;
                if (item.Value is JsonObject) Index(item.Value, key + ".");
                else if (item.Value is JsonValue value && value.TryGetValue<string>(out string? text)) _PhraseKeys.TryAdd(text, key);
            }
        }
        private static string PluralCategory(string locale, int count)
        {
            long value = Math.Abs((long)count);
            if (locale is "zh" or "ja" or "ms") return "other";
            if (locale == "ru")
            {
                long digit = value % 10; long pair = value % 100;
                if (digit == 1 && pair != 11) return "one";
                if (digit >= 2 && digit <= 4 && (pair < 12 || pair > 14)) return "few";
                return "many";
            }
            if (locale is "fr" or "pt" or "hi") return value <= 1 ? "one" : "other";
            return value == 1 ? "one" : "other";
        }
        private static string? Lookup(JsonNode catalog, string key)
        {
            JsonNode? node = catalog;
            foreach (string part in key.Split('.')) node = node is JsonObject obj ? obj[part] : null;
            return node is JsonValue value ? value.ToString() : null;
        }
        private static JsonNode Read(string locale)
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SharpAI.Tui.Localization." + locale + ".json") ?? throw new InvalidDataException("Missing locale " + locale);
            return JsonNode.Parse(stream) ?? throw new InvalidDataException("Empty locale " + locale);
        }
    }
}
