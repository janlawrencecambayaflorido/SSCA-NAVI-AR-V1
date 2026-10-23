using System.Collections.Generic;
using UnityEngine;

// SSCA NAVI: room picker used by the AR view and the 3D map.
// Floor tabs plus a two-column list, sized for a phone held upright
// (GUI coordinates are in the 420-wide virtual screen used by both screens).
public static class SSCARoomMenu
{
    // Returns the room the student tapped, or null.
    public static SSCARoomNavigation.RoomDestination Draw(
        float top,
        List<SSCARoomNavigation.RoomDestination> rooms,
        ref int menuFloor,
        GUIStyle buttonStyle)
    {
        int floorShown = menuFloor;
        List<SSCARoomNavigation.RoomDestination> floorRooms =
            rooms.FindAll(r => r.floor == floorShown);
        int rows = (floorRooms.Count + 1) / 2;

        GUI.Box(new Rect(8, top, 405, 64 + rows * 50), "");
        for (int floor = 1; floor <= SSCAFloorLayouts.FloorCount; floor++)
        {
            string tab = floor == menuFloor ? "[ Floor " + floor + " ]" : "Floor " + floor;
            if (GUI.Button(new Rect(14 + (floor - 1) * 98, top + 6, 94, 46), tab, buttonStyle))
                menuFloor = floor;
        }

        SSCARoomNavigation.RoomDestination picked = null;
        for (int i = 0; i < floorRooms.Count; i++)
        {
            Rect button = new Rect(14 + (i % 2) * 198, top + 60 + (i / 2) * 50, 192, 46);
            if (GUI.Button(button, floorRooms[i].roomName, buttonStyle))
                picked = floorRooms[i];
        }
        return picked;
    }
}
