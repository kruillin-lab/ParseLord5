using System;
using Dalamud.Game.ClientState.Objects.Types;
using WrathCombo.CustomComboNS.Functions;
using WrathCombo.Services.TankCooldownHelperIPC;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Services.SmartMitigation;

/// <summary>Shared threat / pressure detection for tank smart mitigation (WAR, PLD, GNB, DRK).</summary>
internal readonly record struct TankThreatState(
    bool ConfirmedTankbuster,
    bool SoftTankbuster,
    bool Raidwide,
    float MechanicSpikeFraction,
    bool SustainedPressure);

internal static class TankSmartMitigationThreat
{
    internal static TankThreatState Detect(bool isBoss, PlayerPressureState pressure)
    {
        var confirmedTankbuster = HasIncomingTankBusterEffect(out _);

        if (!confirmedTankbuster &&
            TryGetTankBusterTarget(out IBattleChara tbTarget) &&
            LocalPlayer is { } player &&
            tbTarget.GameObjectId == player.GameObjectId)
        {
            confirmedTankbuster = true;
        }

        // Targeting alone isn't evidence of an incoming spike -- a tank holds
        // aggro (and thus IsPlayerTargeted()) for the entire pull. Require a
        // recent real hit (rolling MaxSingleHit window) so this only fires
        // when damage is actually landing, not just "boss fight in progress".
        var softTankbuster = !confirmedTankbuster && isBoss && IsPlayerTargeted() &&
            pressure.MaxSingleHit > 0f;
        var raidwide = GroupDamageIncoming();

        var mechanicSpikeFraction = 0f;
        if (confirmedTankbuster)
            mechanicSpikeFraction = Math.Max(mechanicSpikeFraction, MitigationCoverageCalculator.TankbusterSpikeFraction);
        else if (softTankbuster)
            mechanicSpikeFraction = Math.Max(mechanicSpikeFraction, MitigationCoverageCalculator.SoftTankbusterSpikeFraction);

        if (raidwide)
            mechanicSpikeFraction = Math.Max(mechanicSpikeFraction, MitigationCoverageCalculator.RaidwideSpikeFraction);

        if (LocalPlayer is { MaxHp: > 0 } hpPlayer && pressure.MaxSingleHit > 0f)
            mechanicSpikeFraction = Math.Max(mechanicSpikeFraction, pressure.MaxSingleHit / hpPlayer.MaxHp);

        var deathTimerThreshold = isBoss ? 10f : 12f;
        var sustainedPressure = pressure.TankCooldownCritical ||
            pressure.TankCooldownInDanger ||
            (pressure.NetDps > 0f && (
                pressure.DangerRatio >= (isBoss ? 0.8f : 1.0f) ||
                (pressure.SecondsUntilDeath is { } ttd && ttd <= deathTimerThreshold))) ||
            confirmedTankbuster;

        return new TankThreatState(confirmedTankbuster, softTankbuster, raidwide, mechanicSpikeFraction, sustainedPressure);
    }

    internal static bool HasMitigationThreat(TankThreatState threat, bool isBoss, PlayerPressureState pressure)
    {
        if (pressure.TankCooldownEmergency)
            return true;

        if (threat.ConfirmedTankbuster || threat.Raidwide)
            return true;

        if (pressure.FromTankCooldownHelper)
        {
            if (pressure.TankCooldownCritical)
                return true;

            if (threat.SoftTankbuster && pressure.TankCooldownInDanger)
                return true;

            if (threat.SustainedPressure)
                return true;

            if (threat.MechanicSpikeFraction >= MitigationCoverageCalculator.RaidwideSpikeFraction)
                return true;

            return pressure.TankCooldownInDanger;
        }

        if (threat.SoftTankbuster && pressure.DangerRatio >= 0.9f)
            return true;

        if (threat.SustainedPressure && pressure.NetDps > 0f)
            return true;

        if (threat.MechanicSpikeFraction >= MitigationCoverageCalculator.RaidwideSpikeFraction)
            return true;

        return pressure.DangerRatio >= (isBoss ? 1.0f : 1.2f) && pressure.NetDps > 0f;
    }

    internal static PlayerPressureState GetPlayerPressure(uint objectId)
    {
        if (TankCooldownHelperIpcClient.TryGetPlayerPressure(objectId, out var pressure))
            return pressure;

        return CombatTelemetryService.GetPlayerPressure(objectId);
    }

    /// <summary>Strict TB telegraph or TCH Emergency in combat below 50% HP (WAR Damnation-style gate).</summary>
    internal static bool ShouldOfferHeavyMitigation(
        TankThreatState threat,
        PlayerPressureState pressure,
        uint currentHp,
        uint maxHp)
    {
        _ = threat;
        if (HasIncomingTankBusterEffect(out _))
            return true;

        if (!InCombat() || maxHp == 0)
            return false;

        if (!pressure.FromTankCooldownHelper || !pressure.TankCooldownEmergency)
            return false;

        return currentHp * 100u < maxHp * 50u;
    }

    internal static float ResolveMechanicSpikeFraction(
        TankThreatState threat,
        PlayerPressureState pressure)
    {
        var spike = threat.MechanicSpikeFraction;

        if (threat.ConfirmedTankbuster)
            spike = Math.Max(spike, MitigationCoverageCalculator.TankbusterSpikeFraction);

        if (threat.SoftTankbuster)
            spike = Math.Max(spike, MitigationCoverageCalculator.SoftTankbusterSpikeFraction);

        if (threat.Raidwide)
            spike = Math.Max(spike, MitigationCoverageCalculator.RaidwideSpikeFraction);

        if (pressure.MaxSingleHit > 0f && LocalPlayer is { MaxHp: > 0 } player)
            spike = Math.Max(spike, pressure.MaxSingleHit / player.MaxHp);

        if (pressure.TankCooldownEmergency)
            spike = Math.Max(spike, MitigationCoverageCalculator.TankbusterSpikeFraction);
        else if (pressure.TankCooldownCritical)
            spike = Math.Max(spike, MitigationCoverageCalculator.SoftTankbusterSpikeFraction);
        else if (pressure.TankCooldownInDanger)
            spike = Math.Max(spike, MitigationCoverageCalculator.RaidwideSpikeFraction);

        return spike;
    }
}
