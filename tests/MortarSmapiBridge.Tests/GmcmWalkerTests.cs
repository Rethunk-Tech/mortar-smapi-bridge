using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GMCMOptions.Framework;
using MortarSmapiBridge.Gmcm;
using Xunit;

namespace MortarSmapiBridge.Tests
{

public sealed class GmcmWalkerTests
{
    [Fact]
    public void WalksRegistrationOrderAndNullsGuidFieldIds()
    {
        ModConfig config = SampleConfig();
        GmcmCaptureFile file = GmcmWalker.WalkModConfig(
            config,
            new GmcmModIdentity { Id = "demo.Mod", Name = "Demo", Version = "1.0.0" },
            "1.12.0",
            DateTimeOffset.Parse("2026-01-02T03:04:05Z"));

        Assert.Equal(1, file.Schema);
        Assert.Equal("demo.Mod", file.Mod.Id);
        Assert.True(file.TitleScreenOnlyDefault);
        Assert.Equal(2, file.Pages.Count);
        Assert.Equal("", file.Pages[0].Id);
        Assert.Equal("extra", file.Pages[1].Id);
        Assert.Equal("Extras", file.Pages[1].Title);

        GmcmOptionCapture checkbox = file.Pages[0].Options[0];
        Assert.Equal(0, checkbox.Index);
        Assert.Equal("bool", checkbox.Kind);
        Assert.Equal("enableFeature", checkbox.FieldId);
        Assert.Equal("Enable feature", checkbox.Name);
        Assert.Equal(true, checkbox.Value);
        Assert.True(checkbox.Editable);

        GmcmOptionCapture guidField = file.Pages[0].Options[1];
        Assert.Null(guidField.FieldId);
        Assert.Equal("string", guidField.Kind);
        Assert.Equal("Player name", guidField.Name);

        GmcmOptionCapture numeric = file.Pages[0].Options[2];
        Assert.Equal("int", numeric.Kind);
        Assert.Equal(1, numeric.Min);
        Assert.Equal(5, numeric.Max);
        Assert.Equal(2, numeric.Interval);
        Assert.Equal(3, numeric.FormatSamples!.Count);
        Assert.Equal("n=1", numeric.FormatSamples[0].Label);

        GmcmOptionCapture choice = file.Pages[0].Options[3];
        Assert.Equal("choice", choice.Kind);
        Assert.Equal("b", choice.Value);
        Assert.Equal("Bee", choice.Choices!.Single(c => (string?)c.Value == "b").Label);

        Assert.Equal("sectionTitle", file.Pages[0].Options[4].Kind);
        Assert.False(file.Pages[0].Options[4].Editable);
        Assert.Equal("pageLink", file.Pages[0].Options[5].Kind);
        Assert.Equal("extra", file.Pages[0].Options[5].Value);
        Assert.Equal("complex", file.Pages[0].Options[6].Kind);
        Assert.False(file.Pages[0].Options[6].Editable);

        GmcmOptionCapture color = file.Pages[0].Options[7];
        Assert.Equal("color", color.Kind);
        Assert.True(color.Editable);
        Dictionary<string, object?> colorValue = Assert.IsType<Dictionary<string, object?>>(color.Value);
        Assert.Equal(10, colorValue["r"]);
        Assert.Equal(true, colorValue["showAlpha"]);

        GmcmOptionCapture image = file.Pages[0].Options[8];
        Assert.Equal("image", image.Kind);
        Assert.True(image.Editable);
        Assert.Equal(1, image.Value);
        Assert.Equal(2, image.Max);
    }

    [Fact]
    public void CaptureJsonShapeOmitsGuidFieldId()
    {
        GmcmCaptureFile file = GmcmWalker.WalkModConfig(
            SampleConfig(),
            new GmcmModIdentity { Id = "demo.Mod", Name = "Demo", Version = "1.0.0" },
            "1.12.0",
            DateTimeOffset.Parse("2026-01-02T03:04:05Z"));
        string json = JsonSerializer.Serialize(file, GmcmSession.JsonOptions);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement options = doc.RootElement.GetProperty("pages")[0].GetProperty("options");
        Assert.Equal("enableFeature", options[0].GetProperty("fieldId").GetString());
        Assert.False(options[1].TryGetProperty("fieldId", out _));
        Assert.Equal(1, doc.RootElement.GetProperty("schema").GetInt32());
    }

