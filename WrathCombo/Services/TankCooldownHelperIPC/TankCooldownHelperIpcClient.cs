using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Reflection;
using System;
using System.Text.Json;
using WrathCombo.Services.SmartMitigation;

namespace WrathCombo.Services.TankCooldownHelperIPC;

/// <summary>
/// Reads local-player danger telemetry from Tank Cooldown Helper when loaded.
/// </summary>
internal static class TankCooldownHelperIpcClient
{
    public const string GetLocalDangerSnapshotIpc = DangerIpcNames.GetLocalDangerSnapshot;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static ICallGateSubscriber<string>? _subscriber;

    /// <summary>Last failure reason for smart-mit trace (e.g. tch_ipc_miss).</summary>
    internal static string? LastPressureSourceFailure { get; private set; }

    internal static void Init()
    {
        _subscriber = Svc.PluginInterface.GetIpcSubscriber<string>(GetLocalDangerSnapshotIpc);
    }

    internal static void Dispose()
    {
        _subscriber = null;
        LastPressureSourceFailure = null;
    }

    internal static bool IsPluginLoaded =>
        DalamudReflector.TryGetDalamudPlugin("TankCooldownHelper", out _, false, true);

    internal static bool TryGetPlayerPressure(uint objectId, out PlayerPressureState pressure)
    {
        pressure = default;
        LastPressureSourceFailure = null;

        if (!IsPluginLoaded)
            return false;

        if (_subscriber is null)
        {
            LastPressureSourceFailure = "tch_ipc_no_subscriber";
            return false;
        }

        if (Player.Object is null || (uint)Player.Object.GameObjectId != objectId)
        {
            LastPressureSourceFailure = "tch_ipc_not_local_player";
            return false;
        }

        try
        {
            var json = _subscriber.InvokeFunc();
            if (string.IsNullOrWhiteSpace(json))
            {
                LastPressureSourceFailure = "tch_ipc_empty";
                return false;
            }

            var payload = JsonSerializer.Deserialize<DangerIpcPayload>(json, JsonOptions);
            if (payload is null)
            {
                LastPressureSourceFailure = "tch_ipc_parse_null";
                return false;
            }

            if (!payload.Ok)
            {
                LastPressureSourceFailure = "tch_ipc_not_ok";
                return false;
            }

            var netDps = Math.Max(0f, payload.IncomingDps - payload.IncomingHps);

            pressure = new PlayerPressureState(
                payload.IncomingDps,
                payload.IncomingHps,
                netDps,
                payload.DangerRatio,
                // TCH's payload carries no max-single-hit field. Left 0 here and
                // backfilled from local HP-delta telemetry by
                // TankSmartMitigationThreat.GetPlayerPressure -- consumers that gate on
                // MaxSingleHit must never see a hardcoded 0 just because TCH is the source.
                MaxSingleHit: 0f,
                payload.SecondsUntilDeath,
                payload.DangerLevel);

            return true;
        }
        catch (IpcNotReadyError)
        {
            LastPressureSourceFailure = "tch_ipc_not_ready";
            return false;
        }
        catch (IpcError)
        {
            LastPressureSourceFailure = "tch_ipc_error";
            return false;
        }
        catch (JsonException)
        {
            LastPressureSourceFailure = "tch_ipc_json";
            return false;
        }
    }

    private sealed class DangerIpcPayload
    {
        public bool Ok { get; set; }
        public float IncomingDps { get; set; }
        public float IncomingHps { get; set; }
        public float DangerRatio { get; set; }
        public int DangerLevel { get; set; }
        public bool InDanger { get; set; }
        public float? SecondsUntilDeath { get; set; }
    }
}

/// <summary>IPC endpoint names shared with Tank Cooldown Helper (string literals only).</summary>
internal static class DangerIpcNames
{
    public const string GetLocalDangerSnapshot = "TankCooldownHelper.GetLocalDangerSnapshot";
}
