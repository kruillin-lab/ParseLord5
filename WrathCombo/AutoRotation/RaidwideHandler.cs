#region

using FFXIVClientStructs.FFXIV.Client.Game;
using System.Collections.Generic;
using WrathCombo.Combos.PvE;
using WrathCombo.Core;
using WrathCombo.CustomComboNS;
using WrathCombo.CustomComboNS.Functions;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;
using static WrathCombo.Data.ActionWatching;
using ActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType;

#endregion

namespace WrathCombo.AutoRotation;

internal unsafe partial class AutoRotationController
{
    public static IEnumerable<uint> TankbusterActions =
    [
        WHM.Aquaveil,
        WHM.DivineBenison,
        SCH.Protraction,
        SCH.Adloquium,
        SCH.Manifestation,
        AST.Spire,
        AST.Bole,
        AST.CelestialIntersection,
        AST.Exaltation,
        SGE.Taurochole,
        SGE.Eukrasia,
        SGE.EukrasianDiagnosis,
    ];

    private static void HandleTankbuster(ulong? safeGameObjectId)
    {
        if (safeGameObjectId == null)
            return;

        foreach (var spell in TankbusterActions)
        {
            if (TankbusterHandled)
                return;

            if (AbleToCast(spell, safeGameObjectId))
            {
                var act = spell;
                if (act == AST.Bole) act = AST.Play2;
                if (act == AST.Spire) act = AST.Play3;
                WouldLikeToGroundTarget = ActionSheet[act].TargetArea;
                ActionManager.Instance()->UseAction(ActionType.Action, act is SGE.Eukrasia ? act.Retarget(SimpleTarget.Self) : act.Retarget(safeGameObjectId.GetObject()), safeGameObjectId!.Value);
                WouldLikeToGroundTarget = false;
                if (act != SGE.Eukrasia)
                    TankbusterHandled = true;
                return;
            }
        }
    }

    private static bool AbleToCast(uint spell, ulong? safeGameObjectId = null)
    {
        return ActionReady(spell) && (safeGameObjectId != null ? !JustUsedOn(spell, safeGameObjectId.GetObject(), 5) : !JustUsed(spell, 10)) && LocalPlayer.CastActionId != spell && (!IsMoving(true) || ActionManager.GetAdjustedCastTime(ActionType.Action, spell) == 0);
    }

    public static IEnumerable<(uint Action, bool MultiHitOnly)> RaidwideActions =
    [
        (WHM.LiturgyOfTheBell.Retarget(SimpleTarget.Self), true),
        (WHM.PlenaryIndulgence, false),
        (WHM.Temperance, false),
        (WHM.DivineCaress, false),
        (WHM.Asylum.Retarget(SimpleTarget.Self), false),
        (WHM.Medica2, false),
        (WHM.Medica3, false),
        (SCH.Expedient, false),
        (SCH.Seraphism, false),
        (SCH.Succor, false),
        (SCH.Accession, false),
        (SCH.Concitation, false),
        (AST.CollectiveUnconscious, false),
        (AST.SunSign, false),
        (AST.CelestialOpposition, false),
        (AST.AspectedHelios, false),
        (AST.HeliosConjuction, false),
        (SGE.Panhaima, true),
        (SGE.Kerachole, false),
        (SGE.Physis, false),
        (SGE.Physis2, false),
        (SGE.Holos, false),
        (SGE.Eukrasia, false),
        (SGE.EukrasianPrognosis, false),
        (SGE.EukrasianPrognosis2, false),
    ];

    public static List<uint> BlacklistedRaidwides = [];

    private static void HandleRaidwide(bool multihit)
    {
        foreach (var (spell, multihitter) in RaidwideActions)
        {
            if (AutorotRaidwides >= 2)
                return;

            if (!multihit && multihitter)
                continue;

            if (BlacklistedRaidwides.Contains(spell))
                continue;

            if (AbleToCast(spell))
            {
                WouldLikeToGroundTarget = ActionSheet[spell].TargetArea;
                ActionManager.Instance()->UseAction(ActionType.Action, spell);
                WouldLikeToGroundTarget = false;
                return;
            }
        }
    }
}
