using System.Globalization;
using System.Reflection;

namespace DS4AudioUtil.Utils
{
    internal static class ArguementsParser
    {
        internal static bool TryParse<T>(string[] args, T defaultConfig, out T config) where T : struct
        {
            object boxed = defaultConfig;

            var argMap = parseArgsToDictionary(args);
            FieldInfo[] fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields)
            {
                if (argMap.TryGetValue(field.Name, out string? rawValue))
                {
                    if (tryParseValue(rawValue, field.FieldType, out object? parsedValue))
                    {
                        field.SetValue(boxed, parsedValue);
                    }
                    else
                    {
                        Console.WriteLine($"Error: Value '{rawValue}' for arguement '--{field.Name}' has invalid type (expected {field.FieldType.Name})");
                        config = defaultConfig;
                        return false;
                    }
                }
            }

            config = (T)boxed;
            return true;
        }

        private static Dictionary<string, string> parseArgsToDictionary(string[] args)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].TrimStart('-');

                // Поддержка формата --Key=Value
                var parts = arg.Split('=', 2);
                if (parts.Length == 2)
                {
                    dict[parts[0]] = parts[1];
                    continue;
                }

                // Поддержка формата --Key Value
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    dict[arg] = args[i + 1];
                    i++;
                }
            }
            return dict;
        }

        private static bool tryParseValue(string input, Type targetType, out object? result)
        {
            result = null;

            // Разбор чисел (Hex 0x00 или Decimal)
            if (isNumericType(targetType))
            {
                return tryParseNumber(input, targetType, out result);
            }

            if (targetType == typeof(bool))
            {
                if (bool.TryParse(input, out bool boolRes))
                {
                    result = boolRes;
                    return true;
                }
                return false;
            }

            if (targetType == typeof(string))
            {
                result = input;
                return true;
            }

            return false;
        }

        private static bool tryParseNumber(string input, Type targetType, out object? result)
        {
            result = null;
            bool isHex = input.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            string cleanInput = isHex ? input.Substring(2) : input;
            NumberStyles style = isHex ? NumberStyles.HexNumber : NumberStyles.Integer;

            try
            {
                result = targetType switch
                {
                    Type t when t == typeof(short) => short.Parse(cleanInput, style, CultureInfo.InvariantCulture),
                    Type t when t == typeof(ushort) => ushort.Parse(cleanInput, style, CultureInfo.InvariantCulture),
                    Type t when t == typeof(int) => int.Parse(cleanInput, style, CultureInfo.InvariantCulture),
                    Type t when t == typeof(uint) => uint.Parse(cleanInput, style, CultureInfo.InvariantCulture),
                    Type t when t == typeof(long) => long.Parse(cleanInput, style, CultureInfo.InvariantCulture),
                    Type t when t == typeof(ulong) => ulong.Parse(cleanInput, style, CultureInfo.InvariantCulture),
                    Type t when t == typeof(byte) => byte.Parse(cleanInput, style, CultureInfo.InvariantCulture),
                    _ => null
                };
                return result != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool isNumericType(Type type) =>
            type == typeof(short) || type == typeof(ushort) ||
            type == typeof(int) || type == typeof(uint) ||
            type == typeof(long) || type == typeof(ulong) ||
            type == typeof(byte);
    }
}
