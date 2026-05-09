using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
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

    /// <summary>Subtle warm panel, same visual weight as <see cref="DrawSetupGuide"/> / preview child bg.</summary>
    private static readonly Vector4 VenueServicePanelBg = new(0.13f, 0.10f, 0.076f, 1f);

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
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.45f, 0.42f, 1f));
            ImGui.TextWrapped(
                "One or more webhooks failed to deliver. The row stays highlighted in red until a send succeeds "
                    + "again. Turn Enable off then on on that row to retry (PATCH, then POST if the message no longer exists). "
                    + "Check that the webhook URL still works in Discord.");
            ImGui.PopStyleColor();

            ImGui.Spacing();
        }

        DiscordWebhookEntry[] snapshot;
        lock (_config)
        {
            GrowCommittedSnapshotsLocked(_config.DiscordWebhooks);
            snapshot = [.. _config.DiscordWebhooks];
        }

        ImGui.PushStyleColor(ImGuiCol.Text, GuideAccentColour);
        ImGui.TextUnformatted("Webhook API URLs");
        ImGui.PopStyleColor();

        ImGuiHelpers.ScaledDummy(2f);
        ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.25f, 0.32f, 0.45f, 0.9f));
        ImGui.Separator();
        ImGui.PopStyleColor();
        ImGuiHelpers.ScaledDummy(6f);

        var rowCount = snapshot.Length;
        for (var i = 0; i < snapshot.Length; i++)
            DrawWebhookRow(snapshot[i], i, rowCount);

        DrawVenueServiceInformation();
        DrawWebhookEmbedPreview();
    }

    private void DrawDiscordHeader()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var maxW = DiscordLogoMaxWidth * scale;

        Vector2 drawSize;
        var tex = _imageCache.GetBundledImage("discordlogo.webp");
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

    private void DrawSetupGuide()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.09f, 0.09f, 0.11f, 1f));

        var scale = ImGuiHelpers.GlobalScale;
        var guideH = scale * 210f;

        if (ImGui.BeginChild(
                "DiscordWebhookGuide",
                new Vector2(-1f, guideH),
                true,
                ImGuiWindowFlags.NoScrollbar))
        {
            ImGuiHelpers.ScaledDummy(6f);

            ImGui.PushStyleColor(ImGuiCol.Text, GuideAccentColour);
            ImGui.TextUnformatted("Webhook setup");
            ImGui.PopStyleColor();

            ImGuiHelpers.ScaledDummy(4f);
            ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.25f, 0.32f, 0.45f, 0.9f));
            ImGui.Separator();
            ImGui.PopStyleColor();
            ImGuiHelpers.ScaledDummy(6f);

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

            ImGuiHelpers.ScaledDummy(6f);
        }

        ImGui.EndChild();

        ImGui.PopStyleColor();
        ImGui.PopStyleVar();

        ImGuiHelpers.ScaledDummy(8f);
    }

    private static void GuideBullet(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, GuideMutedColour);
        ImGui.BulletText(text);
        ImGui.PopStyleColor();
        ImGuiHelpers.ScaledDummy(2f);
    }

    private void DrawVenueServiceInformation()
    {
        ImGuiHelpers.ScaledDummy(10f);

        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, VenueServicePanelBg);

        var scale = ImGuiHelpers.GlobalScale;
        var style = ImGui.GetStyle();

        var parentAvailX = ImGui.GetContentRegionAvail().X;
        var innerApprox =
            Math.Max(parentAvailX - 2f * style.ChildBorderSize - 2f * style.WindowPadding.X - 2f, 32f);
        var wrapForVenue = Math.Max(innerApprox - scale * 16f, 32f);

        var textBlockH = ImGui.CalcTextSize(VenueServiceMessage, false, wrapForVenue).Y;
        var childH = style.WindowPadding.Y * 2f + scale * 12f + textBlockH + scale * 4f;

        if (ImGui.BeginChild(
                "DiscordVenueServiceInfo",
                new Vector2(-1f, childH),
                true,
                ImGuiWindowFlags.NoScrollbar))
        {
            ImGuiHelpers.ScaledDummy(6f);

            var innerAvail = ImGui.GetContentRegionAvail().X;
            var drawWrap = Math.Max(innerAvail - scale * 16f, 32f);

            var textSize = ImGui.CalcTextSize(VenueServiceMessage, false, drawWrap);

            var x0 = ImGui.GetCursorPosX();
            var centreGap = Math.Max(0f, (innerAvail - textSize.X) * 0.5f);
            ImGui.SetCursorPosX(x0 + centreGap);

            ImGui.PushStyleColor(ImGuiCol.Text, VenueServiceTextColour);
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + drawWrap);
            ImGui.TextUnformatted(VenueServiceMessage);
            ImGui.PopTextWrapPos();
            ImGui.PopStyleColor();

            ImGuiHelpers.ScaledDummy(6f);
        }

        ImGui.EndChild();

        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
    }

    private void DrawWebhookEmbedPreview()
    {
        ImGuiHelpers.ScaledDummy(8f);

        ImGui.PushStyleColor(ImGuiCol.Text, GuideAccentColour);
        ImGui.TextUnformatted("Discord preview example");
        ImGui.PopStyleColor();

        ImGuiHelpers.ScaledDummy(2f);
        ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.25f, 0.32f, 0.45f, 0.9f));
        ImGui.Separator();
        ImGui.PopStyleColor();
        ImGuiHelpers.ScaledDummy(6f);

        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.09f, 0.09f, 0.11f, 1f));

        var scale = ImGuiHelpers.GlobalScale;
        var previewH = scale * 480f;

        if (ImGui.BeginChild(
                "DiscordWebhookEmbedPreview",
                new Vector2(-1f, previewH),
                true,
                ImGuiWindowFlags.None))
        {
            ImGuiHelpers.ScaledDummy(6f);

            var tex = _imageCache.GetBundledImage("discordwebhookexample.png");
            var innerW = ImGui.GetContentRegionAvail().X;

            if (tex != null && tex.Width > 0 && tex.Height > 0 && innerW > 1f)
            {
                var w = innerW - scale * 8f;
                var h = w * tex.Height / (float)tex.Width;
                var maxH = scale * 520f;
                if (h > maxH && maxH > 1f)
                {
                    var s = maxH / h;
                    h = maxH;
                    w *= s;
                }

                CentreForWidth(w);
                ImGui.Image(tex.Handle, new Vector2(w, h));
                ImGuiHelpers.ScaledDummy(8f);
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Text, GuideMutedColour);
                ImGui.TextWrapped(tex == null
                    ? "Preview image is still loading; switch away and back if it does not appear."
                    : "Preview image has no usable size.");
                ImGui.PopStyleColor();
                ImGuiHelpers.ScaledDummy(8f);
            }
        }

        ImGui.EndChild();

        ImGui.PopStyleColor();
        ImGui.PopStyleVar();

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
        ImGui.PushID(index);

        var enabledFlag = entry.Enabled;
        ImGui.BeginGroup();

        if (entry.PostFailed)
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.32f, 0.34f, 1f));

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

        if (entry.PostFailed)
            ImGui.PopStyleColor();

        ImGui.SameLine();

        var btn = 26f * ImGuiHelpers.GlobalScale;
        var iconsW = btn * 2f + ImGui.GetStyle().ItemSpacing.X;

        var controlWidth =
            Math.Max(ImGui.GetContentRegionAvail().X - iconsW, 120f);

        ImGui.SetNextItemWidth(controlWidth);

        if (entry.PostFailed)
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.48f, 0.06f, 0.06f, 1f));

        var mutableUrl = _urlDrafts[index];
        ImGui.InputText("##DiscordUrl", ref mutableUrl, UrlBufferLength);

        _urlDrafts[index] = mutableUrl;
        EvaluateUrlCommitted(entry, index);

        if (entry.PostFailed)
            ImGui.PopStyleColor();

        ImGui.SameLine(0f, ImGui.GetStyle().ItemInnerSpacing.X);

        if (ImGuiComponents.IconButton("##DiscordAddRow", FontAwesomeIcon.Plus))
            InsertBlankBelow(index);

        ImGui.SameLine(0f, ImGui.GetStyle().ItemInnerSpacing.X);

        var canRemove = rowCount > 1;
        if (!canRemove)
            ImGui.BeginDisabled();

        if (ImGuiComponents.IconButton("##DiscordRemoveRow", FontAwesomeIcon.TrashAlt))
            _pendingRowRemovalIndex = index;

        if (!canRemove)
        {
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("At least one webhook URL row must remain.");
        }
        else if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Remove this webhook row.");
        }

        ImGui.EndGroup();

        ImGui.PopID();

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
