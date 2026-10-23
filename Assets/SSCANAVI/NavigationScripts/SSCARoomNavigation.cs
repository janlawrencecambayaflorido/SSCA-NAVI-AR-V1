
using System.Collections.Generic;
using UnityEngine;

public class SSCARoomNavigation : MonoBehaviour
{
    [System.Serializable]
    public class RoomDestination
    {
        public string roomName;
        public int floor;
        public string waypointId;

        // Direction a student faces when standing in the doorway
        // with their back to the room (used for AR calibration).
        public Vector3 hallwayDirection;
    }

    public SSCANavigationGraph navigationGraph;

    [Header("Room Destinations")]
    public List<RoomDestination> rooms =
        new List<RoomDestination>();

    [Header("Navigation Testing")]
    public string startingRoom = "Room 101";
    public string destinationRoom = "H.S. Library";

    private void Awake()
    {
        CreateRooms();
    }

    public void EnsureRooms()
    {
        if (rooms == null || rooms.Count == 0)
            CreateRooms();
    }

    // Floors 1 to 4, from the same layout as the 3D model.
    private void CreateRooms()
    {
        if (rooms == null)
            rooms = new List<RoomDestination>();

        rooms.Clear();

        for (int floor = 1; floor <= SSCAFloorLayouts.FloorCount; floor++)
        {
            foreach (SSCAFloorLayouts.RoomLayout room in SSCAFloorLayouts.GetRooms(floor))
            {
                rooms.Add(new RoomDestination
                {
                    roomName = room.Name,
                    floor = floor,
                    waypointId = SSCAFloorLayouts.GetDoorWaypointId(room, floor),
                    hallwayDirection = room.DoorFacing
                });
            }
        }
    }

    public RoomDestination FindRoom(string roomName)
    {
        EnsureRooms();
        return rooms.Find(r => r.roomName == roomName);
    }

    [ContextMenu("Test Room Navigation")]
    public void TestRoomNavigation()
    {
        if (navigationGraph == null)
        {
            Debug.LogError(
                "SSCA NAVI: Navigation Graph not assigned.");
            return;
        }

        RoomDestination start = FindRoom(startingRoom);
        RoomDestination destination = FindRoom(destinationRoom);

        if (start == null || destination == null)
        {
            Debug.LogWarning(
                "SSCA NAVI: Invalid room selection.");
            return;
        }

        List<SSCANavigationGraph.Waypoint> path =
            navigationGraph.ShowRoute(
                start.waypointId,
                destination.waypointId);

        if (path.Count == 0)
        {
            Debug.LogWarning(
                "SSCA NAVI: Room route not found.");
            return;
        }

        string route = "";

        foreach (var waypoint in path)
            route += waypoint.id + " -> ";

        Debug.Log(
            "SSCA NAVI ROOM ROUTE: " +
            startingRoom + " to " +
            destinationRoom + " | " + route);
    }
}
