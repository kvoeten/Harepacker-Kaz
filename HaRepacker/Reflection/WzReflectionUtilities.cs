using MapleLib.WzLib.WzProperties;
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MapleLib.WzLib.Serializer
{
    public static class WzReflectionUtils
    {
        /// <summary>
        /// Safely gets a value from a child property, converting types as needed.
        /// </summary>
        public static T GetWzValue<T>(WzImageProperty parent, string childName, T defaultValue = default)
        {
            if (parent is WzSubProperty sub && sub[childName] is WzImageProperty prop)
            {
                object value = prop switch
                {
                    WzStringProperty stringProp => stringProp.Value,
                    WzShortProperty shortProp => shortProp.Value,
                    WzIntProperty intProp => intProp.Value,
                    WzLongProperty longProp => longProp.Value,
                    WzFloatProperty floatProp => floatProp.Value,
                    WzDoubleProperty doubleProp => doubleProp.Value,
                    _ => null
                };

                if (value != null)
                {
                    try
                    {
                        return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
                    }
                    catch (Exception)
                    {
                        return defaultValue;
                    }
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Sanitizes a name to avoid conflicts with language keywords.
        /// </summary>
        public static string SanitizeName(string name)
        {
            return name switch
            {
                "string" => "display_string",
                "default" => "default_value",
                "option" => "option_str",
                "type" => "type_str",
                _ => name.Replace(" ", "_")
            };
        }

        /// <summary>
        /// Converts a PascalCase or camelCase string to snake_case.
        /// </summary>
        public static string ToSnakeCase(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return Regex.Replace(text, @"([a-z0-9])([A-Z])", "$1_$2").ToLower();
        }
    }
}
