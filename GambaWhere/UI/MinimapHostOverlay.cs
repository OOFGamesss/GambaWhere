using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GambaWhere.Config;
using GambaWhere.Services;
using GambaWhere.UI.Components;
using GambaWhere.Utility;
using Lumina.Excel.Sheets;

namespace GambaWhere.UI;

/// <summary>
/// Overlay that draws a game-coloured dice icon on the minimap for each nearby host, with a hover tooltip.
/// </summary>
public sealed class MinimapHostOverlay : Window, IDisposable
{
    private const string NaviMapAddon = "_NaviMap";
    private const float DiceBaseSize = 20f;
    private const float HoverPadding = 3f;

    private readonly HostMarkerService _markerService;
    private readonly IGameGui _gameGui;
    private readonly IClientState _clientState;
    private readonly PlayerInfoService _playerInfo;
    private readonly IDataManager _dataManager;
    private readonly Configuration _config;

    public MinimapHostOverlay(
        HostMarkerService markerService,
        IGameGui gameGui,
        IClientState clientState,
        PlayerInfoService playerInfo,
        IDataManager dataManager,
        Configuration config)
        : base("##GambaWhereMinimapHosts",
               ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
               ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse |
               ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing |
               ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoInputs |
               ImGuiWindowFlags.NoDocking)
    {
        _markerService = markerService;
        _gameGui = gameGui;
        _clientState = clientState;
        _playerInfo = playerInfo;
        _dataManager = dataManager;
        _config = config;

        RespectCloseHotkey = false;
        DisableWindowSounds = true;
    }

    public override void PreDraw()
    {
        var viewport = ImGuiHelpers.MainViewport;
        Position = viewport.Pos;
        Size = viewport.Size;
        PositionCondition = ImGuiCond.Always;
        SizeCondition = ImGuiCond.Always;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
    }

    public override void PostDraw() => ImGui.PopStyleVar();

    public override void Draw()
    {
        var markers = _markerService.Markers;
        if (markers.Count == 0)
            return;

        var playerPosition = _playerInfo.GetWorldPosition();
        if (playerPosition == null)
            return;

        var addon = _gameGui.GetAddonByName(NaviMapAddon, 1);
        if (addon == nint.Zero || !MinimapProjector.TryRead(addon, out var reading) || reading.NaviScale <= 0f)
            return;

        var viewportPos = ImGui.GetWindowViewport().Pos;
        var playerCentre = MinimapProjector.PlayerCentre(reading, viewportPos);
        var radius = MinimapProjector.RadiusFor(reading);
        var combinedScale = ResolveZoneScale() * reading.NaviScale * reading.Zoom;
        var playerWorld = new Vector2(playerPosition.Value.X, playerPosition.Value.Z);

        var drawList = ImGui.GetWindowDrawList();
        var iconFont = UiBuilder.IconFont;
        var glyph = FontAwesomeIcon.Dice.ToIconString();
        var diceSize = DiceBaseSize * ImGuiHelpers.GlobalScale;
        var glyphExtent = new Vector2(diceSize, diceSize);

        var mousePos = ImGui.GetMousePos();
        var hovered = false;
        HostMarker hoveredMarker = default;

        foreach (var marker in markers)
        {
            var centre = MinimapProjector.Project(
                reading, playerCentre, radius, combinedScale,
                playerWorld, new Vector2(marker.Position.X, marker.Position.Z));

            var topLeft = centre - (glyphExtent / 2f);
            var (_, accent) = GameTypeColours.ForGame(marker.Game);

            drawList.AddText(iconFont, diceSize, topLeft + new Vector2(1f, 1f), 0xC0000000, glyph);
            drawList.AddText(iconFont, diceSize, topLeft, ImGui.GetColorU32(accent), glyph);

            var min = topLeft - new Vector2(HoverPadding, HoverPadding);
            var max = topLeft + glyphExtent + new Vector2(HoverPadding, HoverPadding);
            if (ImGui.IsMouseHoveringRect(min, max, false))
            {
                hovered = true;
                hoveredMarker = marker;
            }
        }

        if (hovered)
            DrawTooltip(drawList, mousePos, hoveredMarker);
    }

