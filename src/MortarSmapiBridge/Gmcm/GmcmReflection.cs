using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace MortarSmapiBridge.Gmcm;

internal static class GmcmReflection
{
    internal const string GmcmModId = "spacechase0.GenericModConfigMenu";

    internal static object? GetMemberValue(object target, string name)
    {
        for (Type? type = target.GetType(); type != null; type = type.BaseType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            PropertyInfo? property = type.GetProperty(name, flags);
            if (property != null)
                return property.GetValue(target);

            FieldInfo? field = type.GetField(name, flags);
            if (field != null)
                return field.GetValue(target);
        }

        return null;
    }

    internal static bool TrySetMemberValue(object target, string name, object? value)
    {
        for (Type? type = target.GetType(); type != null; type = type.BaseType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            PropertyInfo? property = type.GetProperty(name, flags);
            if (property != null && property.SetMethod != null)
            {
                property.SetValue(target, Coerce(value, property.PropertyType));
                return true;
            }

            FieldInfo? field = type.GetField(name, flags);
            if (field != null && !field.IsInitOnly)
            {
                field.SetValue(target, Coerce(value, field.FieldType));
                return true;
            }
        }

        return false;
    }

    internal static object? InvokeMember(object target, string name, params object?[] args)
    {
        for (Type? type = target.GetType(); type != null; type = type.BaseType)
        {
            MethodInfo? method = type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (method != null)
                return method.Invoke(target, args);
        }

        return null;
    }

    internal static bool TryInvoke(object target, string name, out object? result, params object?[] args)
    {
        result = null;
        for (Type? type = target.GetType(); type != null; type = type.BaseType)
        {
            MethodInfo? method = type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (method == null)
                continue;
            result = method.Invoke(target, args);
            return true;
        }

        return false;
    }

    internal static string? InvokeStringFunc(object? func)
    {
        if (func == null)
            return null;
        if (func is Func<string> typed)
            return typed();
        if (func is Delegate del)
        {
            object? value = del.DynamicInvoke();
            return value?.ToString();
        }

        return func.ToString();
    }

    internal static object? Coerce(object? value, Type targetType)
    {
        if (value == null)
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

        Type nonNull = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (nonNull.IsInstanceOfType(value))
            return value;

        if (value is System.Text.Json.JsonElement element)
            return CoerceJson(element, nonNull);

        if (nonNull.IsEnum)
            return Enum.Parse(nonNull, value.ToString() ?? "", ignoreCase: true);

        if (value is IDictionary<string, object?> dict && LooksLikeColor(nonNull))
            return ColorFromComponents(nonNull, dict);

        try
        {
            return Convert.ChangeType(value, nonNull, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return value;
        }
    }

    internal static object CoerceJson(System.Text.Json.JsonElement element, Type targetType)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.True:
            case System.Text.Json.JsonValueKind.False:
                return element.GetBoolean();
            case System.Text.Json.JsonValueKind.Number:
                if (targetType == typeof(int) || targetType == typeof(int?))
                    return element.GetInt32();
                if (targetType == typeof(float) || targetType == typeof(float?))
                    return element.GetSingle();
                if (targetType == typeof(double) || targetType == typeof(double?))
                    return element.GetDouble();
                if (element.TryGetInt32(out int asInt))
                    return asInt;
                return element.GetDouble();
            case System.Text.Json.JsonValueKind.String:
                string text = element.GetString() ?? "";
                if (targetType.IsEnum)
                    return Enum.Parse(targetType, text, ignoreCase: true);
                return text;
            case System.Text.Json.JsonValueKind.Object:
                if (LooksLikeColor(targetType))
                {
                    Dictionary<string, object?> parts = [];
                    foreach (System.Text.Json.JsonProperty property in element.EnumerateObject())
                        parts[property.Name] = CoerceJson(property.Value, typeof(object));
                    return ColorFromComponents(targetType, parts);
                }

                return element;
            default:
                return element;
        }
    }

    internal static bool LooksLikeColor(Type type)
    {
        return type.Name == "Color" || GetInstanceMember(type, "R") != null && GetInstanceMember(type, "G") != null && GetInstanceMember(type, "B") != null;
    }

    private static MemberInfo? GetInstanceMember(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        return (MemberInfo?)type.GetProperty(name, flags) ?? type.GetField(name, flags);
    }

    internal static object ColorFromComponents(Type colorType, IDictionary<string, object?> parts)
    {
        object color = Activator.CreateInstance(colorType)!;
        SetByte(color, "R", parts, "r", "R");
        SetByte(color, "G", parts, "g", "G");
        SetByte(color, "B", parts, "b", "B");
        SetByte(color, "A", parts, "a", "A");
        return color;
    }

    private static void SetByte(object color, string member, IDictionary<string, object?> parts, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (!parts.TryGetValue(key, out object? raw) || raw == null)
                continue;
            byte parsed = Convert.ToByte(raw, CultureInfo.InvariantCulture);
            TrySetMemberValue(color, member, parsed);
            return;
        }
    }

    internal static Dictionary<string, object?> ColorToJson(object color)
    {
        return new Dictionary<string, object?>
        {
            ["r"] = Convert.ToInt32(GetMemberValue(color, "R") ?? 0, CultureInfo.InvariantCulture),
            ["g"] = Convert.ToInt32(GetMemberValue(color, "G") ?? 0, CultureInfo.InvariantCulture),
            ["b"] = Convert.ToInt32(GetMemberValue(color, "B") ?? 0, CultureInfo.InvariantCulture),
            ["a"] = Convert.ToInt32(GetMemberValue(color, "A") ?? 255, CultureInfo.InvariantCulture)
        };
    }

    internal static IEnumerable<object> Enumerate(object? collection)
    {
        if (collection == null)
            yield break;

        object? values = GetMemberValue(collection, "Values") ?? GetMemberValue(collection, "values");
        if (values is IEnumerable dictValues && values is not string)
        {
            foreach (object? item in dictValues)
            {
                if (item != null)
                    yield return item;
            }

            yield break;
        }

        if (collection is IEnumerable enumerable and not string)
        {
            foreach (object? item in enumerable)
            {
                if (item == null)
                    continue;
                PropertyInfo? valueProperty = item.GetType().GetProperty("Value");
                if (valueProperty != null && item.GetType().IsValueType && item.GetType().Name.StartsWith("KeyValuePair", StringComparison.Ordinal))
                {
                    object? unpacked = valueProperty.GetValue(item);
                    if (unpacked != null)
                        yield return unpacked;
                    continue;
                }

                yield return item;
            }
        }
    }

    internal static IEnumerable<KeyValuePair<string, object>> EnumeratePages(object pages)
    {
        if (pages is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Value == null)
                    continue;
                yield return new KeyValuePair<string, object>(entry.Key?.ToString() ?? "", entry.Value);
            }

            yield break;
        }

        foreach (object item in Enumerate(pages))
        {
            object? key = GetMemberValue(item, "Key") ?? GetMemberValue(item, "Id");
            yield return new KeyValuePair<string, object>(key?.ToString() ?? "", item);
        }
    }

    internal static string TypeName(object instance)
    {
        return instance.GetType().Name;
    }

    internal static Type? GenericArgument(object instance)
    {
        Type type = instance.GetType();
        return type.IsGenericType ? type.GetGenericArguments()[0] : null;
    }
}
