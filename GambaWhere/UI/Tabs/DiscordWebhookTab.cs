using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using GambaWhere.Config;
using GambaWhere.Discord;
using GambaWhere.Images;

namespace GambaWhere.UI.Tabs;

public sealed class DiscordWebhookTab
{
    private const int UrlBufferLength = 1536;

    private static readonly Vector4 GuideAccentColour = new(0.35f, 0.62f, 0.92f, 1f);
    private static readonly Vector4 GuideMutedColour = new(0.78f, 0.82f, 0.92f, 1f);

    private static readonly Vector4 VenueServiceTextColour = new(0.86f, 0.82f, 0.76f, 1f);

    private const string VenueServiceMessage =
        "OOF Games offers this as a venue service where it can be branded as your own gamba tracker "
            + "with filters on only allowing certain gamba hosts names and venue names.\n"
            + "Contact me on Discord for more information.";

    private const float DiscordLogoMaxWidth = 196f;

    private readonly Configuration _config;
    private readonly DiscordWebhookService _discordWebhook;
    private readonly ImageCache _imageCache;
    private readonly IPluginLog _log;

    private readonly List<string> _lastCommittedUrls = new();
    private readonly List<string> _urlDrafts = new();

    // Row delete runs at start of next Draw so drafts stay aligned mid-frame.
    private int? _pendingRowRemovalIndex;

    public DiscordWebhookTab(
        Configuration config,
        DiscordWebhookService discordWebhook,
        ImageCache imageCache,
        IPluginLog log)
    {
        _config = config;
        _discordWebhook = discordWebhook;
        _imageCache = imageCache;
        _log = log;
    }

    public void Draw()
    {
        EnsureDiscordRowsPresent();
        FlushPendingRowRemoval();

        DrawDiscordHeader();
        DrawSetupGuide();

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);