    [Fact]
    public void MatchesStableFieldIdElsePageIndexKindName()
    {
        ModConfig config = SampleConfig();
        List<GmcmPendingEdit> edits = new()
        {
            new GmcmPendingEdit
            {
                Page = "",
                Index = 99,
                Kind = "bool",
                FieldId = "enableFeature",
                Name = "wrong",
                Value = false
            },
            new GmcmPendingEdit
            {
                Page = "",
                Index = 1,
                Kind = "string",
                FieldId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                Name = "Player name",
                Value = "Sam"
            },
            new GmcmPendingEdit
            {
                Page = "",
                Index = 4,
                Kind = "bool",
                Name = "Section",
                Value = true
            }
        };

        GmcmPendingResultFile result = GmcmApply.ApplyEdits(config, edits);
        Assert.False((bool)config.Pages[""].Options[0].GetType().GetProperty("Value")!.GetValue(config.Pages[""].Options[0])!);
        Assert.Equal("Sam", config.Pages[""].Options[1].GetType().GetProperty("Value")!.GetValue(config.Pages[""].Options[1]));
        Assert.Single(result.Skipped);
        Assert.Equal("kind/name mismatch", result.Skipped[0].Reason);
        Assert.Equal(2, result.Applied.Count);
    }

    [Fact]
    public void LooksLikeGuidDetectsGmcmGeneratedIds()
    {
        Assert.True(GmcmFieldIds.LooksLikeGuid("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        Assert.False(GmcmFieldIds.LooksLikeGuid("enableFeature"));
        Assert.False(GmcmFieldIds.LooksLikeGuid(null));
    }

    [Fact]
    public void SaveRunsBeforeSaveThenSaveThenAfterSave()
    {
        ModConfig config = SampleConfig();
        List<string> order = config.SaveLog;
        GmcmApply.SaveLikeGmcm(config);
        Assert.Equal(
            Enumerable.Repeat("before", 10).Concat(new[] { "save" }).Concat(Enumerable.Repeat("after", 10)).ToArray(),
            order);
    }

    [Fact]
    public void AtomicWriteReplacesViaTempRename()
    {
        string dir = Path.Combine(Path.GetTempPath(), "gmcm-bridge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "demo.Mod.json");
        GmcmSession.AtomicWrite(path, "{\"a\":1}");
        GmcmSession.AtomicWrite(path, "{\"a\":2}");
        Assert.Equal("{\"a\":2}", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
    }

    private static ModConfig SampleConfig()
    {
        ColorPickerOption colorPicker = new()
        {
            getValue = () => new Color { R = 10, G = 20, B = 30, A = 40 },
            setValue = _ => { },
            showAlpha = true,
            style = 2
        };
        ImagePickerOption imagePicker = new()
        {
            getValue = () => 1,
            setValue = _ => { },
            MaxValue = 2,
            Labels = new[] { "one", "two", "three" }
        };

        ModConfig config = new()
        {
            DefaultTitleScreenOnly = true,
            ModManifest = new Manifest { UniqueID = "demo.Mod", Name = "Demo", Version = "1.0.0" },
            Pages =
            {
                [""] = new ModConfigPage
                {
                    Options =
                    {
                        new SimpleModOption<bool> { FieldId = "enableFeature", Name = () => "Enable feature", Tooltip = () => "on/off", Value = true },
                        new SimpleModOption<string> { FieldId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", Name = () => "Player name", Value = "Alex" },
                        new NumericModOption<int>
                        {
                            FieldId = "count",
                            Name = () => "Count",
                            Value = 3,
                            Minimum = 1,
                            Maximum = 5,
                            Interval = 2,
                            FormatValue = v => "n=" + v
                        },
                        new ChoiceModOption<string>
                        {
                            FieldId = "pick",
                            Name = () => "Pick",
                            Value = "b",
                            Choices = new[] { "a", "b" },
                            FormatChoice = v => v == "a" ? "Aye" : "Bee"
                        },
                        new SectionTitleModOption { Name = () => "Section" },
                        new PageLinkModOption { Name = () => "More", PageId = "extra" },
                        new ComplexModOption { Name = () => "Opaque", Draw = _ => { } },
                        new ComplexModOption { Name = () => "Tint", Draw = colorPicker.Draw },
                        new ComplexModOption { Name = () => "Portrait", Draw = imagePicker.Draw }
                    }
                },
                ["extra"] = new ModConfigPage
                {
                    PageTitle = () => "Extras",
                    Options = { new ParagraphModOption { Name = () => "Hello" } }
                }
            }
        };
        foreach (ModConfigPage page in config.Pages.Values)
        {
            foreach (BaseModOption option in page.Options)
                option.Log = config.SaveLog;
        }

        return config;
    }

    public sealed class Manifest
    {
        public string UniqueID { get; set; } = "";
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
    }

    public sealed class ModConfig
    {
        public Manifest ModManifest { get; set; } = new();
        public bool DefaultTitleScreenOnly { get; set; }
        public Dictionary<string, ModConfigPage> Pages { get; set; } = new();
        public List<string> SaveLog { get; } = new();

        public void Save() => SaveLog.Add("save");
    }

    public sealed class ModConfigPage
    {
        public Func<string>? PageTitle { get; set; }
        public List<BaseModOption> Options { get; set; } = new();
    }

    public abstract class BaseModOption
    {
        public string FieldId { get; set; } = "";
        public Func<string> Name { get; set; } = () => "";
        public Func<string> Tooltip { get; set; } = () => "";
        public bool IsTitleScreenOnly { get; set; }
        public List<string>? Log { get; set; }

        public void BeforeSave() => Log?.Add("before");

        public void AfterSave() => Log?.Add("after");
    }

    public sealed class SimpleModOption<T> : BaseModOption
    {
        public T Value { get; set; } = default!;
    }

    public sealed class NumericModOption<T> : BaseModOption
    {
        public T Value { get; set; } = default!;
        public T Minimum { get; set; } = default!;
        public T Maximum { get; set; } = default!;
        public T Interval { get; set; } = default!;
        public Func<T, string>? FormatValue { get; set; }
    }

    public sealed class ChoiceModOption<T> : BaseModOption
    {
        public T Value { get; set; } = default!;
        public T[] Choices { get; set; } = Array.Empty<T>();
        public Func<T, string>? FormatChoice { get; set; }
    }

    public sealed class PageLinkModOption : BaseModOption
    {
        public string PageId { get; set; } = "";
    }

    public sealed class SectionTitleModOption : BaseModOption;

    public sealed class SectionSubHeaderModOption : BaseModOption;

    public sealed class ParagraphModOption : BaseModOption;

    public sealed class ImageModOption : BaseModOption;

    public sealed class ComplexModOption : BaseModOption
    {
        public Action<int> Draw { get; set; } = _ => { };
    }

    public sealed class Color
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public byte A { get; set; }
    }
}
}

namespace GMCMOptions.Framework
{
    public sealed class ColorPickerOption
    {
        public Func<MortarSmapiBridge.Tests.GmcmWalkerTests.Color> getValue { get; set; } = () => new();
        public Action<MortarSmapiBridge.Tests.GmcmWalkerTests.Color> setValue { get; set; } = _ => { };
        public bool showAlpha { get; set; }
        public int style { get; set; }

        public void Draw(int _) { }
    }

    public sealed class ImagePickerOption
    {
        public Func<int> getValue { get; set; } = () => 0;
        public Action<int> setValue { get; set; } = _ => { };
        public int MaxValue { get; set; }
        public string[] Labels { get; set; } = Array.Empty<string>();

        public void Draw(int _) { }
    }
}
