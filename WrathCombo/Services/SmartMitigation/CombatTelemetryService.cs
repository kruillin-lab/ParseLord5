using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameHelpers;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace WrathCombo.Services.SmartMitigation;

/// <summary>
/// Rolling HP-delta telemetry for ParseLord5 smart mitigation experiments.
/// </summary>
internal static class CombatTelemetryService
{
    private const float WindowSeconds = 5f;
    private static readonly CombatEventBuffer Buffer = new(WindowSeconds);
    private static readonly ConcurrentDictionary<uint, uint> LastHpValues = new();

    internal static void Update()
    {
        if (Player.Object is not { } player)
            return;

        Buffer.PruneOldEvents();
        TrackHpChange((uint)player.GameObjectId, player.CurrentHp);

        foreach (var member in Svc.Party)
        {
            if (member?.GameObject is not Dalamud.Game.ClientState.Objects.Types.ICharacter character)
                continue;

            TrackHpChange((uint)member.GameObject.GameObjectId, character.CurrentHp);
        }
    }

    internal static void Clear()
    {
        Buffer.Clear();
        LastHpValues.Clear();
    }

    /// <summary>
    ///     Largest single hit taken by <paramref name="objectId"/> inside the rolling window.
    ///     Exposed so pressure from sources that omit the field (TankCooldownHelper IPC) can be backfilled.
    /// </summary>
    internal static float GetMaxSingleHit(uint objectId) => Buffer.GetMaxSingleHit(objectId);

    internal static PlayerPressureState GetPlayerPressure(uint objectId)
    {
        var totalDamage = Buffer.GetTotalDamage(objectId);
        var totalHealing = Buffer.GetTotalHealing(objectId);
        var dps = totalDamage / WindowSeconds;
        var hps = totalHealing / WindowSeconds;
        var netDps = Math.Max(0f, dps - hps);
        var ratio = hps <= 0f ? (dps > 0f ? 99.9f : 0f) : dps / hps;

        float? deathTimer = null;
        if (Player.Object is { } player && (uint)player.GameObjectId == objectId && netDps > 1f)
            deathTimer = player.CurrentHp / netDps;

        return new PlayerPressureState(
            dps,
            hps,
            netDps,
            ratio,
            Buffer.GetMaxSingleHit(objectId),
            deathTimer);
    }

    private static void TrackHpChange(uint objectId, uint currentHp)
    {
        if (LastHpValues.TryGetValue(objectId, out var lastHp))
        {
            var hpDiff = (int)currentHp - (int)lastHp;
            if (hpDiff < 0)
            {
                Buffer.AddEvent(new CombatEvent(
                    objectId, 0, 0, Math.Abs(hpDiff), CombatEventType.Damage, DateTime.UtcNow));
            }
            else if (hpDiff > 0)
            {
                Buffer.AddEvent(new CombatEvent(
                    objectId, 0, 0, hpDiff, CombatEventType.Healing, DateTime.UtcNow));
            }
        }

        LastHpValues[objectId] = currentHp;
    }
}
