using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace MortarSmapiBridge.Gmcm;

internal static class GmcmWalker
{
    internal static GmcmCaptureFile WalkModConfig(object modConfig, GmcmModIdentity mod, string gmcmVersion, DateTimeOffset capturedAt)
    {
        GmcmCaptureFile file = new()
        {
            Schema = 1,
            Mod = mod,
            GmcmVersion = gmcmVersion,
            CapturedAt = capturedAt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            TitleScreenOnlyDefault = ReadTitleScreenOnlyDefault(modConfig)
        };

        object? pages = GmcmReflection.GetMemberValue(modConfig, "Pages")
            ?? throw new InvalidOperationException("ModConfig.Pages");

        foreach (KeyValuePair<string, object> pageEntry in GmcmReflection.EnumeratePages(pages))
        {
            object page = pageEntry.Value;
            string? title = GmcmReflection.InvokeStringFunc(
                GmcmReflection.GetMemberValue(page, "PageTitle") ?? GmcmReflection.GetMemberValue(page, "Title"));

            GmcmPageCapture capturedPage = new()
            {
                Id = pageEntry.Key,
                Title = title
            };

            object? options = GmcmReflection.GetMemberValue(page, "Options")
                ?? throw new InvalidOperationException("ModConfigPage.Options");

            int index = 0;
            foreach (object option in GmcmReflection.Enumerate(options))
            {
                capturedPage.Options.Add(WalkOption(option, index));
                index++;
            }

            file.Pages.Add(capturedPage);
        }

        return file;
    }

    internal static GmcmOptionCapture WalkOption(object option, int index)
    {
        string? fieldId = GmcmReflection.GetMemberValue(option, "FieldId")?.ToString();
        if (GmcmFieldIds.LooksLikeGuid(fieldId))
            fieldId = null;
        else if (string.IsNullOrEmpty(fieldId))
            fieldId = null;

        bool titleScreenOnly = GmcmReflection.GetMemberValue(option, "IsTitleScreenOnly") is true;
        string name = GmcmReflection.InvokeStringFunc(GmcmReflection.GetMemberValue(option, "Name")) ?? "";
        string? tooltip = GmcmReflection.InvokeStringFunc(GmcmReflection.GetMemberValue(option, "Tooltip"));

        GmcmOptionCapture capture = new()
        {
            Index = index,
            FieldId = fieldId,
            Name = name,
            Tooltip = tooltip,
            TitleScreenOnly = titleScreenOnly,
            Editable = true
        };

        string typeName = GmcmReflection.TypeName(option);
        if (typeName.StartsWith("SimpleModOption", StringComparison.Ordinal))
            FillSimple(option, capture);
        else if (typeName.StartsWith("NumericModOption", StringComparison.Ordinal))
            FillNumeric(option, capture);
        else if (typeName.StartsWith("ChoiceModOption", StringComparison.Ordinal))
            FillChoice(option, capture);
        else if (typeName == "PageLinkModOption")
        {
            capture.Kind = "pageLink";
            capture.Value = GmcmReflection.GetMemberValue(option, "PageId")?.ToString();
            capture.Editable = false;
        }
        else if (typeName == "SectionTitleModOption")
        {
            capture.Kind = "sectionTitle";
            capture.Editable = false;
        }
        else if (typeName == "SectionSubHeaderModOption")
        {
            capture.Kind = "sectionSubHeader";
            capture.Editable = false;
        }
        else if (typeName == "ParagraphModOption")
        {
            capture.Kind = "paragraph";
            capture.Editable = false;
        }
        else if (typeName == "ImageModOption")
        {
            capture.Kind = "image";
            capture.Editable = false;
        }
        else if (typeName == "ComplexModOption")
            FillComplex(option, capture);
        else
        {
            capture.Kind = "complex";
            capture.Editable = false;
        }

        return capture;
    }

    private static bool ReadTitleScreenOnlyDefault(object modConfig)
    {
        object? value = GmcmReflection.GetMemberValue(modConfig, "DefaultTitleScreenOnly")
            ?? GmcmReflection.GetMemberValue(modConfig, "TitleScreenOnly");
        return value is true;
    }

