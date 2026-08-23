using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using ECommons.Logging;
using System;

namespace WrathCombo.Services.IPC_Subscriber;

internal static class ActionStacksEXIPC
{
    private const string PrepareActionName = "ActionStacksEX.PrepareAction";

    private static ICallGateSubscriber<uint, ulong, (bool Matched, uint ActionID, ulong TargetObjectID, string StackName)>? prepareActionSubscriber;

    public static void Init()
    {
        prepareActionSubscriber ??= Svc.PluginInterface.GetIpcSubscriber<uint, ulong, (bool, uint, ulong, string)>(PrepareActionName);
    }

    public static void Dispose()
    {
        prepareActionSubscriber = null;
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
}
