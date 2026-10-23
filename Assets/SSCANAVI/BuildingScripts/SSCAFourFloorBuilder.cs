
using System.Collections.Generic;
using UnityEngine;

// SSCA NAVI: 3D dollhouse of the SSCA building, Floors 1-4.
// Everything comes from SSCAFloorLayouts, so the model always matches
// the navigation graph and the AR arrows.
public class SSCAFourFloorBuilder : MonoBehaviour
{
    [Tooltip("Floor buttons and overview camera for the model preview scene. " +
             "The 3D Dollhouse map turns these off and uses its own.")]
    public bool showPreviewControls = true;

    private readonly List<GameObject> floorObjects =
        new List<GameObject>();

    private readonly Dictionary<Color, Material> materials =
        new Dictionary<Color, Material>();

    private int selectedFloor = 1;

    private readonly Color roomColor =
        new Color(0.42f, 0.75f, 0.91f);

    private readonly Color corridorColor =
        new Color(0.82f, 0.88f, 0.91f);

    private readonly Color wallColor =
        new Color(0.9f, 0.94f, 0.97f);

    private readonly Color stairColor =
        new Color(1f, 0.69f, 0.25f);

    private readonly Color doorColor =
        new Color(0.1f, 0.8f, 1f);

    private readonly Color railColor =
        new Color(0.35f, 0.4f, 0.45f);

    private const float WallHeight = 2.5f;
    private const float WallThickness = 0.12f;
    private const float DoorWidth = 1.0f;

    private void Start()
    {
        if (floorObjects.Count == 0)
        {
            BuildAllFloors();
            ShowFloor(1);
        }

        // Demo overview camera.
        Camera cam = Camera.main;
        if (showPreviewControls && cam != null)
        {
            cam.transform.position =
                new Vector3(0f, 40f, -42f);

            cam.transform.LookAt(
                new Vector3(0f, 4.5f, 0f));
        }
    }

    private Material MakeMaterial(Color color)
    {
        Material mat;
        if (materials.TryGetValue(color, out mat))
            return mat;

        // Shared with the arrows; survives shader stripping in phone builds.
        mat = SSCAArrowMesh.CreateMaterial(color);

        materials[color] = mat;
        return mat;
    }

    private GameObject CreateBox(
        string name,
        Transform parent,
        Vector3 position,
        Vector3 scale,
        Color color)
    {
        return CreateBox(name, parent, position, scale, color, Quaternion.identity);
    }

    private GameObject CreateBox(
        string name,
        Transform parent,
        Vector3 position,
        Vector3 scale,
        Color color,
        Quaternion rotation)
    {
        GameObject obj =
            GameObject.CreatePrimitive(PrimitiveType.Cube);

        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localRotation = rotation;
        obj.transform.localScale = scale;

        obj.GetComponent<Renderer>().sharedMaterial =
            MakeMaterial(color);

        return obj;
    }

    private void CreateLabel(
        string message,
        Transform parent,
        Vector3 position)
    {
        GameObject obj = new GameObject(
            "Label - " + message);

        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;

        // Text lies flat on the floor.
        obj.transform.localRotation =
            Quaternion.Euler(90f, 0f, 0f);

        TextMesh text = obj.AddComponent<TextMesh>();
        text.text = message;
        text.fontSize = 34;
        text.characterSize = 0.12f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = Color.black;
    }

    private void BuildAllFloors()
    {
        List<SSCAFloorLayouts.StairLayout> stairs = SSCAFloorLayouts.GetStairs();

        for (int floor = 1; floor <= SSCAFloorLayouts.FloorCount; floor++)
        {
            GameObject level = new GameObject(
                "SSCA - Floor " + floor);

            level.transform.SetParent(
                transform, false);

            level.transform.localPosition =
                new Vector3(0f, (floor - 1) * SSCAFloorLayouts.FloorHeight, 0f);

            floorObjects.Add(level);

            // Hallway floors (top surface at y = 0, where the arrows walk)
            foreach (SSCAFloorLayouts.HallwayLayout hallway in SSCAFloorLayouts.GetHallways(floor))
            {
                Rect a = hallway.Area;
                CreateBox(
                    hallway.Name,
                    level.transform,
                    new Vector3(a.center.x, -0.075f, a.center.y),
                    new Vector3(a.width, 0.15f, a.height),
                    corridorColor);
            }

            // Rooms with walls and door openings
            foreach (SSCAFloorLayouts.RoomLayout room in SSCAFloorLayouts.GetRooms(floor))
                CreateRoom(level.transform, room);

            // The central area is intentionally open: railing only, no slab.
            CreateOpenAreaRailing(level.transform, floor);

            // Stairs that start on this floor
            foreach (SSCAFloorLayouts.StairLayout stair in stairs)
            {
                if (stair.FromFloor == floor)
                    CreateStaircase(level.transform, stair);
            }

            Rect open = SSCAFloorLayouts.GetOpenArea(floor);
            CreateLabel(
                "FLOOR " + floor,
                level.transform,
                new Vector3(open.center.x, 0.12f, open.center.y));
        }
    }