    private static void FillSimple(object option, GmcmOptionCapture capture)
    {
        Type? argument = GmcmReflection.GenericArgument(option);
        capture.Kind = SimpleKind(argument);
        capture.Value = ReadSimpleValue(GmcmReflection.GetMemberValue(option, "Value"), argument);
    }

    private static string SimpleKind(Type? argument)
    {
        if (argument == typeof(bool))
            return "bool";
        if (argument?.Name == "SButton")
            return "sbutton";
        if (argument?.Name == "KeybindList")
            return "keybindList";
        return "string";
    }

    private static object? ReadSimpleValue(object? value, Type? argument)
    {
        if (value == null)
            return null;
        if (argument == typeof(bool) || value is bool || value is string)
            return value;
        return value.ToString();
    }

    private static void FillNumeric(object option, GmcmOptionCapture capture)
    {
        Type? argument = GmcmReflection.GenericArgument(option);
        capture.Kind = argument == typeof(float) ? "float" : "int";
        capture.Value = GmcmReflection.GetMemberValue(option, "Value");
        capture.Min = GmcmReflection.GetMemberValue(option, "Minimum");
        capture.Max = GmcmReflection.GetMemberValue(option, "Maximum");
        capture.Interval = GmcmReflection.GetMemberValue(option, "Interval");
        capture.FormatSamples = SampleFormat(option, capture.Min, capture.Max, capture.Interval, argument);
    }

    private static List<GmcmFormatSample>? SampleFormat(object option, object? min, object? max, object? interval, Type? argument)
    {
        if (min == null || max == null || interval == null)
            return null;

        object? formatter = GmcmReflection.GetMemberValue(option, "FormatValue")
            ?? GmcmReflection.GetMemberValue(option, "formatValue");
        if (formatter is not Delegate format)
            return null;

        List<GmcmFormatSample> samples = new();
        if (argument == typeof(float) || min is float || min is double)
        {
            float start = Convert.ToSingle(min, CultureInfo.InvariantCulture);
            float end = Convert.ToSingle(max, CultureInfo.InvariantCulture);
            float step = Convert.ToSingle(interval, CultureInfo.InvariantCulture);
            if (step <= 0)
                return samples;
            int count = 0;
            for (float value = start; value <= end + (step / 2f) && count < 50; value += step, count++)
            {
                object boxed = value;
                samples.Add(new GmcmFormatSample
                {
                    Value = value,
                    Label = format.DynamicInvoke(boxed)?.ToString() ?? value.ToString(CultureInfo.InvariantCulture)
                });
            }
        }
        else
        {
            int start = Convert.ToInt32(min, CultureInfo.InvariantCulture);
            int end = Convert.ToInt32(max, CultureInfo.InvariantCulture);
            int step = Convert.ToInt32(interval, CultureInfo.InvariantCulture);
            if (step <= 0)
                return samples;
            int count = 0;
            for (int value = start; value <= end && count < 50; value += step, count++)
            {
                samples.Add(new GmcmFormatSample
                {
                    Value = value,
                    Label = format.DynamicInvoke(value)?.ToString() ?? value.ToString(CultureInfo.InvariantCulture)
                });
            }
        }

        return samples;
    }

    private static void FillChoice(object option, GmcmOptionCapture capture)
    {
        capture.Kind = "choice";
        capture.Value = ReadSimpleValue(
            GmcmReflection.GetMemberValue(option, "Value"),
            GmcmReflection.GenericArgument(option));
        object? formatter = GmcmReflection.GetMemberValue(option, "FormatChoice")
            ?? GmcmReflection.GetMemberValue(option, "formatChoice");
        Delegate? format = formatter as Delegate;
        capture.Choices = new List<GmcmChoiceCapture>();
        foreach (object choice in GmcmReflection.Enumerate(GmcmReflection.GetMemberValue(option, "Choices")))
        {
            capture.Choices.Add(new GmcmChoiceCapture
            {
                Value = ReadSimpleValue(choice, choice.GetType()),
                Label = format?.DynamicInvoke(choice)?.ToString() ?? choice.ToString() ?? ""
            });
        }
    }

