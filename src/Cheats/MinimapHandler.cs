using System.Collections.Generic;
using UnityEngine;

namespace MalumMenu;
public static class MinimapHandler
{
    public static bool minimapActive;
    public static List<HerePoint> herePoints = new List<HerePoint>();
    public static List<HerePoint> herePointsToRemove = new List<HerePoint>();
    public static Dictionary<byte, Vector3> lastKnownHerePointPositions = new Dictionary<byte, Vector3>();

    public static Vector3 GetHerePointLocalPosition(PlayerControl player)
    {
        var truePosition = player.GetTruePosition();
        var vector = new Vector3(truePosition.x, truePosition.y, -1f);
        vector /= ShipStatus.Instance.MapScale;
        vector.x *= Mathf.Sign(ShipStatus.Instance.transform.localScale.x);
        return vector;
    }

    public static Vector3 GetCachedHerePointLocalPosition(PlayerControl player)
    {
        return lastKnownHerePointPositions.TryGetValue(player.PlayerId, out var cached)
            ? cached
            : GetHerePointLocalPosition(player);
    }

    public static void SnapshotHerePointPositions()
    {
        if (!Utils.isPlayer || ShipStatus.Instance == null) return;

        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (!player) continue;

            lastKnownHerePointPositions[player.PlayerId] = GetHerePointLocalPosition(player);
        }
    }

    public static bool IsCheatEnabled()
    {
        return CheatToggles.mapCrew || CheatToggles.mapGhosts || CheatToggles.mapImps;
    }

    public static void HandleHerePoint(HerePoint herePoint)
    {
        // Skip the local player — the game handles their own "YOU ARE HERE" dot natively
        if (herePoint.player.AmOwner) return;

        var pauseEspDuringMeeting = CheatToggles.pauseEspDuringMeetings && Utils.isMeeting;

        if (!pauseEspDuringMeeting)
        {
            herePoint.sprite.gameObject.SetActive(false); // Initially make player icon invisible
        }

        bool shouldShow = false;
        Color herePointColor = Palette.White; // Safe default — won't be used unless shouldShow is true

        try // try-catch to fix issues caused by player disconnection
        {
            // Crewmate, alive
            if (CheatToggles.mapCrew && !herePoint.player.Data.Role.IsImpostor && !herePoint.player.Data.IsDead)
            {
                shouldShow = true;
                herePointColor = CheatToggles.colorBasedMap
                    ? herePoint.player.Data.Color      // Color-Based Icon
                    : herePoint.player.Data.Role.TeamColor; // Role-Based Icon
            }
            // Impostor, alive
            else if (CheatToggles.mapImps && herePoint.player.Data.Role.IsImpostor && !herePoint.player.Data.IsDead)
            {
                shouldShow = true;
                herePointColor = CheatToggles.colorBasedMap
                    ? herePoint.player.Data.Color      // Color-Based Icon
                    : herePoint.player.Data.Role.TeamColor; // Role-Based Icon
            }

            // Any Role, dead (runs independently — a dead impostor can also be a ghost)
            if (CheatToggles.mapGhosts && herePoint.player.Data.IsDead)
            {
                shouldShow = true;
                herePointColor = CheatToggles.colorBasedMap
                    ? herePoint.player.Data.Color // Color-Based Icon
                    : Palette.White;
            }

            herePoint.sprite.gameObject.SetActive(shouldShow);

            if (shouldShow)
            {
                // Set the right colors for active herePoint icons
                herePoint.sprite.material.SetColor(PlayerMaterial.BackColor, herePointColor);
                herePoint.sprite.material.SetColor(PlayerMaterial.BodyColor, herePointColor);
                herePoint.sprite.material.SetColor(PlayerMaterial.VisorColor, Palette.VisorColor);

                // Sync the position of active herePoint icons with their players unless ESP is paused during a meeting
                if (!pauseEspDuringMeeting)
                {
                    herePoint.lastKnownLocalPosition = GetHerePointLocalPosition(herePoint.player);
                    lastKnownHerePointPositions[herePoint.player.PlayerId] = herePoint.lastKnownLocalPosition;
                }

                herePoint.sprite.transform.localPosition = herePoint.lastKnownLocalPosition;
            }
        }
        catch
        {
            // Remove icons that are causing problems
            Object.Destroy(herePoint.sprite.gameObject);
            herePointsToRemove.Add(herePoint);
        }
    }
}