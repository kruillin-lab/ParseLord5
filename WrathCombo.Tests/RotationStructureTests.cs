using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace WrathCombo.Tests;

/// <summary>
///     Cross-platform port of scripts/rotation-evals.ps1's structural checks:
///     preset enum parses, every job has presets, preset IDs are unique.
///     Reads source files as text from the repo root located via CallerFilePath.
/// </summary>
public class RotationStructureTests
{
    private static string RepoRoot([CallerFilePath] string thisFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));

    private static readonly Regex PresetEntry =
        new(@"^\s+(\w+) = (\d+),?", RegexOptions.Multiline);

    private static MatchCollection PresetMatches()
    {
        var presetFile = Path.Combine(RepoRoot(), "WrathCombo", "Combos", "CustomComboPreset.cs");
        Assert.True(File.Exists(presetFile), $"Preset file missing: {presetFile}");
        return PresetEntry.Matches(File.ReadAllText(presetFile));
    }

    private static string ExtractClassSource(string source, string className)
    {
        var marker = $"internal class {className}";
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find class {className}");

        var next = source.IndexOf("internal class ", start + marker.Length, StringComparison.Ordinal);
        return next >= 0 ? source[start..next] : source[start..];
    }

    [Fact]
    public void PresetEnum_HasEntries()
        => Assert.True(PresetMatches().Count > 100,
            "CustomComboPreset.cs parsed to almost no entries - regex or file drifted");

    [Fact]
    public void PresetIds_AreUnique()
    {
        var dupes = PresetMatches().Select(m => m.Groups[2].Value)
            .GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(dupes.Count == 0, $"Duplicate preset IDs: {string.Join(", ", dupes)}");
    }

    [Fact]
    public void EveryJobDir_HasPresets()
    {
        var combosDir = Path.Combine(RepoRoot(), "WrathCombo", "Combos", "PvE");
        var names = PresetMatches().Select(m => m.Groups[1].Value).ToList();
        var missing = new List<string>();
        foreach (var dir in Directory.GetDirectories(combosDir))
        {
            var job = Path.GetFileName(dir);
            if (!Regex.IsMatch(job, "^(ALL|[A-Z]{3})$")) continue;
            if (!names.Any(n => n.StartsWith(job + "_", StringComparison.Ordinal)))
                missing.Add(job);
        }
        Assert.True(missing.Count == 0, $"Jobs with zero presets: {string.Join(", ", missing)}");
    }

    [Fact]
    public void HealerRaidwideHandler_RequiresGroupedContentOrBossContext()
    {
        var controllerFile = Path.Combine(RepoRoot(), "WrathCombo", "AutoRotation", "AutoRotationController.cs");
        var source = File.ReadAllText(controllerFile);

        Assert.Contains("ShouldHandleHealerRaidwides(isHealer) && GroupDamageIncoming", source);
        Assert.Matches(
            @"private static bool ShouldHandleHealerRaidwides\(bool isHealer\)[\s\S]*InBossEncounter\(\)[\s\S]*InDuty\(\) && IsInParty\(2\)",
            source);
    }

    [Fact]
    public void PartyAverageHp_NoMembersDefaultsToHealthy()
    {
        var partyFile = Path.Combine(RepoRoot(), "WrathCombo", "CustomCombo", "Functions", "Party.cs");
        var source = File.ReadAllText(partyFile);

        Assert.Contains("return count == 0 ? 100 : totalHP / count;", source);
    }

    [Fact]
    public void SgeHealRaidwideFeatures_AreSuppressedDuringAutorotationSelection()
    {
        var sgeFile = Path.Combine(RepoRoot(), "WrathCombo", "Combos", "PvE", "SGE", "SGE.cs");
        var source = File.ReadAllText(sgeFile);

        foreach (var className in new[] { "SGE_ST_Heal_AdvancedMode", "SGE_AoE_Heal_AdvancedMode" })
        {
            var classSource = ExtractClassSource(source, className);
            var guardIndex = classSource.IndexOf("if (!AutoRotationController.IsSelectingAutorotAction)", StringComparison.Ordinal);
            var keracholeIndex = classSource.IndexOf("RaidwideKerachole()", StringComparison.Ordinal);

            Assert.True(guardIndex >= 0, $"{className} does not guard raidwide features from autorotation selection");
            Assert.True(keracholeIndex >= 0, $"{className} no longer has a Kerachole raidwide feature to guard");
            Assert.True(guardIndex < keracholeIndex, $"{className} checks raidwide features before the autorotation-selection guard");
            Assert.Contains("RaidwideHolos()", classSource);
            Assert.Contains("RaidwideEprognosis()", classSource);
        }
    }

    [Fact]
    public void AutorotationDpsLane_BlocksSgeDefensiveActions()
    {
        var controllerFile = Path.Combine(RepoRoot(), "WrathCombo", "AutoRotation", "AutoRotationController.cs");
        var source = File.ReadAllText(controllerFile);

        Assert.Contains("private static bool CanUseAutorotDpsAction(uint outAct)", source);
        Assert.Contains("SGE.Rhizomata or", source);
        Assert.Contains("SGE.Kerachole or", source);
        Assert.Contains("SGE.EukrasianDiagnosis or", source);
        Assert.Contains("SGE.EukrasianPrognosis2", source);
        Assert.DoesNotContain("SGE.Eukrasia or", source);
        Assert.Matches(
            @"uint outAct = OriginalHook\(InvokeCombo\(preset, attributes, ref gameAct, OverrideTarget\)\);\s*if \(!CanUseAutorotDpsAction\(outAct\)\)",
            source);
        Assert.Matches(
            @"var outAct = OriginalHook\(InvokeCombo\(preset, attributes, ref gameAct, target\)\);\s*if \(!attributes\.AutoAction!\.IsHeal && !CanUseAutorotDpsAction\(outAct\)\)",
            source);
    }

    [Fact]
    public void PreemptiveShield_DoesNotRunDuringCombat()
    {
        var controllerFile = Path.Combine(RepoRoot(), "WrathCombo", "AutoRotation", "AutoRotationController.cs");
        var source = File.ReadAllText(controllerFile);

        Assert.Matches(
            @"private static void PreEmptiveShield\(\)[\s\S]*if \(InCombat\(\) \|\| PartyInCombat\(\)",
            source);
    }

    [Fact]
    public void SoloHealerTargeting_RequiresPlayerBelowHealThreshold()
    {
        var controllerFile = Path.Combine(RepoRoot(), "WrathCombo", "AutoRotation", "AutoRotationController.cs");
        var source = File.ReadAllText(controllerFile);

        Assert.DoesNotContain("if (GetPartyMembers().Count == 0) return Player.Object;", source);
        Assert.Contains("PlayerNeedsSingleTargetHeal()", source);
        Assert.Matches(
            @"private static bool PlayerNeedsSingleTargetHeal\(\)[\s\S]*GetTargetHPPercent\(Player\.Object, cfg\.HealerSettings\.IncludeShields\)[\s\S]*cfg\.HealerSettings\.SingleTargetHPP",
            source);
    }

    [Fact]
    public void AstAndWhmDpsCombos_DoNotUseForkOnlyDpsHealPriority()
    {
        var cases = new[]
        {
            ("AST", "AST_ST_Simple_DPS"),
            ("AST", "AST_AOE_Simple_DPS"),
            ("AST", "AST_ST_DPS"),
            ("AST", "AST_AOE_DPS"),
            ("WHM", "WHM_ST_Simple_DPS"),
            ("WHM", "WHM_AoE_Simple_DPS"),
            ("WHM", "WHM_ST_MainCombo"),
            ("WHM", "WHM_AoE_DPS"),
        };

        foreach (var (job, className) in cases)
        {
            var file = Path.Combine(RepoRoot(), "WrathCombo", "Combos", "PvE", job, $"{job}.cs");
            var classSource = ExtractClassSource(File.ReadAllText(file), className);

            Assert.DoesNotContain("TryDpsSingleTargetHealPriority", classSource);
            Assert.DoesNotContain("TryDpsAoEHealPriority", classSource);
        }
    }

    [Fact]
    public void WhmOffensiveWeaves_AreNotSuppressedByAutorotationSelectionFlag()
    {
        var whmFile = Path.Combine(RepoRoot(), "WrathCombo", "Combos", "PvE", "WHM", "WHM.cs");
        var source = File.ReadAllText(whmFile);

        Assert.Contains("var canAssize", source);
        Assert.Contains("var canPresenceOfMind", source);
        Assert.DoesNotContain("if (!AutoRotationController.IsSelectingAutorotAction && CanWeave())", source);
        Assert.DoesNotContain("if (!AutoRotationController.IsSelectingAutorotAction && (CanWeave() || IsMoving()))", source);
    }

    [Fact]
    public void AutorotationProbeContext_IsOptIn()
    {
        var controller = Path.Combine(RepoRoot(), "WrathCombo", "AutoRotation", "AutoRotationController.cs");
        var source = File.ReadAllText(controller);

        Assert.Contains("IGameObject? optionalTarget = null, bool selectingAutorotAction = false)", source);
        Assert.Contains("IsSelectingAutorotAction = selectingAutorotAction;", source);
        Assert.DoesNotContain("IsSelectingAutorotAction = true;", source);
        Assert.Single(Regex.Matches(source, @"selectingAutorotAction:\s*true"));
        Assert.Matches(@"attr\.AutoAction\?\.IsHeal == true && ActionReady\(AutoRotationHelper\.InvokeCombo\(x\.Key, attr, ref _, selectingAutorotAction: true\)\)", source);
    }
}