    private void DrawTooltip(ImDrawListPtr drawList, Vector2 mousePos, in HostMarker marker)
    {
        var (_, accent) = GameTypeColours.ForGame(marker.Game);

        var lines = new List<(string Text, uint Colour)>
        {
            (marker.DisplayName, ImGui.GetColorU32(ThemeColours.AccentText(_config.SecondaryColour))),
            ($"Hosting {marker.Game}", ImGui.GetColorU32(accent)),
        };

        foreach (var rule in marker.Rules)
        {
            var key = RuleKeyFormatting.FormatDisplayKey(rule.Key);
            var value = EventCardRenderer.FormatRuleValue(rule.Value, rule.Key);
            lines.Add(($"{key}:  {value}", 0xFFCFCFCF));
        }

        var pad = new Vector2(8f, 6f) * ImGuiHelpers.GlobalScale;
        var lineAdvance = ImGui.GetTextLineHeightWithSpacing();
        var width = 0f;
        foreach (var line in lines)
            width = MathF.Max(width, ImGui.CalcTextSize(line.Text).X);
        var boxSize = new Vector2(width, lineAdvance * lines.Count) + (pad * 2f);

        var origin = mousePos + new Vector2(16f, 28f) * ImGuiHelpers.GlobalScale;
        var viewport = ImGuiHelpers.MainViewport;
        var maxX = viewport.Pos.X + viewport.Size.X;
        var maxY = viewport.Pos.Y + viewport.Size.Y;
        if (origin.X + boxSize.X > maxX)
            origin.X = maxX - boxSize.X;
        if (origin.Y + boxSize.Y > maxY)
            origin.Y = maxY - boxSize.Y;

        drawList.AddRectFilled(origin, origin + boxSize, 0xE0101010, 4f);
        drawList.AddRect(origin, origin + boxSize, ImGui.GetColorU32(accent), 4f);

        var cursor = origin + pad;
        foreach (var line in lines)
        {
            drawList.AddText(cursor, line.Colour, line.Text);
            cursor.Y += lineAdvance;
        }
    }

    private float ResolveZoneScale()
    {
        var mapSheet = _dataManager.GetExcelSheet<Map>();
        if (mapSheet.TryGetRow(_clientState.MapId, out var map) && map.SizeFactor > 0)
            return map.SizeFactor / 100f;
        return 1f;
    }

    public void Dispose() { }

    // Reads the in-game "_NaviMap" minimap addon and projects world positions onto it.
    // Maths and node indices adapted from https://github.com/GemPlugins/MiniMappingway.
    private static class MinimapProjector
    {
        private const float NaviMapBaseSize = 218f;
        private const float RadiusFactor = 0.315f;
        private const float CentrePivotYOffset = 5f;

        public readonly struct NaviMapReading
        {
            public NaviMapReading(float x, float y, float naviScale, float zoom, float rotation, bool isLocked)
            {
                X = x;
                Y = y;
                NaviScale = naviScale;
                Zoom = zoom;
                Rotation = rotation;
                IsLocked = isLocked;
            }

            public float X { get; }
            public float Y { get; }
            public float NaviScale { get; }
            public float Zoom { get; }
            public float Rotation { get; }
            public bool IsLocked { get; }
        }

        public static unsafe bool TryRead(nint addonPtr, out NaviMapReading reading)
        {
            reading = default;

            var unit = (AtkUnitBase*)addonPtr;
            if (unit == null || !unit->IsVisible)
                return false;

            try
            {
                var isLocked = false;
                var lockNode = unit->GetNodeById(4);
                if (lockNode != null)
                {
                    var checkbox = (AtkComponentCheckBox*)lockNode->GetComponent();
                    if (checkbox != null)
                        isLocked = checkbox->IsChecked;
                }

                var rotation = 0f;
                var rotationNode = unit->GetNodeById(8);
                if (rotationNode != null)
                    rotation = rotationNode->Rotation;

                var zoom = 1f;
                var zoomNode = unit->GetNodeById(18);
                if (zoomNode != null)
                {
                    var component = zoomNode->GetComponent();
                    if (component != null)
                    {
                        var imageNode = component->GetImageNodeById(6);
                        if (imageNode != null)
                            zoom = imageNode->ScaleX;
                    }
                }

                reading = new NaviMapReading(unit->X, unit->Y, unit->Scale, zoom, rotation, isLocked);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static float MapSizeFor(in NaviMapReading reading) => NaviMapBaseSize * reading.NaviScale;

        public static float RadiusFor(in NaviMapReading reading) => MapSizeFor(reading) * RadiusFactor;

        public static Vector2 PlayerCentre(in NaviMapReading reading, Vector2 viewportPos)
        {
            var mapSize = MapSizeFor(reading);
            var centre = new Vector2(reading.X + (mapSize / 2f), reading.Y + (mapSize / 2f)) + viewportPos;
            centre.Y -= CentrePivotYOffset;
            return centre;
        }

        public static Vector2 Project(
            in NaviMapReading reading,
            Vector2 playerCentre,
            float radius,
            float combinedScale,
            Vector2 playerWorldXz,
            Vector2 targetWorldXz)
        {
            var relative = (playerWorldXz - targetWorldXz) * combinedScale;
            var pos = playerCentre - relative;

            if (!reading.IsLocked)
                pos = Rotate(playerCentre, pos, reading.Rotation);

            var distance = Vector2.Distance(playerCentre, pos);
            if (distance > radius && distance > 0f)
            {
                var originToObject = (pos - playerCentre) * (radius / distance);
                pos = playerCentre + originToObject;
            }

            return pos;
        }

        private static Vector2 Rotate(Vector2 center, Vector2 pos, float angle)
        {
            var cos = MathF.Cos(angle);
            var sin = MathF.Sin(angle);
            var dx = pos.X - center.X;
            var dy = pos.Y - center.Y;
            return new Vector2(
                (cos * dx) - (sin * dy) + center.X,
                (sin * dx) + (cos * dy) + center.Y);
        }
    }
}
