using Dalamud.Plugin.Services;
using ECommons.Automation.UIInput;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace GambaWhere.Partyfinder;

/// <summary>
/// Interop surface over the native LookingForGroup agent and its creation addon.
/// </summary>
public sealed unsafe class LookingForGroupInterop
{
    private const ushort PasswordDisabled = 10000;

    private readonly IPluginLog _log;

    public LookingForGroupInterop(IPluginLog log) => _log = log;

    private static AgentLookingForGroup* Agent => AgentLookingForGroup.Instance();

    public bool IsAgentAvailable => Agent != null;

    public bool IsWindowOpen
    {
        get
        {
            var agent = Agent;
            return agent != null && ((AgentInterface*)agent)->IsAgentActive();
        }
    }

    public void CloseWindow()
    {
        var agent = Agent;
        if (agent == null)
            return;

        var agentInterface = (AgentInterface*)agent;
        if (agentInterface->IsAgentActive())
            agentInterface->Hide();
    }

    public bool OpenWindow()
    {
        var agent = Agent;
        if (agent == null)
        {
            _log.Warning("[PF] LookingForGroup agent was null; cannot open window.");
            return false;
        }

        var agentInterface = (AgentInterface*)agent;
        if (!agentInterface->IsAgentActive())
            agentInterface->Show();

        return true;
    }

    public void BrowseListings(byte areaTab, byte category)
    {
        var agent = Agent;
        if (agent == null)
            return;

        agent->SearchAreaTab = areaTab;
        agent->RequestCategoryListings(category);
    }

    public bool OpenListing(ulong listingId)
    {
        var agent = Agent;
        return agent != null && agent->OpenListing(listingId);
    }

    public void PrepareRecruitment(string comment)
    {
        var agent = Agent;
        if (agent == null)
            return;

        agent->StoredRecruitmentInfo.CommentString = comment;
        agent->StoredRecruitmentInfo.Password = PasswordDisabled;
        agent->StoredRecruitmentInfo.OnePlayerPerJob = 0;
    }

    public static void MoveOffScreen(AtkUnitBase* addon)
    {
        if (addon == null)
            return;

        addon->SetPosition(-10000, -10000);
    }

    public static void MoveTo(AtkUnitBase* addon, short x, short y)
    {
        if (addon == null)
            return;

        addon->SetPosition(x, y);
    }

    public static bool AreInputsReady(AddonLookingForGroupCondition* addon)
        => addon != null && addon->RemoveRoleRestrictionsCheckBox != null;

    public static void TickRemoveRoleRestrictions(AddonLookingForGroupCondition* addon)
    {
        if (addon != null)
            EnsureChecked((AtkUnitBase*)addon, addon->RemoveRoleRestrictionsCheckBox);
    }

    private static void EnsureChecked(AtkUnitBase* owner, AtkComponentCheckBox* checkbox)
    {
        if (owner == null || checkbox == null || checkbox->IsChecked)
            return;

        checkbox->SetChecked(true);
        checkbox->ClickCheckBox(owner);
    }
}
