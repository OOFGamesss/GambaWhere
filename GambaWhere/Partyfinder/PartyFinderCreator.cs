using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace GambaWhere.Partyfinder;

/// <summary>
/// Opens the Party Finder creation window, pre-fills the recruitment criteria off-screen,
/// then reveals the form for the host to register manually.
/// </summary>
public sealed unsafe class PartyFinderCreator : IDisposable
{
    private const string ListAddonName = "LookingForGroup";
    private const string ConditionAddonName = "LookingForGroupCondition";

    private static readonly TimeSpan MaxDuration = TimeSpan.FromSeconds(10);

    private const int SettleFrames = 5;

    private enum Stage
    {
        Idle,
        WaitingForListClose,
        WaitingForListWindow,
        WaitingForConditionWindow,
        ApplyingSettings,
    }

    private enum ApplyPhase
    {
        SetType,
        SettleAfterType,
        RemoveRoles,
        Finish,
    }

    private readonly IAddonLifecycle _addonLifecycle;
    private readonly IFramework _framework;
    private readonly IGameGui _gameGui;
    private readonly IPluginLog _log;
    private readonly LookingForGroupInterop _interop;
    private readonly ICondition _condition;
    private readonly IPartyList _partyList;
    private readonly IObjectTable _objectTable;

    private Stage _stage = Stage.Idle;
    private bool _alliance;
    private string _pendingComment = string.Empty;
    private short _originX;
    private short _originY;
    private bool _originCaptured;
    private DateTime _deadlineUtc;
    private bool _postSetupRegistered;
    private bool _preDrawRegistered;

    private ApplyPhase _applyPhase;
    private int _settleRemaining;

    private string _statusMessage = string.Empty;
    private DateTime _statusUntil;

    public PartyFinderCreator(
        IAddonLifecycle addonLifecycle,
        IFramework framework,
        IGameGui gameGui,
        IPluginLog log,
        LookingForGroupInterop interop,
        ICondition condition,
        IPartyList partyList,
        IObjectTable objectTable)
    {
        _addonLifecycle = addonLifecycle;
        _framework = framework;
        _gameGui = gameGui;
        _log = log;
        _interop = interop;
        _condition = condition;
        _partyList = partyList;
        _objectTable = objectTable;

        _framework.Update += OnFrameworkUpdate;
    }

    public bool IsRunning => _stage != Stage.Idle;

    public string StatusMessage => DateTime.UtcNow < _statusUntil ? _statusMessage : string.Empty;

