// ============================================================================
// ProceduralManifestJsonSetup.cs
// ============================================================================
// PURPOSE:
//   Parses the small nested JSON format used by environment manifests without an
//   additional assembly reference. Unity JsonUtility cannot read arrays of arrays,
//   so the editor boundary needs a strict, bounded reader before DTO conversion.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Procedural.
// KEY RESPONSIBILITIES:
//   - Read JSON values with bounded input and nesting and reject duplicate keys.
//   - Preserve numeric token types so integer cell coordinates remain strict.
// DEPENDENCIES:
//   - System only; no packages, engine state or filesystem access.
// USAGE NOTES:
//   Maximum input is 16M UTF-16 code units; depth 64. No comments or trailing commas.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Worsen.Editor.Procedural
{
    public static class ProceduralManifestJsonSetup
    {
        public sealed class Value
        {
            public string Text;
            public bool IsString, IsInteger;
            public List<Value> Items;
            public Dictionary<string, Value> Members;
            public Value this[string key] => Members != null && Members.TryGetValue(key, out var result) ? result : null;
            public Value this[int index] => Items[index];
        }
        public static Value Parse(string text)
        {
            if (text == null || text.Length > 16 * 1024 * 1024) throw new ArgumentException("Manifest input exceeds bounds.");
            int position = 0;
            var result = Read(0); Space();
            if (position != text.Length) Fail();
            return result;
            void Fail() => throw new ArgumentException("Invalid JSON at offset " + position);
            void Space() { while (position < text.Length && (text[position] == ' ' || text[position] == '\r' || text[position] == '\n' || text[position] == '\t')) position++; }
            bool Eat(char c) { Space(); if (position >= text.Length || text[position] != c) return false; position++; return true; }
            string String()
            {
                if (!Eat('"')) Fail(); var value = new StringBuilder();
                while (position < text.Length)
                {
                    char c = text[position++]; if (c == '"') return value.ToString();
                    if (c < 32) Fail();
                    if (c != '\\') { value.Append(c); continue; }
                    if (position >= text.Length) Fail(); c = text[position++];
                    switch (c)
                    {
                        case '"': case '\\': case '/': value.Append(c); break;
                        case 'b': value.Append('\b'); break; case 'f': value.Append('\f'); break;
                        case 'n': value.Append('\n'); break; case 'r': value.Append('\r'); break; case 't': value.Append('\t'); break;
                        case 'u':
                            if (position + 4 > text.Length) Fail();
                            if (!ushort.TryParse(text.Substring(position, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort code)) Fail();
                            value.Append((char)code); position += 4; break;
                        default: Fail(); break;
                    }
                }
                Fail(); return null;
            }
            Value Read(int depth)
            {
                Space(); if (depth > 64 || position >= text.Length) Fail();
                if (text[position] == '"') return new Value { IsString = true, Text = String() };
                if (Eat('{'))
                {
                    var members = new Dictionary<string, Value>(StringComparer.Ordinal);
                    if (Eat('}')) return new Value { Members = members };
                    do { string key = String(); if (!Eat(':') || members.ContainsKey(key)) Fail(); members.Add(key, Read(depth + 1)); } while (Eat(','));
                    if (!Eat('}')) Fail(); return new Value { Members = members };
                }
                if (Eat('['))
                {
                    var items = new List<Value>(); if (Eat(']')) return new Value { Items = items };
                    do { items.Add(Read(depth + 1)); } while (Eat(','));
                    if (!Eat(']')) Fail(); return new Value { Items = items };
                }
                foreach (string literal in new[] { "true", "false", "null" })
                    if (position + literal.Length <= text.Length && text.Substring(position, literal.Length) == literal)
                    { position += literal.Length; return new Value { Text = literal }; }
                int start = position;
                if (text[position] == '-') position++;
                if (position >= text.Length || text[position] < '0' || text[position] > '9') Fail();
                if (text[position] == '0') position++;
                else while (position < text.Length && text[position] >= '0' && text[position] <= '9') position++;
                bool integer = true;
                if (position < text.Length && text[position] == '.')
                {
                    integer = false; position++; int digits = position;
                    while (position < text.Length && text[position] >= '0' && text[position] <= '9') position++;
                    if (digits == position) Fail();
                }
                if (position < text.Length && (text[position] == 'e' || text[position] == 'E'))
                {
                    integer = false; position++; if (position < text.Length && (text[position] == '+' || text[position] == '-')) position++;
                    int digits = position; while (position < text.Length && text[position] >= '0' && text[position] <= '9') position++;
                    if (digits == position) Fail();
                }
                return new Value { Text = text.Substring(start, position - start), IsInteger = integer };
            }
        }
    }
}
