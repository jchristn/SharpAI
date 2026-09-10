namespace SharpAI.Grammars
{
    using System;

    using SharpAI.Models.OpenAI;

    /// <summary>
    /// Provides GBNF grammars for JSON-mode / structured-output decoding, and maps the OpenAI
    /// <c>response_format</c> and Ollama <c>format</c> request fields onto a grammar. When a grammar is
    /// applied during sampling the model can only emit tokens permitted by the grammar, so the output is
    /// guaranteed to be syntactically valid JSON.
    /// <para>
    /// Schema-constrained decoding (OpenAI <c>json_schema</c>, Ollama schema objects) currently falls back to
    /// the general JSON grammar — the output is guaranteed valid JSON but is not yet constrained to the exact
    /// schema. Full JSON-Schema-to-GBNF translation is a future enhancement.
    /// </para>
    /// </summary>
    public static class JsonGrammar
    {
        #region Public-Members

        /// <summary>
        /// GBNF grammar (root rule <c>root</c>) that accepts any valid JSON value. Mirrors the canonical
        /// llama.cpp <c>json.gbnf</c>.
        /// </summary>
        public static readonly string GenericJson =
            "root   ::= object\n" +
            "value  ::= object | array | string | number | (\"true\" | \"false\" | \"null\") ws\n" +
            "object ::=\n" +
            "  \"{\" ws (\n" +
            "            string \":\" ws value\n" +
            "    (\",\" ws string \":\" ws value)*\n" +
            "  )? \"}\" ws\n" +
            "array  ::=\n" +
            "  \"[\" ws (\n" +
            "            value\n" +
            "    (\",\" ws value)*\n" +
            "  )? \"]\" ws\n" +
            "string ::=\n" +
            "  \"\\\"\" (\n" +
            "    [^\"\\\\\\x7F\\x00-\\x1F] |\n" +
            "    \"\\\\\" ([\"\\\\bfnrt/] | \"u\" [0-9a-fA-F] [0-9a-fA-F] [0-9a-fA-F] [0-9a-fA-F])\n" +
            "  )* \"\\\"\" ws\n" +
            "number ::= (\"-\"? ([0-9] | [1-9] [0-9]{0,15})) (\".\" [0-9]+)? ([eE] [-+]? [0-9] [1-9]{0,15})? ws\n" +
            "ws ::= | \" \" | \"\\n\" [ \\t]{0,20}\n";

        /// <summary>
        /// Root rule name for the grammars returned by this type.
        /// </summary>
        public static readonly string RootRule = "root";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Resolve a GBNF grammar for an OpenAI <c>response_format</c>. Returns the general JSON grammar for
        /// <c>json_object</c> and <c>json_schema</c>, or null when the caller did not request a JSON format
        /// (<c>text</c> or absent).
        /// </summary>
        /// <param name="responseFormat">The OpenAI response_format, or null.</param>
        /// <returns>A GBNF grammar string, or null when no JSON constraint applies.</returns>
        public static string ForOpenAIResponseFormat(OpenAIResponseFormat responseFormat)
        {
            if (responseFormat == null || String.IsNullOrEmpty(responseFormat.Type)) return null;

            if (String.Equals(responseFormat.Type, "json_object", StringComparison.OrdinalIgnoreCase)
                || String.Equals(responseFormat.Type, "json_schema", StringComparison.OrdinalIgnoreCase))
            {
                return GenericJson;
            }

            return null;
        }

        /// <summary>
        /// Resolve a GBNF grammar for an Ollama <c>format</c> value. Returns the general JSON grammar for the
        /// literal <c>"json"</c> or any non-empty schema value; returns null when the format is absent.
        /// </summary>
        /// <param name="format">The Ollama format value, or null.</param>
        /// <returns>A GBNF grammar string, or null when no JSON constraint applies.</returns>
        public static string ForOllamaFormat(string format)
        {
            if (String.IsNullOrWhiteSpace(format)) return null;
            return GenericJson;
        }

        #endregion
    }
}
