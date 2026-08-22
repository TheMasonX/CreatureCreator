using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ProceduralCreature.Serialization
{
    /// <summary>
    /// Minimal recursive-descent JSON parser, dependency-free by design (matches the
    /// rest of the Definition layer avoiding third-party/package dependencies —
    /// implementation guide §1.2). Parses into a plain object tree:
    /// Dictionary&lt;string, object&gt; for objects, List&lt;object&gt; for arrays,
    /// string/double/bool/null for leaves. JsonDnaSerializer walks this tree; this
    /// class knows nothing about CreatureDefinition.
    ///
    /// Not a general-purpose JSON library — it covers exactly the JSON subset
    /// CanonicalJsonWriter produces (no comments, no trailing commas, standard
    /// escapes). Reaching for a full package (Newtonsoft/System.Text.Json) is a
    /// reasonable upgrade once one is already a project dependency for another
    /// reason; nothing here blocks that swap since it's isolated behind
    /// IDnaSerializer.
    /// </summary>
    internal static class MiniJsonReader
    {
        public static object Parse(string json)
        {
            if (json == null)
            {
                throw new DnaDeserializationException("JSON input was null.");
            }

            int index = 0;
            try
            {
                SkipWhitespace(json, ref index);
                object value = ParseValue(json, ref index);
                SkipWhitespace(json, ref index);
                if (index != json.Length)
                {
                    throw new DnaDeserializationException(
                        $"Unexpected trailing content at position {index}.");
                }
                return value;
            }
            catch (DnaDeserializationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DnaDeserializationException(
                    $"Malformed JSON near position {index}: {ex.Message}", ex);
            }
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length)
            {
                throw new DnaDeserializationException("Unexpected end of JSON input.");
            }

            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't':
                    Expect(s, ref i, "true");
                    return true;
                case 'f':
                    Expect(s, ref i, "false");
                    return false;
                case 'n':
                    Expect(s, ref i, "null");
                    return null;
                default:
                    return ParseNumber(s, ref i);
            }
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            i++; // consume '{'
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}')
            {
                i++;
                return result;
            }

            while (true)
            {
                SkipWhitespace(s, ref i);
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                ExpectChar(s, ref i, ':');
                object value = ParseValue(s, ref i);
                result[key] = value;
                SkipWhitespace(s, ref i);

                if (i >= s.Length)
                {
                    throw new DnaDeserializationException("Unterminated JSON object.");
                }

                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; break; }
                throw new DnaDeserializationException($"Expected ',' or '}}' at position {i}.");
            }

            return result;
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var result = new List<object>();
            i++; // consume '['
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']')
            {
                i++;
                return result;
            }

            while (true)
            {
                object value = ParseValue(s, ref i);
                result.Add(value);
                SkipWhitespace(s, ref i);

                if (i >= s.Length)
                {
                    throw new DnaDeserializationException("Unterminated JSON array.");
                }

                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; break; }
                throw new DnaDeserializationException($"Expected ',' or ']' at position {i}.");
            }

            return result;
        }

        private static string ParseString(string s, ref int i)
        {
            ExpectChar(s, ref i, '"');
            var sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length)
                {
                    throw new DnaDeserializationException("Unterminated JSON string.");
                }

                char c = s[i++];
                if (c == '"') break;

                if (c == '\\')
                {
                    if (i >= s.Length)
                    {
                        throw new DnaDeserializationException("Unterminated escape sequence.");
                    }
                    char esc = s[i++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length)
                            {
                                throw new DnaDeserializationException("Truncated unicode escape.");
                            }
                            string hex = s.Substring(i, 4);
                            sb.Append((char)ushort.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default:
                            throw new DnaDeserializationException($"Unknown escape '\\{esc}'.");
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E'
                                     || s[i] == '+' || s[i] == '-')) i++;

            string token = s.Substring(start, i - start);
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                throw new DnaDeserializationException($"Invalid number literal '{token}' at position {start}.");
            }
            return value;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static void ExpectChar(string s, ref int i, char expected)
        {
            if (i >= s.Length || s[i] != expected)
            {
                throw new DnaDeserializationException($"Expected '{expected}' at position {i}.");
            }
            i++;
        }

        private static void Expect(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length || s.Substring(i, literal.Length) != literal)
            {
                throw new DnaDeserializationException($"Expected literal '{literal}' at position {i}.");
            }
            i += literal.Length;
        }
    }
}
