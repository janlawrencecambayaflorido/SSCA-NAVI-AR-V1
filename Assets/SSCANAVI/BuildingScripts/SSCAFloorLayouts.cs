using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// SSCA building layout, traced from the official SSCA floor plans (SCALE: NTS).
// Units are metres. X = east (+) / west (-), Z = north (+) / south (-),
// origin = centre of the building, which is assumed to be 34 m x 21 m.
// Floors 1-3 share the same hallways and stairs; Floor 4 is the smaller L-shaped floor.
// Replace the numbers with measured values once the school is surveyed.
// Shared by the 3D builder, the navigation graph and the AR guide.
public static class SSCAFloorLayouts
{
    public const int FloorCount = 4;
    public const float FloorHeight = 3.5f;
    public const float BuildingWidth = 34f;  // X
    public const float BuildingDepth = 21f;  // Z

    public struct RoomLayout
    {
        public string Name;
        public Rect Area;           // x = X, y = Z
        public Vector3 Door;        // point on the room wall (y = 0)
        public Vector3 DoorFacing;  // from the room out into the hallway

        public Vector3 Position
        {
            get { return new Vector3(Area.center.x, 0f, Area.center.y); }
        }

        public RoomLayout(string name, float xMin, float zMin, float xMax, float zMax, float doorX, float doorZ)
        {
            Name = name;
            Area = Rect.MinMaxRect(xMin, zMin, xMax, zMax);
            Door = new Vector3(doorX, 0f, doorZ);

            // The door is on the wall it is closest to.
            float west = Mathf.Abs(doorX - xMin);
            float east = Mathf.Abs(doorX - xMax);
            float south = Mathf.Abs(doorZ - zMin);
            float north = Mathf.Abs(doorZ - zMax);
            float closest = Mathf.Min(Mathf.Min(west, east), Mathf.Min(south, north));

            if (closest == south) DoorFacing = Vector3.back;
            else if (closest == north) DoorFacing = Vector3.forward;
            else if (closest == west) DoorFacing = Vector3.left;
            else DoorFacing = Vector3.right;
        }
    }

    public struct HallwayLayout
    {
        public string Name;
        public Rect Area;          // walkable floor that is drawn
        public bool HasPath;       // false = drawn only, no navigation centre line
        public Vector3 PathStart;  // navigation centre line (y = 0)
        public Vector3 PathEnd;

        public HallwayLayout(string name, float xMin, float zMin, float xMax, float zMax)
        {
            Name = name;
            Area = Rect.MinMaxRect(xMin, zMin, xMax, zMax);
            HasPath = false;
            PathStart = Vector3.zero;
            PathEnd = Vector3.zero;
        }

        public HallwayLayout(string name, float xMin, float zMin, float xMax, float zMax,
                             float startX, float startZ, float endX, float endZ)
        {
            Name = name;
            Area = Rect.MinMaxRect(xMin, zMin, xMax, zMax);
            HasPath = true;
            PathStart = new Vector3(startX, 0f, startZ);
            PathEnd = new Vector3(endX, 0f, endZ);
        }
    }

    public struct StairLayout
    {
        public string Name;       // "NW", "SW", "SE", "W"
        public int FromFloor;     // goes up to FromFloor + 1
        public float Width;       // width of one flight
        public Vector3[] Path;    // walking line; y = 0..1 of the floor height

        public StairLayout(string name, int fromFloor, float width, params Vector3[] path)
        {
            Name = name;
            FromFloor = fromFloor;
            Width = width;
            Path = path;
        }
    }