        if (DiscordWebhookService.TabShouldWarn(_config))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 0.45f, 0.42f, 1f)))
            {
                ImGui.TextWrapped(
                    "One or more webhooks failed to deliver. The row stays highlighted in red until a send succeeds "
                        + "again. Turn Enable off then on on that row to retry (PATCH, then POST if the message no longer exists). "
                        + "Check that the webhook URL still works in Discord.");
            }

            ImGui.Spacing();
        }

        DiscordWebhookEntry[] snapshot;
        lock (_config)
        {
            GrowCommittedSnapshotsLocked(_config.DiscordWebhooks);
            snapshot = [.. _config.DiscordWebhooks];
        }

        DrawSectionHeader("Webhook API URLs");

        var rowCount = snapshot.Length;
        for (var i = 0; i < snapshot.Length; i++)
            DrawWebhookRow(snapshot[i], i, rowCount);

        DrawVenueServiceInformation();
        DrawWebhookEmbedPreview();
    }

    private void DrawDiscordHeader()
    {
        ImGuiHelpers.ScaledDummy(10f);

        var scale = ImGuiHelpers.GlobalScale;
        var maxW = DiscordLogoMaxWidth * scale;

        Vector2 drawSize;
        var tex = _imageCache.GetBundledImage("discordlogo.png");
        if (tex != null && tex.Width > 0 && tex.Height > 0)
        {
            var ratio = tex.Height / (float)tex.Width;
            drawSize = new Vector2(maxW, maxW * ratio);
        }
        else
            drawSize = new Vector2(maxW, 48f * scale);

        CentreForWidth(drawSize.X);
        if (tex != null)
            ImGui.Image(tex.Handle, drawSize);
        else
            ImGui.Dummy(drawSize);

        ImGuiHelpers.ScaledDummy(10f);
    }

    private static void DrawSectionHeader(string label)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, GuideAccentColour))
            ImGui.TextUnformatted(label);

        ImGuiHelpers.ScaledDummy(2f);
        using (ImRaii.PushColor(ImGuiCol.Separator, new Vector4(0.25f, 0.32f, 0.45f, 0.9f)))
            ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);
    }

    private void DrawSetupGuide()
    {
        DrawSectionHeader("Webhook setup");

        GuideBullet(
            "In Discord, open the channel that should show your host card, then open "
                + "Channel settings (the gear icon).");

        GuideBullet(
            "Go to Integrations → Webhooks. Create a new webhook or pick an existing one, then copy its URL "
                + "(it starts with https://discord.com/api/webhooks/…).");

        GuideBullet(
            "Paste the URL below. New rows start with Enable off — turn it on when the URL looks correct so "
                + "the plugin can create or refresh the Discord message.");

        GuideBullet(
            "Use the + button to add another webhook row. Use the bin to remove a row — at least one row always "
                + "stays (the bin is disabled when only one row remains).");

        GuideBullet(
            "While you host a session, the embed updates with your activity. When you stop hosting, the card "
                + "returns to the idle \"No gamba available\" state.");

        ImGuiHelpers.ScaledDummy(8f);
    }

    private static void GuideBullet(string text)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, GuideMutedColour))
        {
            ImGui.Bullet();
            ImGui.SameLine();
            ImGui.TextWrapped(text);
        }
        ImGuiHelpers.ScaledDummy(2f);
    }

    private void DrawVenueServiceInformation()
    {
        ImGuiHelpers.ScaledDummy(10f);

        var scale = ImGuiHelpers.GlobalScale;
        var avail = ImGui.GetContentRegionAvail().X;
        var drawWrap = Math.Max(avail - scale * 16f, 32f);

        var textSize = ImGui.CalcTextSize(VenueServiceMessage, false, drawWrap);
        var centreGap = Math.Max(0f, (avail - textSize.X) * 0.5f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + centreGap);

        using (ImRaii.PushColor(ImGuiCol.Text, VenueServiceTextColour))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + drawWrap);
            ImGui.TextUnformatted(VenueServiceMessage);
            ImGui.PopTextWrapPos();
        }

        ImGuiHelpers.ScaledDummy(6f);
    }

    private void DrawWebhookEmbedPreview()
    {
        ImGuiHelpers.ScaledDummy(8f);

        DrawSectionHeader("Discord preview example");

        var scale = ImGuiHelpers.GlobalScale;
        var tex = _imageCache.GetBundledImage("discordwebhookexample.png");
        var innerW = ImGui.GetContentRegionAvail().X;

        if (tex != null && tex.Width > 0 && tex.Height > 0 && innerW > 1f)
        {
            var maxW = scale * 480f;
            var w = Math.Min(innerW - scale * 8f, maxW);
            var h = w * tex.Height / tex.Width;

            CentreForWidth(w);
            ImGui.Image(tex.Handle, new Vector2(w, h));
            ImGuiHelpers.ScaledDummy(8f);
        }
        else
        {
            using (ImRaii.PushColor(ImGuiCol.Text, GuideMutedColour))
            {
                ImGui.TextWrapped(tex == null
                    ? "Preview image is still loading; switch away and back if it does not appear."
                    : "Preview image has no usable size.");
            }
            ImGuiHelpers.ScaledDummy(8f);
        }

        ImGuiHelpers.ScaledDummy(6f);
    }

    private void FlushPendingRowRemoval()
    {
        if (_pendingRowRemovalIndex is not { } index)
            return;

        _pendingRowRemovalIndex = null;
        RemoveRowAt(index);
    }

    private void EnsureDiscordRowsPresent()
    {
        lock (_config)
        {
            if (_config.DiscordWebhooks.Count == 0)
            {
                _config.DiscordWebhooks.Add(new DiscordWebhookEntry());

                _config.Save();
            }

            GrowCommittedSnapshotsLocked(_config.DiscordWebhooks);
            ShrinkSnapshotsLocked(_config.DiscordWebhooks.Count);
        }
    }

    private void GrowCommittedSnapshotsLocked(IReadOnlyList<DiscordWebhookEntry> entries)
    {
        while (_lastCommittedUrls.Count < entries.Count)
        {
            var i = _lastCommittedUrls.Count;
            var seeded = entries[i].Url.Trim();
            _lastCommittedUrls.Add(seeded);

            var draftSeed = entries[i].Url.Length == 0 ? string.Empty : entries[i].Url;
            _urlDrafts.Add(draftSeed);
        }
    }

    private void ShrinkSnapshotsLocked(int targetCount)
    {
        while (_lastCommittedUrls.Count > targetCount)
        {
            _lastCommittedUrls.RemoveAt(_lastCommittedUrls.Count - 1);
            _urlDrafts.RemoveAt(_urlDrafts.Count - 1);
        }
    }

    private void DrawWebhookRow(DiscordWebhookEntry entry, int index, int rowCount)
    {
        using var id = ImRaii.PushId(index);
        using var group = ImRaii.Group();

        var enabledFlag = entry.Enabled;

        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 0.32f, 0.34f, 1f), entry.PostFailed))
        {
            if (ImGui.Checkbox("##DiscordEnabled", ref enabledFlag))
                ToggleEnabled(entry, enabledFlag, index);

            if (ImGui.IsItemHovered())
            {
                var tip = entry.PostFailed
                    ? "Delivery failed recently: turn Enable off and on again to retry the webhook (PATCH, or POST "
                        + "if the Discord message was removed). Tick Enable whenever you want this URL to receive idle "
                        + "and session embeds."
                    : "New rows start disabled: paste the webhook URL, then enable when ready. "
                        + "Enable must stay on for this URL to receive idle and session embeds.";
                ImGui.SetTooltip(tip);
            }
        }

        ImGui.SameLine();

        var btn = 26f * ImGuiHelpers.GlobalScale;
        var iconsW = btn * 2f + ImGui.GetStyle().ItemSpacing.X;

        var controlWidth =
            Math.Max(ImGui.GetContentRegionAvail().X - iconsW, 120f);

        ImGui.SetNextItemWidth(controlWidth);

        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0.48f, 0.06f, 0.06f, 1f), entry.PostFailed))
        {
            var mutableUrl = _urlDrafts[index];
            ImGui.InputText("##DiscordUrl", ref mutableUrl, UrlBufferLength);

            _urlDrafts[index] = mutableUrl;
            EvaluateUrlCommitted(entry, index);
        }

        ImGui.SameLine(0f, ImGui.GetStyle().ItemInnerSpacing.X);

        if (ImGuiComponents.IconButton("##DiscordAddRow", FontAwesomeIcon.Plus))
            InsertBlankBelow(index);

        ImGui.SameLine(0f, ImGui.GetStyle().ItemInnerSpacing.X);

        var canRemove = rowCount > 1;
        using (ImRaii.Disabled(!canRemove))
        {
            if (ImGuiComponents.IconButton("##DiscordRemoveRow", FontAwesomeIcon.TrashAlt))
                _pendingRowRemovalIndex = index;
        }

        if (!canRemove)
        {
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("At least one webhook URL row must remain.");
        }
        else if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Remove this webhook row.");
        }

        ImGui.Spacing();
    }

    private void ToggleEnabled(DiscordWebhookEntry entry, bool desired, int index)
    {
        CommitUrlDraftToEntry(entry, index);
        entry.Enabled = desired;
        _config.Save();

        KickApply(entry);
    }

    private void CommitUrlDraftToEntry(DiscordWebhookEntry entry, int index)
    {
        if (index < 0 || index >= _urlDrafts.Count || index >= _lastCommittedUrls.Count)
            return;

        var draft = _urlDrafts[index].Trim();
        _urlDrafts[index] = draft;

        var prior = _lastCommittedUrls[index].Trim();
        var changedUrl = prior != draft;

        if (changedUrl)
        {
            entry.MessageId = null;
            entry.PostFailed = false;
        }

        entry.Url = draft;
        _lastCommittedUrls[index] = draft;
    }

    private void EvaluateUrlCommitted(DiscordWebhookEntry entry, int index)
    {
        if (!ImGui.IsItemDeactivatedAfterEdit())
            return;

        CommitUrlDraftToEntry(entry, index);
        _config.Save();

        KickApply(entry);
    }

    private void InsertBlankBelow(int anchorIndex)
    {
        lock (_config)
        {
            var insertion = Math.Min(anchorIndex + 1, _config.DiscordWebhooks.Count);
            _config.DiscordWebhooks.Insert(insertion, new DiscordWebhookEntry());
            _lastCommittedUrls.Insert(insertion, string.Empty);
            _urlDrafts.Insert(insertion, string.Empty);

            _config.Save();
        }
    }

    private void RemoveRowAt(int index)
    {
        lock (_config)
        {
            if (_config.DiscordWebhooks.Count <= 1)
                return;

            if (index < 0 || index >= _config.DiscordWebhooks.Count)
                return;

            _config.DiscordWebhooks.RemoveAt(index);
            if (index < _lastCommittedUrls.Count)
                _lastCommittedUrls.RemoveAt(index);
            if (index < _urlDrafts.Count)
                _urlDrafts.RemoveAt(index);

            _config.Save();
        }
    }

    private void KickApply(DiscordWebhookEntry entry)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _discordWebhook.ApplyEntryCommittedAsync(entry);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Discord webhook delivery failed unexpectedly.");
            }
        });
    }

    private static void CentreForWidth(float width)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (avail - width) * 0.5f));
    }
}
