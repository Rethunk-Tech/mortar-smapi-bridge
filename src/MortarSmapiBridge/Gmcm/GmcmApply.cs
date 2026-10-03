using System;
using System.Collections.Generic;
using System.Text.Json;

namespace MortarSmapiBridge.Gmcm;

internal static class GmcmApply
{
    internal static GmcmPendingResultFile ApplyEdits(object modConfig, IReadOnlyList<GmcmPendingEdit> edits)
    {
        GmcmPendingResultFile result = new();
        Dictionary<(string Page, int Index), object> byIndex = new();
        Dictionary<string, object> byFieldId = new();
        IndexOptions(modConfig, byIndex, byFieldId);

        foreach (GmcmPendingEdit edit in edits)
        {
            if (!TryMatch(edit, byIndex, byFieldId, out object? option, out string? reason) || option == null)
            {
                result.Skipped.Add(new GmcmSkippedEdit { Edit = edit, Reason = reason ?? "unmatched" });
                continue;
            }

            try
            {
                if (!TrySetValue(option, edit))
                {
                    result.Skipped.Add(new GmcmSkippedEdit { Edit = edit, Reason = "could not set value" });
                    continue;
                }

                result.Applied.Add(edit);
            }
            catch (Exception ex)
            {
                result.Skipped.Add(new GmcmSkippedEdit { Edit = edit, Reason = ex.Message });
            }
        }

        return result;
    }

    internal static void SaveLikeGmcm(object modConfig)
    {
        object? pages = GmcmReflection.GetMemberValue(modConfig, "Pages")
            ?? throw new InvalidOperationException("ModConfig.Pages");
        List<object> options = new();
        foreach (KeyValuePair<string, object> page in GmcmReflection.EnumeratePages(pages))
        {
            object? pageOptions = GmcmReflection.GetMemberValue(page.Value, "Options");
            foreach (object option in GmcmReflection.Enumerate(pageOptions))
                options.Add(option);
        }

        foreach (object option in options)
            GmcmReflection.TryInvoke(option, "BeforeSave", out _);

        if (!GmcmReflection.TryInvoke(modConfig, "Save", out _))
            throw new InvalidOperationException("ModConfig.Save");

        foreach (object option in options)
            GmcmReflection.TryInvoke(option, "AfterSave", out _);
    }

    internal static bool TryMatch(
        GmcmPendingEdit edit,
        IReadOnlyDictionary<(string Page, int Index), object> byIndex,
        IReadOnlyDictionary<string, object> byFieldId,
        out object? option,
        out string? reason)
    {
        option = null;
        reason = null;
        if (!string.IsNullOrEmpty(edit.FieldId) && !GmcmFieldIds.LooksLikeGuid(edit.FieldId))
        {
            if (byFieldId.TryGetValue(edit.FieldId, out option))
                return true;
            reason = "fieldId not found";
            return false;
        }

        if (!byIndex.TryGetValue((edit.Page, edit.Index), out object? candidate))
        {
            reason = "page/index not found";
            return false;
        }

        GmcmOptionCapture snapshot = GmcmWalker.WalkOption(candidate, edit.Index);
        if (!string.Equals(snapshot.Kind, edit.Kind, StringComparison.Ordinal)
            || !string.Equals(snapshot.Name, edit.Name, StringComparison.Ordinal))
        {
            reason = "kind/name mismatch";
            option = null;
            return false;
        }

        option = candidate;
        return true;
    }

    private static void IndexOptions(
        object modConfig,
        Dictionary<(string Page, int Index), object> byIndex,
        Dictionary<string, object> byFieldId)
    {
        object? pages = GmcmReflection.GetMemberValue(modConfig, "Pages");
        if (pages == null)
            return;

        foreach (KeyValuePair<string, object> page in GmcmReflection.EnumeratePages(pages))
        {
            object? options = GmcmReflection.GetMemberValue(page.Value, "Options");
            int index = 0;
            foreach (object option in GmcmReflection.Enumerate(options))
            {
                byIndex[(page.Key, index)] = option;
                string? fieldId = GmcmReflection.GetMemberValue(option, "FieldId")?.ToString();
                if (!string.IsNullOrEmpty(fieldId) && !GmcmFieldIds.LooksLikeGuid(fieldId))
                    byFieldId[fieldId] = option;
                index++;
            }
        }
    }

    private static bool TrySetValue(object option, GmcmPendingEdit edit)
    {
        string typeName = GmcmReflection.TypeName(option);
        object? value = UnwrapJson(edit.Value);
        if (typeName.StartsWith("SimpleModOption", StringComparison.Ordinal)
            || typeName.StartsWith("NumericModOption", StringComparison.Ordinal)
            || typeName.StartsWith("ChoiceModOption", StringComparison.Ordinal))
        {
            return GmcmReflection.TrySetMemberValue(option, "Value", value);
        }

        if (typeName == "ComplexModOption")
        {
            object? picker = GmcmWalker.FindGmcmOptionsTarget(option);
            if (picker == null)
                return false;
            if (value is Dictionary<string, object?> dict && picker.GetType().Name == "ColorPickerOption")
            {
                Dictionary<string, object?> colorParts = new(dict);
                colorParts.Remove("showAlpha");
                colorParts.Remove("style");
                return GmcmWalker.TryInvokeSetter(picker, colorParts);
            }

            return GmcmWalker.TryInvokeSetter(picker, value);
        }

        return false;
    }

    internal static object? UnwrapJson(object? value)
    {
        if (value is JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    Dictionary<string, object?> map = new();
                    foreach (JsonProperty property in element.EnumerateObject())
                        map[property.Name] = UnwrapJson(property.Value);
                    return map;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return element.GetBoolean();
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int i))
                        return i;
                    return element.GetDouble();
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;
                default:
                    return element.GetRawText();
            }
        }

        return value;
    }
}