    private void CreateRoom(Transform parent, SSCAFloorLayouts.RoomLayout room)
    {
        Rect a = room.Area;

        CreateBox(
            "Room Floor - " + room.Name,
            parent,
            new Vector3(a.center.x, -0.05f, a.center.y),
            new Vector3(a.width, 0.1f, a.height),
            roomColor);

        CreateWall(parent, room.Name + " North Wall",
            new Vector2(a.xMin, a.yMax), new Vector2(a.xMax, a.yMax),
            room.DoorFacing == Vector3.forward, room.Door);

        CreateWall(parent, room.Name + " South Wall",
            new Vector2(a.xMin, a.yMin), new Vector2(a.xMax, a.yMin),
            room.DoorFacing == Vector3.back, room.Door);

        CreateWall(parent, room.Name + " West Wall",
            new Vector2(a.xMin, a.yMin), new Vector2(a.xMin, a.yMax),
            room.DoorFacing == Vector3.left, room.Door);

        CreateWall(parent, room.Name + " East Wall",
            new Vector2(a.xMax, a.yMin), new Vector2(a.xMax, a.yMax),
            room.DoorFacing == Vector3.right, room.Door);

        // Blue doorway threshold on the floor. Does not block the doorway.
        bool eastWestDoor = Mathf.Abs(room.DoorFacing.x) > 0.5f;
        CreateBox(
            room.Name + " Door Threshold",
            parent,
            new Vector3(room.Door.x, 0.02f, room.Door.z),
            eastWestDoor
                ? new Vector3(0.14f, 0.04f, DoorWidth)
                : new Vector3(DoorWidth, 0.04f, 0.14f),
            doorColor);

        CreateLabel(
            room.Name,
            parent,
            new Vector3(a.center.x, 0.06f, a.center.y));
    }

    // A wall from one corner to another, with a door gap if needed.
    private void CreateWall(
        Transform parent,
        string wallName,
        Vector2 from,
        Vector2 to,
        bool hasDoor,
        Vector3 door)
    {
        if (!hasDoor)
        {
            CreateWallSegment(parent, wallName, from, to, WallHeight, wallColor);
            return;
        }

        float length = Vector2.Distance(from, to);
        Vector2 direction = (to - from) / length;
        float doorAt = Vector2.Dot(new Vector2(door.x, door.z) - from, direction);
        float gapStart = Mathf.Clamp(doorAt - DoorWidth / 2f, 0f, length);
        float gapEnd = Mathf.Clamp(doorAt + DoorWidth / 2f, 0f, length);

        if (gapStart > 0.01f)
            CreateWallSegment(parent, wallName + " A", from, from + direction * gapStart, WallHeight, wallColor);

        if (length - gapEnd > 0.01f)
            CreateWallSegment(parent, wallName + " B", from + direction * gapEnd, to, WallHeight, wallColor);
    }

    private void CreateWallSegment(
        Transform parent,
        string wallName,
        Vector2 from,
        Vector2 to,
        float height,
        Color color)
    {
        Vector2 middle = (from + to) / 2f;
        float length = Vector2.Distance(from, to);
        bool alongX = Mathf.Abs(to.x - from.x) > Mathf.Abs(to.y - from.y);

        CreateBox(
            wallName,
            parent,
            new Vector3(middle.x, height / 2f, middle.y),
            alongX
                ? new Vector3(length, height, WallThickness)
                : new Vector3(WallThickness, height, length),
            color);
    }