    private static void FillComplex(object option, GmcmOptionCapture capture)
    {
        object? picker = FindGmcmOptionsTarget(option);
        if (picker == null)
        {
            capture.Kind = "complex";
            capture.Editable = false;
            return;
        }

        string pickerName = picker.GetType().Name;
        if (pickerName == "ColorPickerOption")
        {
            capture.Kind = "color";
            capture.Editable = true;
            object? color = InvokeGetter(picker);
            Dictionary<string, object?> value = color != null
                ? GmcmReflection.ColorToJson(color)
                : new Dictionary<string, object?>();
            value["showAlpha"] = GmcmReflection.GetMemberValue(picker, "showAlpha")
                ?? GmcmReflection.GetMemberValue(picker, "ShowAlpha")
                ?? false;
            value["style"] = GmcmReflection.GetMemberValue(picker, "style")
                ?? GmcmReflection.GetMemberValue(picker, "Style");
            capture.Value = value;
            return;
        }

        if (pickerName == "ImagePickerOption")
        {
            capture.Kind = "image";
            capture.Editable = true;
            capture.Value = InvokeGetter(picker);
            capture.Max = GmcmReflection.GetMemberValue(picker, "max")
                ?? GmcmReflection.GetMemberValue(picker, "Max")
                ?? GmcmReflection.GetMemberValue(picker, "MaxValue");
            object? labels = GmcmReflection.GetMemberValue(picker, "labels")
                ?? GmcmReflection.GetMemberValue(picker, "Labels");
            if (labels != null)
            {
                capture.Choices = new List<GmcmChoiceCapture>();
                int i = 0;
                foreach (object label in GmcmReflection.Enumerate(labels))
                {
                    capture.Choices.Add(new GmcmChoiceCapture { Value = i, Label = label.ToString() ?? "" });
                    i++;
                }
            }

            return;
        }

        capture.Kind = "complex";
        capture.Editable = false;
    }

    internal static object? FindGmcmOptionsTarget(object option)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        for (Type? type = option.GetType(); type != null; type = type.BaseType)
        {
            foreach (FieldInfo field in type.GetFields(flags))
            {
                object? candidate = UnwrapDelegateTarget(field.GetValue(option));
                if (IsGmcmOptionsPicker(candidate))
                    return candidate;
            }

            foreach (PropertyInfo property in type.GetProperties(flags))
            {
                if (property.GetIndexParameters().Length != 0)
                    continue;
                object? candidate = UnwrapDelegateTarget(property.GetValue(option));
                if (IsGmcmOptionsPicker(candidate))
                    return candidate;
            }
        }

        return null;
    }

    private static object? UnwrapDelegateTarget(object? value)
    {
        if (value is Delegate del)
            return del.Target;
        return value;
    }

    private static bool IsGmcmOptionsPicker(object? candidate)
    {
        if (candidate == null)
            return false;
        Type type = candidate.GetType();
        if (type.Namespace != "GMCMOptions.Framework")
            return false;
        return type.Name is "ColorPickerOption" or "ImagePickerOption";
    }

    internal static object? InvokeGetter(object picker)
    {
        object? getter = GmcmReflection.GetMemberValue(picker, "getValue")
            ?? GmcmReflection.GetMemberValue(picker, "GetValue")
            ?? GmcmReflection.GetMemberValue(picker, "Getter");
        if (getter is Delegate del)
            return del.DynamicInvoke();
        if (GmcmReflection.TryInvoke(picker, "getValue", out object? invoked) || GmcmReflection.TryInvoke(picker, "GetValue", out invoked))
            return invoked;
        return GmcmReflection.GetMemberValue(picker, "Value");
    }

    internal static bool TryInvokeSetter(object picker, object? value)
    {
        object? setter = GmcmReflection.GetMemberValue(picker, "setValue")
            ?? GmcmReflection.GetMemberValue(picker, "SetValue")
            ?? GmcmReflection.GetMemberValue(picker, "Setter");
        if (setter is Delegate del)
        {
            Type[]? parameters = del.Method.GetParameters() is { Length: > 0 } args
                ? new[] { args[0].ParameterType }
                : null;
            object? coerced = parameters == null ? value : GmcmReflection.Coerce(value, parameters[0]);
            if (value is Dictionary<string, object?> dict && parameters != null)
                coerced = GmcmReflection.ColorFromComponents(parameters[0], dict);
            del.DynamicInvoke(coerced);
            return true;
        }

        return GmcmReflection.TrySetMemberValue(picker, "Value", value);
    }
}
