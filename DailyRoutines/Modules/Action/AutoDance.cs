using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Hooking;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using TaskManager = ECommons.Automation.TaskManager;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoDanceTitle", "AutoDanceDescription", ModuleCategories.Action)]
public unsafe class AutoDance : DailyModuleBase
{
    private delegate bool UseActionSelfDelegate(
        ActionManager* actionManager, uint actionType, uint actionID, ulong targetID = 0xE000_0000, uint a4 = 0,
        uint a5 = 0, uint a6 = 0, void* a7 = null);

    private Hook<UseActionSelfDelegate>? useActionSelfHook;

    public override void Init()
    {
        useActionSelfHook =
            Service.Hook.HookFromAddress<UseActionSelfDelegate>((nint)ActionManager.MemberFunctionPointers.UseAction,
                                                                UseActionSelf);
        useActionSelfHook?.Enable();

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
    }

    private bool UseActionSelf(
        ActionManager* actionManager, uint actionType, uint actionID, ulong targetID, uint a4, uint a5, uint a6, void* a7)
    {
        var gauge = Service.JobGauges.Get<DNCGauge>();
        if ((ActionType)actionType is ActionType.Action && actionID is 15997 or 15998 &&
            ActionManager.Instance()->GetActionStatus(ActionType.Action, actionID) == 0 && !gauge.IsDancing)
        {
            TaskManager.Enqueue(() => gauge.IsDancing);
            TaskManager.Enqueue(actionID == 15997 ? DanceStandardStep : DanceTechnicalStep);
        }

        return useActionSelfHook.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);
    }

    private bool? DanceStandardStep() => DanceStep(false);

    private bool? DanceTechnicalStep() => DanceStep(true);

    private bool? DanceStep(bool isTechnicalStep)
    {
        if (!EzThrottler.Throttle("AutoDance", 200)) return false;
        var gauge = Service.JobGauges.Get<DNCGauge>();
        if (!gauge.IsDancing)
        {
            TaskManager.Abort();
            return true;
        }

        var nextStep = gauge.NextStep;
        if (gauge.CompletedSteps < (isTechnicalStep ? 4 : 2))
        {
            if (ActionManager.Instance()->UseAction(ActionType.Action, nextStep))
            {
                TaskManager.Enqueue(() => DanceStep(isTechnicalStep));
                return true;
            }
        }

        return false;
    }
}