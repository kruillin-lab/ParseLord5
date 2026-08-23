using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using ECommons.Logging;
using System;

namespace WrathCombo.Services.IPC_Subscriber;

internal static class ActionStacksEXIPC
{
    private const string PrepareActionName = "ActionStacksEX.PrepareAction";
    private const string PeekActionName = "ActionStacksEX.PeekAction";

    private static ICallGateSubscriber<uint, ulong, (bool Matched, uint ActionID, ulong TargetObjectID, string StackName)>? prepareActionSubscriber;
    private static ICallGateSubscriber<uint, ulong, (bool Matched, uint ActionID, ulong TargetObjectID, string StackName)>? peekActionSubscriber;

    public static void Init()
    {
        prepareActionSubscriber ??= Svc.PluginInterface.GetIpcSubscriber<uint, ulong, (bool, uint, ulong, string)>(PrepareActionName);
    }

    public static void Dispose()
    {
        prepareActionSubscriber = null;
        peekActionSubscriber = null;
    }

    public static bool TryPrepareAction(uint actionID, ulong targetObjectID, out uint preparedActionID, out ulong preparedTargetObjectID, out string stackName)
    {
        preparedActionID = actionID;
        preparedTargetObjectID = targetObjectID;
        stackName = string.Empty;

        try
        {
            Init();
            if (prepareActionSubscriber is null)
                return false;

            var result = prepareActionSubscriber.InvokeFunc(actionID, targetObjectID);
            if (!result.Matched)
                return false;

            preparedActionID = result.ActionID;
            preparedTargetObjectID = result.TargetObjectID;
            stackName = result.StackName;
            return true;
        }
        catch (Exception e)
        {
            PluginLog.Verbose($"[ActionStacksEXIPC] PrepareAction unavailable: {e.Message}");
            return false;
        }
    }

    /// <summary>
    ///     Peek what ActionStacksEX would resolve this action to WITHOUT consuming
    ///     the stack lock. Returns false if AS is not installed, the action is not
    ///     matched by any stack, or the peek endpoint is unavailable (older AS).
    /// </summary>
    public static bool TryPeekAction(uint actionID, ulong targetObjectID, out uint resolvedActionID, out ulong resolvedTargetObjectID, out string stackName)
    {
        resolvedActionID = actionID;
        resolvedTargetObjectID = targetObjectID;
        stackName = string.Empty;

        try
        {
            peekActionSubscriber ??= Svc.PluginInterface.GetIpcSubscriber<uint, ulong, (bool, uint, ulong, string)>(PeekActionName);
            if (peekActionSubscriber is null)
                return false;

            var result = peekActionSubscriber.InvokeFunc(actionID, targetObjectID);
            if (!result.Matched)
                return false;

            resolvedActionID = result.ActionID;
            resolvedTargetObjectID = result.TargetObjectID;
            stackName = result.StackName;
            return true;
        }
        catch (Exception e)
        {
            PluginLog.Verbose($"[ActionStacksEXIPC] PeekAction unavailable (older ActionStacksEX?): {e.Message}");
            return false;
        }
    }
}
