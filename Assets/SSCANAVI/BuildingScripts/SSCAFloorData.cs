using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SSCARoom
{
    public string roomName;
    public int floor;
    public Vector3 position;
    public Vector3 doorPosition;

    public SSCARoom(
        string name,
        int floorNumber,
        Vector3 room,
        Vector3 door)
    {
        roomName = name;
        floor = floorNumber;
        position = room;
        doorPosition = door;
    }
}

// Inspector list of every SSCA room (Floors 1-4) with room centre and door,
// in building coordinates. Read-only view of SSCAFloorLayouts, kept so the
// room data can be checked in the Inspector of the model scene.
public class SSCAFloorData : MonoBehaviour
{
    public List<SSCARoom> rooms = new List<SSCARoom>();

    private void Awake()
    {
        InitializeRooms();
    }

    private void InitializeRooms()
    {
        rooms.Clear();

        for (int floor = 1; floor <= SSCAFloorLayouts.FloorCount; floor++)
        {
            Vector3 height = Vector3.up * (floor - 1) * SSCAFloorLayouts.FloorHeight;

            foreach (SSCAFloorLayouts.RoomLayout room in SSCAFloorLayouts.GetRooms(floor))
            {
                rooms.Add(new SSCARoom(
                    room.Name,
                    floor,
                    room.Position + height,
                    room.Door + height));
            }
        }
    }
}
