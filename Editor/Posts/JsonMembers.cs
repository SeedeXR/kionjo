using System;
using System.Collections.Generic;
using System.Text;

namespace SeedeXR.Kionjo.EditorTools
{
    /// <summary>
    /// Just enough JSON to keep what JsonUtility cannot: the top-level members of an object and the elements of an
    /// array, each as its exact raw text. Used so Kionjo can read entries written by hand or by other tools (media as
    /// plain paths) and write them back without dropping fields it does not know.
    /// </summary>
    static class JsonMembers
    {
        /// <summary>Top-level (key, raw value) pairs of a JSON object, in order; empty when text is not an object.</summary>
        public static List<KeyValuePair<string, string>> Object(string text)
        {
            var members = new List<KeyValuePair<string, string>>();
            int i = Skip(text, 0);
            if (i >= text.Length || text[i] != '{') return members;
            i = Skip(text, i + 1);
            while (i < text.Length && text[i] != '}')
            {
                int keyStart = i;
                i = SkipValue(text, i);
                var key = Unquote(text.Substring(keyStart, i - keyStart));
                i = Skip(text, i);
                if (i >= text.Length || text[i] != ':') break;
                i = Skip(text, i + 1);
                int valueStart = i;
                i = SkipValue(text, i);
                members.Add(new KeyValuePair<string, string>(key, text.Substring(valueStart, i - valueStart).Trim()));
                i = Skip(text, i);
                if (i < text.Length && text[i] == ',') i = Skip(text, i + 1);
            }
            return members;
        }

        /// <summary>Raw elements of a JSON array; empty when raw is not an array.</summary>
        public static List<string> Array(string raw)
        {
            var items = new List<string>();
            int i = Skip(raw, 0);
            if (i >= raw.Length || raw[i] != '[') return items;
            i = Skip(raw, i + 1);
            while (i < raw.Length && raw[i] != ']')
            {
                int start = i;
                i = SkipValue(raw, i);
                items.Add(raw.Substring(start, i - start).Trim());
                i = Skip(raw, i);
                if (i < raw.Length && raw[i] == ',') i = Skip(raw, i + 1);
            }
            return items;
        }

        /// <summary>The string a raw JSON string literal stands for; null when raw is not a string.</summary>
        public static string Unquote(string raw)
        {
            raw = raw?.Trim();
            if (string.IsNullOrEmpty(raw) || raw[0] != '"') return null;
            var s = new StringBuilder();
            for (int i = 1; i < raw.Length - 1; i++)
            {
                char c = raw[i];
                if (c != '\\') { s.Append(c); continue; }
                c = raw[++i];
                switch (c)
                {
                    case 'n': s.Append('\n'); break;
                    case 't': s.Append('\t'); break;
                    case 'r': s.Append('\r'); break;
                    case 'b': s.Append('\b'); break;
                    case 'f': s.Append('\f'); break;
                    case 'u': s.Append((char)Convert.ToInt32(raw.Substring(i + 1, 4), 16)); i += 4; break;
                    default: s.Append(c); break; // \" \\ \/
                }
            }
            return s.ToString();
        }

        static int Skip(string s, int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            return i;
        }

        /// <summary>Index just past the value starting at i (string, object, array or literal).</summary>
        static int SkipValue(string s, int i)
        {
            if (i >= s.Length) return i;
            if (s[i] == '"')
            {
                for (i++; i < s.Length; i++)
                {
                    if (s[i] == '\\') i++;
                    else if (s[i] == '"') return i + 1;
                }
                return i;
            }
            if (s[i] == '{' || s[i] == '[')
            {
                int depth = 0;
                for (; i < s.Length; i++)
                {
                    char c = s[i];
                    if (c == '"') { i = SkipValue(s, i) - 1; continue; }
                    if (c == '{' || c == '[') depth++;
                    else if (c == '}' || c == ']') { if (--depth == 0) return i + 1; }
                }
                return i;
            }
            while (i < s.Length && s[i] != ',' && s[i] != '}' && s[i] != ']') i++;
            return i;
        }
    }
}