    public bool CanCreate(out string reason)
    {
        if (_condition[ConditionFlag.UsingPartyFinder])
        {
            reason = "You already have an active Party Finder listing.";
            return false;
        }

        if (_condition[ConditionFlag.BoundByDuty]
            || _condition[ConditionFlag.BoundByDuty56]
            || _condition[ConditionFlag.BoundByDuty95]
            || _condition[ConditionFlag.InDeepDungeon])
        {
            reason = "You cannot create a Party Finder listing while in a duty.";
            return false;
        }

        if (InfoProxyCrossRealm.IsCrossRealmParty() && !InfoProxyCrossRealm.IsLocalPlayerPartyLeader())
        {
            reason = "Only the party leader can create a Party Finder listing.";
            return false;
        }

        if (_partyList.Length > 1 && !LocalPlayerIsPartyLeader())
        {
            reason = "Only the party leader can create a Party Finder listing.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public void CreateParty(string? gameType, string? venueName, string? location)
        => Begin(alliance: false, gameType, venueName, location);

    public void CreateAlliance(string? gameType, string? venueName, string? location)
        => Begin(alliance: true, gameType, venueName, location);

    public void Dispose()
    {
        _framework.Update -= OnFrameworkUpdate;
        Cleanup();
    }

    private bool LocalPlayerIsPartyLeader()
    {
        var leaderIndex = _partyList.PartyLeaderIndex;
        if (leaderIndex >= _partyList.Length)
            return false;

        var local = _objectTable.LocalPlayer;
        var leader = _partyList[(int)leaderIndex];
        return local != null
            && leader?.GameObject != null
            && leader.GameObject.EntityId == local.EntityId;
    }

    private void Begin(bool alliance, string? gameType, string? venueName, string? location)
    {
        if (IsRunning)
        {
            SetStatus("Party Finder setup already in progress.");
            return;
        }

        if (!CanCreate(out var reason))
        {
            SetStatus(reason);
            return;
        }

        if (!_interop.IsAgentAvailable)
        {
            SetStatus("Could not access the Party Finder. Try again in a moment.");
            return;
        }

        _alliance = alliance;
        _originCaptured = false;
        _pendingComment = PartyFinderComment.Compose(gameType, venueName, location);

        RegisterPostSetup();

        if (_interop.IsWindowOpen)
        {
            _interop.CloseWindow();
            SetStage(Stage.WaitingForListClose);
            return;
        }

        if (!_interop.OpenWindow())
        {
            Cleanup();
            SetStatus("Could not open the Party Finder window.");
            return;
        }

        SetStage(Stage.WaitingForListWindow);
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (_stage == Stage.Idle)
            return;

        if (DateTime.UtcNow > _deadlineUtc)
        {
            Abort("Timed out preparing the Party Finder.");
            return;
        }

        switch (_stage)
        {
            case Stage.WaitingForListClose:
                StepReopenList();
                break;
            case Stage.WaitingForListWindow:
                StepPrepareAndRecruit();
                break;
            case Stage.WaitingForConditionWindow:
                StepWaitForConditionWindow();
                break;
            case Stage.ApplyingSettings:
                StepApplySettings();
                break;
        }
    }

    private void OnConditionPostSetup(AddonEvent type, AddonArgs args)
    {
        RegisterPreDraw();
        EnterApplying();
    }

    private void OnConditionPreDraw(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon == null)
            return;

        CaptureOrigin(addon);
        LookingForGroupInterop.MoveOffScreen(addon);
    }

    private void StepReopenList()
    {
        if (_interop.IsWindowOpen)
            return;

        if (!_interop.OpenWindow())
        {
            Cleanup();
            SetStatus("Could not open the Party Finder window.");
            return;
        }

        SetStage(Stage.WaitingForListWindow);
    }

    private void StepPrepareAndRecruit()
    {
        var list = GetReadyAddon(ListAddonName);
        if (list == null)
            return;

        var master = new AddonMaster.LookingForGroup(list);
        if (master.RecruitMembersButton == null)
            return;

        _interop.PrepareRecruitment(_pendingComment);

        if (!master.RecruitMembersOrDetails())
            return;

        SetStage(Stage.WaitingForConditionWindow);
    }

    private void StepWaitForConditionWindow()
    {
        var addon = GetReadyAddon(ConditionAddonName);
        if (addon == null)
            return;

        CaptureOrigin(addon);
        RegisterPreDraw();
        EnterApplying();
    }

    private void EnterApplying()
    {
        _applyPhase = ApplyPhase.SetType;
        _settleRemaining = 0;
        SetStage(Stage.ApplyingSettings);
    }

    private void StepApplySettings()
    {
        var addon = GetReadyAddon(ConditionAddonName);
        if (addon == null)
            return;

        var condition = (AddonLookingForGroupCondition*)addon;
        if (!LookingForGroupInterop.AreInputsReady(condition))
            return;

        try
        {
            switch (_applyPhase)
            {
                case ApplyPhase.SetType:
                    ApplySetType(addon);
                    break;
                case ApplyPhase.SettleAfterType:
                    if (--_settleRemaining <= 0)
                        _applyPhase = ApplyPhase.RemoveRoles;
                    break;
                case ApplyPhase.RemoveRoles:
                    ApplyRemoveRoles(condition);
                    break;
                case ApplyPhase.Finish:
                    ApplyFinish(addon);
                    break;
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[PF] Failed while configuring the recruitment window.");
            Reveal(addon);
            Cleanup();
            SetStatus("Could not configure the Party Finder. Reverted safely.");
        }
    }

    private void ApplySetType(AtkUnitBase* addon)
    {
        var master = new AddonMaster.LookingForGroupCondition(addon);
        if (_alliance)
            master.Alliance();
        else
            master.Normal();

        _settleRemaining = SettleFrames;
        _applyPhase = ApplyPhase.SettleAfterType;
    }

    private void ApplyRemoveRoles(AddonLookingForGroupCondition* condition)
    {
        if (condition->RemoveRoleRestrictionsCheckBox->IsChecked)
        {
            _applyPhase = ApplyPhase.Finish;
            return;
        }

        LookingForGroupInterop.TickRemoveRoleRestrictions(condition);
    }

    private void ApplyFinish(AtkUnitBase* addon)
    {
        Reveal(addon);
        Cleanup();
        SetStatus(_alliance
            ? "Alliance listing ready. Select your duty, then click Recruit Members."
            : "Party listing ready. Review, then click Recruit Members.");
    }

    private void CaptureOrigin(AtkUnitBase* addon)
    {
        if (_originCaptured || addon == null || (addon->X <= 0 && addon->Y <= 0))
            return;

        _originX = addon->X;
        _originY = addon->Y;
        _originCaptured = true;
    }

    private void SetStage(Stage stage)
    {
        _stage = stage;
        _deadlineUtc = DateTime.UtcNow + MaxDuration;
    }

    private AtkUnitBase* GetReadyAddon(string name)
    {
        var ptr = _gameGui.GetAddonByName(name, 1);
        var addon = (AtkUnitBase*)ptr.Address;
        if (addon == null)
            return null;

        return GenericHelpers.IsAddonReady(addon) ? addon : null;
    }

    private void Reveal(AtkUnitBase* addon)
    {
        UnregisterPreDraw();
        LookingForGroupInterop.MoveTo(addon, _originX, _originY);
    }

    private void RegisterPostSetup()
    {
        if (_postSetupRegistered)
            return;

        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, ConditionAddonName, OnConditionPostSetup);
        _postSetupRegistered = true;
    }

    private void RegisterPreDraw()
    {
        if (_preDrawRegistered)
            return;

        _addonLifecycle.RegisterListener(AddonEvent.PreDraw, ConditionAddonName, OnConditionPreDraw);
        _preDrawRegistered = true;
    }

    private void UnregisterPreDraw()
    {
        if (!_preDrawRegistered)
            return;

        _addonLifecycle.UnregisterListener(AddonEvent.PreDraw, ConditionAddonName, OnConditionPreDraw);
        _preDrawRegistered = false;
    }

    private void Abort(string message)
    {
        _log.Warning("[PF] Aborting at stage {Stage}: {Message}", _stage, message);

        var addon = GetReadyAddon(ConditionAddonName);
        if (addon != null)
            Reveal(addon);

        Cleanup();
        SetStatus(message);
    }

    private void Cleanup()
    {
        if (_postSetupRegistered)
        {
            _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, ConditionAddonName, OnConditionPostSetup);
            _postSetupRegistered = false;
        }

        UnregisterPreDraw();
        _stage = Stage.Idle;
    }

    private void SetStatus(string message)
    {
        _statusMessage = message;
        _statusUntil = DateTime.UtcNow.AddSeconds(6);
    }
}
