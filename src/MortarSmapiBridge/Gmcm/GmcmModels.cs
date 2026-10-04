using System;
using System.Collections.Generic;

namespace MortarSmapiBridge.Gmcm;

internal sealed class GmcmCaptureFile
{
    public int Schema { get; set; } = 1;

    public GmcmModIdentity Mod { get; set; } = new();

    public string GmcmVersion { get; set; } = "";

    public string CapturedAt { get; set; } = "";

    public bool TitleScreenOnlyDefault { get; set; }

    public List<GmcmPageCapture> Pages { get; set; } = [];
}

internal sealed class GmcmModIdentity
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string Version { get; set; } = "";
}

internal sealed class GmcmPageCapture
{
    public string Id { get; set; } = "";

    public string? Title { get; set; }

    public List<GmcmOptionCapture> Options { get; set; } = [];
}

internal sealed class GmcmOptionCapture
{
    public int Index { get; set; }

    public string Kind { get; set; } = "";

    public string? FieldId { get; set; }

    public string Name { get; set; } = "";

    public string? Tooltip { get; set; }

    public object? Value { get; set; }

    public object? Min { get; set; }

    public object? Max { get; set; }

    public object? Interval { get; set; }

    public List<GmcmChoiceCapture>? Choices { get; set; }

    public List<GmcmFormatSample>? FormatSamples { get; set; }

    public bool Editable { get; set; } = true;

    public bool TitleScreenOnly { get; set; }
}

internal sealed class GmcmChoiceCapture
{
    public object? Value { get; set; }

    public string Label { get; set; } = "";
}

internal sealed class GmcmFormatSample
{
    public object? Value { get; set; }

    public string Label { get; set; } = "";
}

/// <summary>The one file-format version the bridge and Mortar exchange; either side refuses any other.</summary>
internal static class GmcmSchema
{
    public const int Current = 1;
}

internal sealed class GmcmIndexFile
{
    public int Schema { get; set; } = 1;

    public string GmcmVersion { get; set; } = "";

    public string CapturedAt { get; set; } = "";

    public List<GmcmModIdentity> Mods { get; set; } = [];
}

internal sealed class GmcmPendingFile
{
    public int Schema { get; set; } = 1;

    public List<GmcmPendingEdit> Edits { get; set; } = [];
}

internal sealed class GmcmPendingEdit
{
    public string Page { get; set; } = "";

    public int Index { get; set; }

    public string Kind { get; set; } = "";

    public string? FieldId { get; set; }

    public string Name { get; set; } = "";

    public object? Value { get; set; }
}

internal sealed class GmcmPendingResultFile
{
    public List<GmcmPendingEdit> Applied { get; set; } = [];

    public List<GmcmSkippedEdit> Skipped { get; set; } = [];
}

internal sealed class GmcmSkippedEdit
{
    public GmcmPendingEdit Edit { get; set; } = new();

    public string Reason { get; set; } = "";
}

internal static class GmcmFieldIds
{
    public static bool LooksLikeGuid(string? fieldId)
    {
        return !string.IsNullOrEmpty(fieldId) && Guid.TryParse(fieldId, out _);
    }
}