    public static List<RoomLayout> GetRooms(int floor)
    {
        var rooms = new List<RoomLayout>();

        if (floor == 1)
        {
            // Same structure as the 2nd floor (Rooms 204-211 -> 101-108).
            // Room 109 is placed in the Computer Room position - verify at SSCA.
            rooms.Add(new RoomLayout("Room 101", -6.9f, 5.6f, 1.35f, 10.5f, -6.3f, 5.6f));
            rooms.Add(new RoomLayout("Room 102", 1.35f, 5.6f, 9.5f, 10.5f, 8.7f, 5.6f));
            rooms.Add(new RoomLayout("Room 103", 9.5f, 5.6f, 17f, 10.5f, 10.1f, 5.6f));
            rooms.Add(new RoomLayout("Room 104", 11.9f, 1.2f, 17f, 5.6f, 11.9f, 1.9f));
            rooms.Add(new RoomLayout("Room 105", 11.9f, -3.1f, 17f, 1.2f, 11.9f, 0.7f));
            rooms.Add(new RoomLayout("Room 106", 9.5f, -10.5f, 17f, -8.4f, 10.15f, -8.4f));
            rooms.Add(new RoomLayout("Room 107", 1.35f, -10.5f, 9.5f, -5.6f, 9.0f, -5.6f));
            rooms.Add(new RoomLayout("Room 108", -6.9f, -10.5f, 1.35f, -5.6f, -6.3f, -5.6f));
            rooms.Add(new RoomLayout("Room 109", -17f, -10.5f, -10.2f, -5.0f, -10.2f, -5.8f));
        }
        else if (floor == 2)
        {
            rooms.Add(new RoomLayout("Director's Office", -17f, 4.3f, -15f, 10.5f, -15f, 5.0f));
            rooms.Add(new RoomLayout("Guidance", -14.1f, 5.6f, -12.5f, 10.5f, -13.4f, 5.6f));
            rooms.Add(new RoomLayout("Clinic", -12.5f, 5.6f, -10.2f, 10.5f, -11.0f, 5.6f));
            rooms.Add(new RoomLayout("Room 204", -6.9f, 5.6f, 1.35f, 10.5f, -6.3f, 5.6f));
            rooms.Add(new RoomLayout("Room 205", 1.35f, 5.6f, 9.5f, 10.5f, 8.7f, 5.6f));
            rooms.Add(new RoomLayout("Room 206", 9.5f, 5.6f, 17f, 10.5f, 10.1f, 5.6f));
            rooms.Add(new RoomLayout("Room 207", 11.9f, 1.2f, 17f, 5.6f, 11.9f, 1.9f));
            rooms.Add(new RoomLayout("Room 208", 11.9f, -3.1f, 17f, 1.2f, 11.9f, 0.7f));
            rooms.Add(new RoomLayout("Room 209", 9.5f, -10.5f, 17f, -8.4f, 10.15f, -8.4f));
            rooms.Add(new RoomLayout("Room 210", 1.35f, -10.5f, 9.5f, -5.6f, 9.0f, -5.6f));
            rooms.Add(new RoomLayout("Room 211", -6.9f, -10.5f, 1.35f, -5.6f, -6.3f, -5.6f));
            rooms.Add(new RoomLayout("TVL Room", -17f, 0.4f, -10.2f, 4.0f, -10.2f, 3.7f));
            rooms.Add(new RoomLayout("TLE Room", -17f, -3.3f, -10.2f, 0.4f, -10.2f, -3.0f));
            rooms.Add(new RoomLayout("Computer Room", -17f, -10.5f, -10.2f, -5.0f, -10.2f, -5.8f));
        }
        else if (floor == 3)
        {
            rooms.Add(new RoomLayout("Video Room", -15f, 5.6f, -11f, 10.5f, -14.2f, 5.6f));
            rooms.Add(new RoomLayout("Room 303", -6.9f, 5.6f, 1.35f, 10.5f, -6.3f, 5.6f));
            rooms.Add(new RoomLayout("Room 304", 1.35f, 5.6f, 9.5f, 10.5f, 1.9f, 5.6f));
            rooms.Add(new RoomLayout("Room 305", 9.5f, 6.3f, 17f, 10.5f, 10.2f, 6.3f));
            rooms.Add(new RoomLayout("C.R. Girls", 11.9f, 5.1f, 17f, 6.3f, 11.9f, 5.7f));
            rooms.Add(new RoomLayout("C.R. Boys", 11.9f, 3.9f, 17f, 5.1f, 11.9f, 4.5f));
            rooms.Add(new RoomLayout("Faculty H.S.", 11.9f, 2.2f, 17f, 3.9f, 11.9f, 3.0f));
            rooms.Add(new RoomLayout("Room 307", 11.9f, -3.3f, 17f, 2.2f, 11.9f, 1.7f));
            rooms.Add(new RoomLayout("Faculty Elem.", 9.5f, -10.5f, 17f, -5.6f, 10.2f, -5.6f));
            rooms.Add(new RoomLayout("Room 309", 1.35f, -10.5f, 9.5f, -5.6f, 1.9f, -5.6f));
            rooms.Add(new RoomLayout("Room 310", -6.9f, -10.5f, 1.35f, -5.6f, -6.3f, -5.6f));
            rooms.Add(new RoomLayout("Room 311", -17f, -10.5f, -10.2f, -3.3f, -10.2f, -4.0f));
            rooms.Add(new RoomLayout("H.S. Library", -17f, -3.3f, -10.2f, 0.4f, -10.2f, -3.0f));
            rooms.Add(new RoomLayout("Audio Visual Room", -17f, 0.4f, -10.2f, 4.0f, -10.2f, 3.6f));
        }
        else if (floor == 4)
        {
            rooms.Add(new RoomLayout("Room 401 - Physics", -13.7f, 6.0f, -6.35f, 10.5f, -6.8f, 6.0f));
            rooms.Add(new RoomLayout("Room 402 - Biology", -6.35f, 6.0f, 1.6f, 10.5f, -5.8f, 6.0f));
            rooms.Add(new RoomLayout("Room 403 - Chemistry", 1.6f, 6.0f, 9.7f, 10.5f, 2.0f, 6.0f));
            rooms.Add(new RoomLayout("Room 404 - TVL Bartending", 9.7f, 6.0f, 17f, 10.5f, 10.1f, 6.0f));
            rooms.Add(new RoomLayout("Room 405 - Industrial", 9.8f, -4.0f, 17f, 4.1f, 10.2f, 4.1f));
            rooms.Add(new RoomLayout("Room 406", -13.7f, 0.0f, -6.7f, 4.1f, -13.7f, 3.5f));
            rooms.Add(new RoomLayout("Room 407", -13.7f, -4.4f, -6.7f, 0.0f, -13.7f, -0.6f));
            rooms.Add(new RoomLayout("Room 408", -13.4f, -10.2f, -8.3f, -4.4f, -13.4f, -5.0f));
            rooms.Add(new RoomLayout("Room 409 - Files Room", -16.7f, -10.4f, -13.4f, -6.0f, -15.0f, -6.0f));
            rooms.Add(new RoomLayout("C.R. (West)", -16.7f, 4.1f, -15.2f, 6.0f, -15.2f, 5.0f));
            rooms.Add(new RoomLayout("C.R. (East)", 13.8f, 4.1f, 17f, 6.0f, 13.8f, 5.0f));
        }

        return rooms;
    }