    private void CreateOpenAreaRailing(Transform parent, int floor)
    {
        // Floor 1 has no floor below it; its central area is just left open.
        if (floor == 1)
            return;

        Rect a = SSCAFloorLayouts.GetOpenArea(floor);
        const float railHeight = 1.0f;

        CreateWallSegment(parent, "Railing North", new Vector2(a.xMin, a.yMax), new Vector2(a.xMax, a.yMax), railHeight, railColor);
        CreateWallSegment(parent, "Railing South", new Vector2(a.xMin, a.yMin), new Vector2(a.xMax, a.yMin), railHeight, railColor);
        CreateWallSegment(parent, "Railing West", new Vector2(a.xMin, a.yMin), new Vector2(a.xMin, a.yMax), railHeight, railColor);
        CreateWallSegment(parent, "Railing East", new Vector2(a.xMax, a.yMin), new Vector2(a.xMax, a.yMax), railHeight, railColor);
    }

    // Builds the steps along the stair walking line: sloped parts become
    // steps, flat parts become landings.
    private void CreateStaircase(Transform parent, SSCAFloorLayouts.StairLayout stair)
    {
        GameObject stairs = new GameObject(
            stair.Name + " Staircase to Floor " + (stair.FromFloor + 1));
        stairs.transform.SetParent(parent, false);

        float floorHeight = SSCAFloorLayouts.FloorHeight;

        for (int i = 0; i < stair.Path.Length - 1; i++)
        {
            Vector3 a = stair.Path[i];
            Vector3 b = stair.Path[i + 1];
            a.y *= floorHeight;
            b.y *= floorHeight;

            Vector3 flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
            float run = flat.magnitude;
            if (run < 0.01f)
                continue;

            Quaternion rotation = Quaternion.LookRotation(flat / run);

            if (Mathf.Abs(b.y - a.y) < 0.01f)
            {
                // Landing
                if (a.y < 0.01f)
                    continue;

                Vector3 middle = (a + b) / 2f;
                CreateBox(
                    "Landing",
                    stairs.transform,
                    new Vector3(middle.x, a.y / 2f, middle.z),
                    new Vector3(stair.Width, a.y, run + stair.Width),
                    stairColor,
                    rotation);
                continue;
            }

            int stepCount = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(b.y - a.y) / 0.25f));
            float stepRun = run / stepCount;

            for (int s = 0; s < stepCount; s++)
            {
                float top = Mathf.Lerp(a.y, b.y, (s + 1f) / stepCount);
                Vector3 centre = Vector3.Lerp(a, b, (s + 0.5f) / stepCount);

                CreateBox(
                    "Step " + (s + 1),
                    stairs.transform,
                    new Vector3(centre.x, top / 2f, centre.z),
                    new Vector3(stair.Width, top, stepRun),
                    stairColor,
                    rotation);
            }
        }
    }

    // 1-4 shows one floor, 0 shows the whole building.
    public void ShowFloor(int floor)
    {
        if (floorObjects.Count == 0)
            BuildAllFloors();

        selectedFloor = floor;

        for (int i = 0; i < floorObjects.Count; i++)
            floorObjects[i].SetActive(floor == 0 || i == floor - 1);
    }

    private void OnGUI()
    {
        if (!showPreviewControls)
            return;

        GUI.Box(
            new Rect(12, 12, 340, 55),
            "SSCA NAVI - 3D FLOOR PREVIEW");

        for (int floor = 1; floor <= SSCAFloorLayouts.FloorCount; floor++)
        {
            if (GUI.Button(
                new Rect(
                    12 + (floor - 1) * 68,
                    72, 64, 48),
                "Floor " + floor))
            {
                ShowFloor(floor);
            }
        }

        if (GUI.Button(new Rect(12 + 4 * 68, 72, 64, 48), "All"))
            ShowFloor(0);

        GUI.Label(
            new Rect(12, 128, 300, 36),
            selectedFloor == 0 ? "Viewing all floors" : "Viewing Floor " + selectedFloor);
    }

    private void OnDestroy()
    {
        foreach (Material mat in materials.Values)
            if (mat != null) Destroy(mat);
    }
}
