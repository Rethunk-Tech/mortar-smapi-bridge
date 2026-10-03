using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MortarSmapiBridge.Gmcm;

internal sealed class GmcmCaptureFile
{
    [JsonPropertyName("schema")]
    public int Schema { get; set; } = 1;

    [JsonPropertyName("mod")]
    public GmcmModIdentity Mod { get; set; } = new();

    [JsonPropertyName("gmcmVersion")]
    public string GmcmVersion { get; set; } = "";

    [JsonPropertyName("capturedAt")]
    public string CapturedAt { get; set; } = "";

    [JsonPropertyName("titleScreenOnlyDefault")]
    public bool TitleScreenOnlyDefault { get; set; }

    [JsonPropertyName("pages")]
    public List<GmcmPageCapture> Pages { get; set; } = new();
}

internal sealed class GmcmModIdentity
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";
}

internal sealed class GmcmPageCapture
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("options")]
    public List<GmcmOptionCapture> Options { get; set; } = new();
}

internal sealed class GmcmOptionCapture
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    [JsonPropertyName("fieldId")]
    public string? FieldId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("tooltip")]
    public string? Tooltip { get; set; }

    [JsonPropertyName("value")]
    public object? Value { get; set; }

    [JsonPropertyName("min")]
    public object? Min { get; set; }

    [JsonPropertyName("max")]
    public object? Max { get; set; }

    [JsonPropertyName("interval")]
    public object? Interval { get; set; }

    [JsonPropertyName("choices")]
    public List<GmcmChoiceCapture>? Choices { get; set; }

    [JsonPropertyName("formatSamples")]
    public List<GmcmFormatSample>? FormatSamples { get; set; }

    [JsonPropertyName("editable")]
    public bool Editable { get; set; } = true;

    [JsonPropertyName("titleScreenOnly")]
    public bool TitleScreenOnly { get; set; }
}

internal sealed class GmcmChoiceCapture
{
    [JsonPropertyName("value")]
    public object? Value { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";
}

internal sealed class GmcmFormatSample
{
    [JsonPropertyName("value")]
    public object? Value { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";
}

internal sealed class GmcmIndexFile
{
    [JsonPropertyName("schema")]
    public int Schema { get; set; } = 1;

    [JsonPropertyName("gmcmVersion")]
    public string GmcmVersion { get; set; } = "";

    [JsonPropertyName("capturedAt")]
    public string CapturedAt { get; set; } = "";

    [JsonPropertyName("mods")]
    public List<GmcmIndexEntry> Mods { get; set; } = new();
}

internal sealed class GmcmIndexEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";
}

internal sealed class GmcmPendingFile
{
    [JsonPropertyName("schema")]
    public int Schema { get; set; } = 1;

    [JsonPropertyName("edits")]
    public List<GmcmPendingEdit> Edits { get; set; } = new();
}

internal sealed class GmcmPendingEdit
{
    [JsonPropertyName("page")]
    public string Page { get; set; } = "";

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    [JsonPropertyName("fieldId")]
    public string? FieldId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("value")]
    public object? Value { get; set; }
}

internal sealed class GmcmPendingResultFile
{
    [JsonPropertyName("applied")]
    public List<GmcmPendingEdit> Applied { get; set; } = new();

    [JsonPropertyName("skipped")]
    public List<GmcmSkippedEdit> Skipped { get; set; } = new();
}

internal sealed class GmcmSkippedEdit
{
    [JsonPropertyName("edit")]
    public GmcmPendingEdit Edit { get; set; } = new();

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";
}

internal static class GmcmFieldIds
{
    public static bool LooksLikeGuid(string? fieldId)
    {
        return !string.IsNullOrEmpty(fieldId) && Guid.TryParse(fieldId, out _);
    }
}