    public static List<HallwayLayout> GetHallways(int floor)
    {
        var hallways = new List<HallwayLayout>();

        if (floor >= 1 && floor <= 3)
        {
            // Floor 3's north hallway continues west to the stairs going up to Floor 4.
            float northMinX = floor == 3 ? -17f : -15f;
            float northStartX = floor == 3 ? -16f : -15f;

            hallways.Add(new HallwayLayout("North Hallway", northMinX, 4.0f, 11.9f, 5.6f,
                northStartX, 4.8f, 10.7f, 4.8f));
            hallways.Add(new HallwayLayout("West Hallway", -10.2f, -6.4f, -6.9f, 4.0f,
                -8.55f, 4.8f, -8.55f, -6.4f));
            hallways.Add(new HallwayLayout("East Hallway", 9.5f, -3.3f, 11.9f, 4.0f,
                10.7f, 4.8f, 10.7f, -4.45f));
            hallways.Add(new HallwayLayout("South Hallway", -10.2f, -5.6f, 12.9f, -3.3f,
                -8.55f, -4.45f, 12.9f, -4.45f));

            if (floor <= 2)
            {
                // Small passage beside the C.R. leading to Room 209 / Room 106.
                hallways.Add(new HallwayLayout("South-East Passage", 9.5f, -8.4f, 10.8f, -5.6f));
            }
        }
        else if (floor == 4)
        {
            hallways.Add(new HallwayLayout("North Hallway", -15.2f, 4.1f, 13.8f, 6.0f,
                -14.4f, 5.0f, 13.0f, 5.0f));
            hallways.Add(new HallwayLayout("West Hallway (Upper)", -15.0f, 6.0f, -13.7f, 10.3f,
                -14.4f, 10.0f, -14.4f, 5.0f));
            hallways.Add(new HallwayLayout("West Hallway", -16.7f, -6.0f, -13.7f, 4.1f,
                -14.4f, 5.0f, -14.4f, -6.0f));
        }

        return hallways;
    }

    // The central "OPEN DOWN BELOW" area (not walkable).
    public static Rect GetOpenArea(int floor)
    {
        return floor == 4
            ? Rect.MinMaxRect(-6.7f, -4.4f, 9.8f, 4.1f)
            : Rect.MinMaxRect(-6.9f, -3.3f, 9.5f, 4.0f);
    }

    public static List<StairLayout> GetStairs()
    {
        var stairs = new List<StairLayout>();

        // Floors 1-2-3: three U-shaped staircases (two flights and a landing).
        for (int floor = 1; floor <= 2; floor++)
        {
            stairs.Add(new StairLayout("NW", floor, 1.5f,
                new Vector3(-9.4f, 0f, 5.8f),
                new Vector3(-9.4f, 0.5f, 9.75f),
                new Vector3(-7.7f, 0.5f, 9.75f),
                new Vector3(-7.7f, 1f, 5.8f)));

            stairs.Add(new StairLayout("SW", floor, 1.5f,
                new Vector3(-9.4f, 0f, -6.6f),
                new Vector3(-9.4f, 0.5f, -9.75f),
                new Vector3(-7.7f, 0.5f, -9.75f),
                new Vector3(-7.7f, 1f, -6.6f)));

            stairs.Add(new StairLayout("SE", floor, 1.1f,
                new Vector3(13.1f, 0f, -5.05f),
                new Vector3(16.1f, 0.5f, -5.05f),
                new Vector3(16.1f, 0.5f, -3.85f),
                new Vector3(13.1f, 1f, -3.85f)));
        }

        // Floor 3 -> 4: straight staircase at the far west ("UP" on 3rd, "DOWN" on 4th).
        stairs.Add(new StairLayout("W", 3, 1.6f,
            new Vector3(-16.0f, 0f, 5.8f),
            new Vector3(-16.0f, 1f, 10.0f)));

        return stairs;
    }

    // "Room 101" on Floor 1 -> "F1_ROOM_101_DOOR"
    public static string GetDoorWaypointId(RoomLayout room, int floor)
    {
        return "F" + floor + "_" + ToId(room.Name) + "_DOOR";
    }

    // "H.S. Library" -> "H_S_LIBRARY"
    public static string ToId(string name)
    {
        return Regex.Replace(name.ToUpperInvariant(), @"[^A-Z0-9]+", "_").Trim('_');
    }
}
