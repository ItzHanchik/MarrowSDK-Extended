using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace AuroraRP
{
    /// <summary>
    /// Мини-сериализатор JSON для данных мода (конфиг, профиль, снимок состояния, кэш банка).
    ///
    /// Почему не UnityEngine.JsonUtility: под IL2CPP он принимает только Il2Cpp-объекты
    /// (Il2CppSystem.Object) и не умеет работать с обычными C#-классами мода — компилятор
    /// ругается «cannot convert from 'AuroraRP.AuroraConfig' to 'Il2CppSystem.Object'».
    /// Свой сериализатор снимает эту зависимость целиком.
    ///
    /// Понимает: строки, числа, bool, enum, списки/массивы и классы с публичными полями.
    /// Формат тот же, что писал JsonUtility (те же имена полей), поэтому старые файлы читаются.
    /// </summary>
    public static class AuroraJson
    {
        // ------------------------------------------------------------------ запись

        /// <summary>Сериализует объект в красивый (с отступами) JSON.</summary>
        public static string Write(object value)
        {
            try
            {
                var sb = new StringBuilder(8192);
                WriteValue(sb, value, 0);
                return sb.ToString();
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "json write");
                return "{}";
            }
        }

        private static void WriteValue(StringBuilder sb, object value, int depth)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            Type type = value.GetType();

            if (value is string s)
            {
                WriteString(sb, s);
                return;
            }

            if (value is char ch)
            {
                WriteString(sb, ch.ToString());
                return;
            }

            if (value is bool flag)
            {
                sb.Append(flag ? "true" : "false");
                return;
            }

            if (type.IsEnum)
            {
                sb.Append(Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is float f)
            {
                WriteNumber(sb, f.ToString("R", CultureInfo.InvariantCulture), float.IsNaN(f) || float.IsInfinity(f));
                return;
            }

            if (value is double d)
            {
                WriteNumber(sb, d.ToString("R", CultureInfo.InvariantCulture), double.IsNaN(d) || double.IsInfinity(d));
                return;
            }

            if (value is decimal dec)
            {
                sb.Append(dec.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is byte || value is sbyte || value is short || value is ushort ||
                value is int || value is uint || value is long || value is ulong)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            if (value is IDictionary dictionary)
            {
                WriteDictionary(sb, dictionary, depth);
                return;
            }

            if (value is IEnumerable sequence)
            {
                WriteSequence(sb, sequence, depth);
                return;
            }

            WriteObject(sb, value, type, depth);
        }

        private static void WriteNumber(StringBuilder sb, string text, bool broken)
        {
            sb.Append(broken ? "0" : text);
        }

        private static void WriteSequence(StringBuilder sb, IEnumerable sequence, int depth)
        {
            sb.Append('[');
            bool first = true;

            foreach (object item in sequence)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                NewLine(sb, depth + 1);
                WriteValue(sb, item, depth + 1);
            }

            if (!first)
            {
                NewLine(sb, depth);
            }

            sb.Append(']');
        }

        private static void WriteDictionary(StringBuilder sb, IDictionary dictionary, int depth)
        {
            sb.Append('{');
            bool first = true;

            foreach (DictionaryEntry pair in dictionary)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                NewLine(sb, depth + 1);
                WriteString(sb, pair.Key == null ? "" : pair.Key.ToString());
                sb.Append(": ");
                WriteValue(sb, pair.Value, depth + 1);
            }

            if (!first)
            {
                NewLine(sb, depth);
            }

            sb.Append('}');
        }

        private static void WriteObject(StringBuilder sb, object value, Type type, int depth)
        {
            sb.Append('{');
            bool first = true;

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];

                if (field.IsStatic || field.IsNotSerialized)
                {
                    continue;
                }

                object fieldValue;

                try
                {
                    fieldValue = field.GetValue(value);
                }
                catch
                {
                    continue;
                }

                if (fieldValue == null)
                {
                    // null-поля не пишем: при чтении останутся значения по умолчанию.
                    continue;
                }

                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                NewLine(sb, depth + 1);
                WriteString(sb, field.Name);
                sb.Append(": ");
                WriteValue(sb, fieldValue, depth + 1);
            }

            if (!first)
            {
                NewLine(sb, depth);
            }

            sb.Append('}');
        }

        private static void NewLine(StringBuilder sb, int depth)
        {
            sb.Append('\n');
            sb.Append(' ', Math.Max(0, depth) * 2);
        }

        private static void WriteString(StringBuilder sb, string text)
        {
            sb.Append('"');

            if (text != null)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];

                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        default:
                            if (c < ' ')
                            {
                                sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                sb.Append(c);
                            }

                            break;
                    }
                }
            }

            sb.Append('"');
        }

        // ------------------------------------------------------------------ чтение

        /// <summary>Читает JSON в объект указанного типа (неизвестные поля игнорируются).</summary>
        public static T Read<T>(string json) where T : class
        {
            try
            {
                object parsed = new Parser(json).Parse();
                return ToType(parsed, typeof(T)) as T;
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "json read");
                return null;
            }
        }

        /// <summary>Читает JSON в объект указанного типа.</summary>
        public static object Read(string json, Type type)
        {
            try
            {
                object parsed = new Parser(json).Parse();
                return ToType(parsed, type);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "json read");
                return null;
            }
        }

        private static object ToType(object value, Type type)
        {
            if (type == null || value == null)
            {
                return null;
            }

            if (type == typeof(object))
            {
                return value;
            }

            if (type == typeof(string))
            {
                return value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            if (type == typeof(bool))
            {
                if (value is bool flag)
                {
                    return flag;
                }

                try
                {
                    return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                    return null;
                }
            }

            if (type.IsEnum)
            {
                try
                {
                    return Enum.ToObject(type, Convert.ToInt64(value, CultureInfo.InvariantCulture));
                }
                catch
                {
                    return null;
                }
            }

            if (type.IsArray || (value is List<object> && type.IsGenericType))
            {
                return ToSequence(value, type);
            }

            if (value is Dictionary<string, object> map)
            {
                return ToObject(map, type);
            }

            try
            {
                return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        private static object ToSequence(object value, Type type)
        {
            var items = value as List<object>;

            if (items == null)
            {
                return null;
            }

            Type elementType;

            if (type.IsArray)
            {
                elementType = type.GetElementType();
            }
            else
            {
                Type[] arguments = type.GetGenericArguments();

                if (arguments.Length != 1)
                {
                    return null;
                }

                elementType = arguments[0];
            }

            if (elementType == null)
            {
                return null;
            }

            if (type.IsArray)
            {
                Array array = Array.CreateInstance(elementType, items.Count);

                for (int i = 0; i < items.Count; i++)
                {
                    object item = ToType(items[i], elementType);

                    if (item != null)
                    {
                        array.SetValue(item, i);
                    }
                }

                return array;
            }

            object list;

            try
            {
                list = Activator.CreateInstance(type, true);
            }
            catch
            {
                return null;
            }

            var target = list as IList;

            if (target == null)
            {
                return null;
            }

            for (int i = 0; i < items.Count; i++)
            {
                object item = ToType(items[i], elementType);

                if (item != null)
                {
                    target.Add(item);
                }
            }

            return target;
        }

        private static object ToObject(Dictionary<string, object> map, Type type)
        {
            object instance;

            try
            {
                instance = Activator.CreateInstance(type, true);
            }
            catch
            {
                return null;
            }

            if (instance == null)
            {
                return null;
            }

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];

                if (field.IsStatic || field.IsNotSerialized)
                {
                    continue;
                }

                object raw;

                if (!map.TryGetValue(field.Name, out raw) || raw == null)
                {
                    continue;
                }

                object converted = ToType(raw, field.FieldType);

                if (converted == null)
                {
                    continue;
                }

                try
                {
                    field.SetValue(instance, converted);
                }
                catch
                {
                    // Поле не подошло — оставляем значение по умолчанию.
                }
            }

            return instance;
        }

        // ------------------------------------------------------------------ парсер

        private sealed class Parser
        {
            private readonly string _text;
            private int _pos;

            public Parser(string text)
            {
                _text = text ?? "";
                _pos = 0;

                if (_text.Length > 0 && _text[0] == '\uFEFF')
                {
                    _pos = 1;
                }
            }

            public object Parse()
            {
                SkipWhitespace();

                if (_pos >= _text.Length)
                {
                    return null;
                }

                return ParseValue();
            }

            private object ParseValue()
            {
                SkipWhitespace();

                if (_pos >= _text.Length)
                {
                    return null;
                }

                char c = _text[_pos];

                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default: return ParseNumber();
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                _pos++; // '{'

                while (true)
                {
                    SkipWhitespace();

                    if (_pos >= _text.Length)
                    {
                        break;
                    }

                    if (_text[_pos] == '}')
                    {
                        _pos++;
                        break;
                    }

                    if (_text[_pos] == ',')
                    {
                        _pos++;
                        continue;
                    }

                    if (_text[_pos] != '"')
                    {
                        _pos++; // мусор — пропускаем
                        continue;
                    }

                    string key = ParseString();
                    SkipWhitespace();

                    if (_pos < _text.Length && _text[_pos] == ':')
                    {
                        _pos++;
                    }

                    object value = ParseValue();

                    if (key != null)
                    {
                        result[key] = value;
                    }
                }

                return result;
            }

            private List<object> ParseArray()
            {
                var result = new List<object>();
                _pos++; // '['

                while (true)
                {
                    SkipWhitespace();

                    if (_pos >= _text.Length)
                    {
                        break;
                    }

                    if (_text[_pos] == ']')
                    {
                        _pos++;
                        break;
                    }

                    if (_text[_pos] == ',')
                    {
                        _pos++;
                        continue;
                    }

                    result.Add(ParseValue());
                }

                return result;
            }

            private string ParseString()
            {
                _pos++; // открывающая кавычка
                var sb = new StringBuilder();

                while (_pos < _text.Length)
                {
                    char c = _text[_pos++];

                    if (c == '"')
                    {
                        break;
                    }

                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }

                    if (_pos >= _text.Length)
                    {
                        break;
                    }

                    char escape = _text[_pos++];

                    switch (escape)
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
                            if (_pos + 4 <= _text.Length)
                            {
                                int code;

                                if (int.TryParse(_text.Substring(_pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                                {
                                    sb.Append((char)code);
                                }

                                _pos += 4;
                            }

                            break;
                        default:
                            sb.Append(escape);
                            break;
                    }
                }

                return sb.ToString();
            }

            private object ParseNumber()
            {
                int start = _pos;

                while (_pos < _text.Length)
                {
                    char c = _text[_pos];

                    if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E')
                    {
                        _pos++;
                        continue;
                    }

                    break;
                }

                if (_pos == start)
                {
                    _pos++;
                    return null;
                }

                string text = _text.Substring(start, _pos - start);
                long integer;

                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer))
                {
                    return integer;
                }

                double number;

                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                {
                    return number;
                }

                return null;
            }

            private void Expect(string literal)
            {
                if (_pos + literal.Length <= _text.Length &&
                    string.Compare(_text, _pos, literal, 0, literal.Length, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    _pos += literal.Length;
                    return;
                }

                _pos++;
            }

            private void SkipWhitespace()
            {
                while (_pos < _text.Length)
                {
                    char c = _text[_pos];

                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                    {
                        _pos++;
                        continue;
                    }

                    break;
                }
            }
        }
    }
}
