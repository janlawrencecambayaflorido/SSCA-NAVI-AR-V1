using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// SSCA NAVI: 3D Dollhouse map of the SSCA building (Floors 1-4).
// The student picks From / To and the cyan route is drawn on the 3D model,
// across floors and up the stairs. Drag to rotate, scroll or pinch to zoom.
// "Open AR Camera" switches to the live AR navigation scene.
// Uses the same layout and navigation graph as the AR view.
public class SSCANaviDemo : MonoBehaviour
{
    [Header("Scenes")]
    public string arSceneName = "SSCA_AR_Navigation";

    [Header("Camera")]
    public float orbit = 200f;     // degrees around the building
    public float elevation = 55f;  // degrees above the horizon
    public float distance = 42f;

    [Header("Route")]
    public float arrowSpacing = 0.9f;

    private SSCAFourFloorBuilder building;
    private SSCANavigationGraph navigationGraph;
    private SSCARoomNavigation roomNavigation;
    private Camera cam;

    private int viewFloor = 1; // 0 = whole building
    private SSCARoomNavigation.RoomDestination startRoom;
    private SSCARoomNavigation.RoomDestination destinationRoom;
    private enum Menu { None, From, To }
    private Menu menu = Menu.None;
    private int menuFloor = 1;
    private string message;

    private List<SSCANavigationGraph.Waypoint> path = new List<SSCANavigationGraph.Waypoint>();
    private float routeLength;
    private readonly List<GameObject> arrows = new List<GameObject>();
    private readonly List<Renderer> arrowRenderers = new List<Renderer>();
    private readonly List<float> arrowDistances = new List<float>();
    private readonly List<int> arrowFloors = new List<int>();
    private GameObject startMarker;
    private GameObject destinationMarker;

    private Material arrowMaterial;
    private Material startMaterial;
    private Mesh arrowMesh;
    private MaterialPropertyBlock arrowColors;
    private GUIStyle buttonStyle;
    private GUIStyle headerStyle;
    private GUIStyle statusStyle;
    private float guiScale = 1f;
    private float lastPinch = -1f;

    private const float PanelBottom = 246f;  // GUI units
    private const float StatusHeight = 104f;

    private void Awake()
    {
        cam = Camera.main;
        if (cam == null)
        {
            GameObject cameraGo = new GameObject("SSCA Map Camera");
            cameraGo.tag = "MainCamera";
            cam = cameraGo.AddComponent<Camera>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.12f, 0.17f);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 500f;

        building = Object.FindFirstObjectByType<SSCAFourFloorBuilder>();
        if (building == null)
            building = new GameObject("SSCA Building (runtime)").AddComponent<SSCAFourFloorBuilder>();
        building.showPreviewControls = false;

        navigationGraph = Object.FindFirstObjectByType<SSCANavigationGraph>();
        roomNavigation = Object.FindFirstObjectByType<SSCARoomNavigation>();
        if (navigationGraph == null || roomNavigation == null)
        {
            GameObject data = new GameObject("SSCA Navigation Data (runtime)");
            if (navigationGraph == null)
                navigationGraph = data.AddComponent<SSCANavigationGraph>();
            if (roomNavigation == null)
                roomNavigation = data.AddComponent<SSCARoomNavigation>();
        }
        if (roomNavigation.navigationGraph == null)
            roomNavigation.navigationGraph = navigationGraph;
        roomNavigation.EnsureRooms();

        arrowMaterial = SSCAArrowMesh.CreateMaterial(SSCAArrowMesh.BrightCyan);
        startMaterial = SSCAArrowMesh.CreateMaterial(new Color(0.2f, 0.9f, 0.3f));
        arrowMesh = SSCAArrowMesh.Create(1.6f); // larger, so it reads from above
        arrowColors = new MaterialPropertyBlock();
    }

    private void Start()
    {
        SetView(1);
    }

    private void SetView(int floor)
    {
        viewFloor = floor;
        building.ShowFloor(floor);

        for (int i = 0; i < arrows.Count; i++)
            arrows[i].SetActive(floor == 0 || arrowFloors[i] == floor);

        if (startMarker != null)
            startMarker.SetActive(floor == 0 || startRoom.floor == floor);
        if (destinationMarker != null)
            destinationMarker.SetActive(floor == 0 || destinationRoom.floor == floor);
    }

    private void ShowRoute()
    {
        ClearRoute();

        if (startRoom == null || destinationRoom == null)
        {
            message = "Choose both From and To first.";
            return;
        }

        if (startRoom == destinationRoom)
        {
            message = "You are already at " + destinationRoom.roomName + ".";
            return;
        }

        path = navigationGraph.ShowRoute(startRoom.waypointId, destinationRoom.waypointId);
        if (path.Count < 2 || arrowMaterial == null)
        {
            message = "No route found from " + startRoom.roomName + " to " + destinationRoom.roomName + ".";
            return;
        }

        routeLength = 0f;
        float nextArrow = arrowSpacing * 0.5f;

        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 from = path[i].position + Vector3.up * 0.08f;
            Vector3 to = path[i + 1].position + Vector3.up * 0.08f;
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.01f) continue;
            Vector3 direction = delta / length;

            while (nextArrow < routeLength + length)
            {
                Vector3 position = from + direction * (nextArrow - routeLength);
                CreateArrow(position, direction, nextArrow);
                nextArrow += arrowSpacing;
            }
            routeLength += length;
        }

        startMarker = CreateMarker("SSCA Start Marker", path[0].position, startMaterial);
        destinationMarker = CreateMarker("SSCA Destination Marker", path[path.Count - 1].position, arrowMaterial);

        SetView(startRoom.floor);
    }

    private void CreateArrow(Vector3 position, Vector3 direction, float distanceAlongRoute)
    {
        GameObject arrow = new GameObject("Cyan Route Arrow");
        arrow.transform.SetParent(transform, false);
        arrow.transform.position = position;
        arrow.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        arrow.AddComponent<MeshFilter>().sharedMesh = arrowMesh;
        MeshRenderer meshRenderer = arrow.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = arrowMaterial;

        // Arrows on a staircase belong to the floor where the stairs start.
        int floor = Mathf.Clamp(
            Mathf.FloorToInt(position.y / SSCAFloorLayouts.FloorHeight + 0.01f) + 1,
            1, SSCAFloorLayouts.FloorCount);

        arrows.Add(arrow);
        arrowRenderers.Add(meshRenderer);
        arrowDistances.Add(distanceAlongRoute);
        arrowFloors.Add(floor);
    }

    private GameObject CreateMarker(string markerName, Vector3 position, Material material)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = markerName;
        Destroy(marker.GetComponent<Collider>());
        marker.transform.SetParent(transform, false);
        marker.transform.localScale = new Vector3(0.6f, 1.3f, 0.6f);
        marker.transform.position = position + Vector3.up * 1.3f;
        marker.GetComponent<Renderer>().sharedMaterial = material;
        return marker;
    }

    private void ClearRoute()
    {
        foreach (GameObject arrow in arrows)
            if (arrow != null) Destroy(arrow);
        arrows.Clear();
        arrowRenderers.Clear();
        arrowDistances.Clear();
        arrowFloors.Clear();
        if (startMarker != null) Destroy(startMarker);
        if (destinationMarker != null) Destroy(destinationMarker);
        startMarker = null;
        destinationMarker = null;
        path = new List<SSCANavigationGraph.Waypoint>();
        routeLength = 0f;
        message = null;
    }

    private void Update()
    {
        for (int i = 0; i < arrows.Count; i++)
        {
            if (!arrows[i].activeSelf)
                continue;

            Color color = SSCAArrowMesh.Pulse(arrowDistances[i]);
            arrowColors.SetColor("_BaseColor", color);
            arrowColors.SetColor("_Color", color);
            arrowRenderers[i].SetPropertyBlock(arrowColors);
        }

        HandleCameraInput();
        UpdateCamera();
    }

    // Input System only (the project has the old Input Manager turned off).
    private void HandleCameraInput()
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            int active = 0;
            Vector2 first = Vector2.zero;
            Vector2 second = Vector2.zero;
            foreach (var touch in touchscreen.touches)
            {
                if (!touch.isInProgress) continue;
                if (active == 0) first = touch.position.ReadValue();
                else if (active == 1) second = touch.position.ReadValue();
                active++;
            }

            if (active == 1 && !IsOverGui(first))
            {
                Vector2 delta = touchscreen.primaryTouch.delta.ReadValue();
                orbit += delta.x * 0.25f;
                elevation = Mathf.Clamp(elevation - delta.y * 0.15f, 15f, 85f);
            }

            if (active == 2)
            {
                float pinch = Vector2.Distance(first, second);
                if (lastPinch > 0f)
                    distance = Mathf.Clamp(distance - (pinch - lastPinch) * 0.05f, 12f, 80f);
                lastPinch = pinch;
            }
            else
            {
                lastPinch = -1f;
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 position = mouse.position.ReadValue();
            if ((mouse.leftButton.isPressed || mouse.rightButton.isPressed) && !IsOverGui(position))
            {
                Vector2 delta = mouse.delta.ReadValue();
                orbit += delta.x * 0.3f;
                elevation = Mathf.Clamp(elevation - delta.y * 0.2f, 15f, 85f);
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !IsOverGui(position))
                distance = Mathf.Clamp(distance - scroll * 0.02f, 12f, 80f);
        }
    }

    // Screen position is from the bottom-left; GUI is from the top-left.
    private bool IsOverGui(Vector2 screenPosition)
    {
        if (menu != Menu.None)
            return true;

        float x = screenPosition.x / guiScale;
        float y = (Screen.height - screenPosition.y) / guiScale;
        float height = Screen.height / guiScale;

        return (x < 413f && y < PanelBottom) || y > height - StatusHeight;
    }

    private void UpdateCamera()
    {
        if (cam == null) return;

        Vector3 centre = viewFloor == 0
            ? new Vector3(0f, 5f, 0f)
            : new Vector3(0f, (viewFloor - 1) * SSCAFloorLayouts.FloorHeight, 0f);

        float o = orbit * Mathf.Deg2Rad;
        float e = elevation * Mathf.Deg2Rad;
        cam.transform.position = centre + new Vector3(
            Mathf.Sin(o) * Mathf.Cos(e),
            Mathf.Sin(e),
            Mathf.Cos(o) * Mathf.Cos(e)) * distance;
        cam.transform.LookAt(centre);
    }

    private void OnGUI()
    {
        if (buttonStyle == null)
        {
            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 17;
            buttonStyle.wordWrap = true;
            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 22;
            headerStyle.normal.textColor = Color.white;
            statusStyle = new GUIStyle(GUI.skin.label);
            statusStyle.fontSize = 17;
            statusStyle.wordWrap = true;
            statusStyle.normal.textColor = Color.white;
        }

        // Same 420-wide layout as the AR screen; also fits a landscape PC window.
        guiScale = Mathf.Clamp(Mathf.Min(Screen.width / 420f, Screen.height / 760f), 0.75f, 4f);
        float height = Screen.height / guiScale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(guiScale, guiScale, 1f));

        GUI.Box(new Rect(8, 8, 405, PanelBottom - 8), "");
        GUI.Label(new Rect(16, 10, 380, 28), "SSCA NAVI - 3D Dollhouse Map", headerStyle);

        for (int floor = 0; floor <= SSCAFloorLayouts.FloorCount; floor++)
        {
            // Tabs: 1F 2F 3F 4F All
            int index = floor == 0 ? SSCAFloorLayouts.FloorCount : floor - 1;
            string label = floor == 0 ? "All" : floor + "F";
            if (floor == viewFloor) label = "[" + label + "]";
            if (GUI.Button(new Rect(14 + index * 79, 44, 76, 44), label, buttonStyle))
                SetView(floor);
        }

        if (GUI.Button(new Rect(14, 94, 192, 46), "From: " + RoomLabel(startRoom, "Where are you?"), buttonStyle))
            OpenMenu(Menu.From, startRoom);
        if (GUI.Button(new Rect(212, 94, 192, 46), "To: " + RoomLabel(destinationRoom, "Select Destination"), buttonStyle))
            OpenMenu(Menu.To, destinationRoom);

        if (GUI.Button(new Rect(14, 146, 192, 44), "SHOW ROUTE", buttonStyle))
        {
            menu = Menu.None;
            ShowRoute();
        }
        if (GUI.Button(new Rect(212, 146, 192, 44), "Clear", buttonStyle))
        {
            ClearRoute();
            destinationRoom = null;
            menu = Menu.None;
        }

        if (GUI.Button(new Rect(14, 196, 390, 40), "Open AR Camera", buttonStyle))
            OpenAR();

        if (menu != Menu.None)
        {
            SSCARoomNavigation.RoomDestination room =
                SSCARoomMenu.Draw(PanelBottom + 6, roomNavigation.rooms, ref menuFloor, buttonStyle);

            if (room != null)
            {
                if (menu == Menu.From)
                    startRoom = room;
                else
                    destinationRoom = room;
                menu = Menu.None;
                ClearRoute();
                SetView(room.floor);
            }
        }

        GUI.Box(new Rect(8, height - StatusHeight, 405, StatusHeight - 8), "");
        GUI.Label(new Rect(18, height - StatusHeight + 4, 388, StatusHeight - 14), GetStatus(), statusStyle);

        GUI.matrix = Matrix4x4.identity;
    }

    private void OpenMenu(Menu which, SSCARoomNavigation.RoomDestination current)
    {
        menu = menu == which ? Menu.None : which;
        if (current != null)
            menuFloor = current.floor;
        else if (viewFloor != 0)
            menuFloor = viewFloor;
    }

    private static string RoomLabel(SSCARoomNavigation.RoomDestination room, string empty)
    {
        return room == null ? empty : room.roomName;
    }

    private string GetStatus()
    {
        if (!string.IsNullOrEmpty(message))
            return message;

        if (path.Count < 2)
            return "Choose From and To, then tap SHOW ROUTE.\nDrag to rotate the building, scroll or pinch to zoom.";

        // Floors and staircases in the order the route uses them
        List<int> floors = new List<int>();
        List<string> stairs = new List<string>();
        foreach (SSCANavigationGraph.Waypoint point in path)
        {
            if (!floors.Contains(point.floor))
                floors.Add(point.floor);

            string stair = SSCANavigationGraph.GetStairName(point.id);
            if (stair != null && !stairs.Contains(stair))
                stairs.Add(stair);
        }

        string text = Mathf.CeilToInt(routeLength) + " m from " + startRoom.roomName +
                      " (Floor " + startRoom.floor + ") to " + destinationRoom.roomName +
                      " (Floor " + destinationRoom.floor + ").";

        if (stairs.Count > 0)
            text += "\nTake the " + string.Join(", then ", stairs.ToArray()) + " stairs.";

        if (floors.Count > 1 && viewFloor != 0)
        {
            int index = floors.IndexOf(viewFloor);
            if (index >= 0 && index < floors.Count - 1)
                text += " Tap " + floors[index + 1] + "F to follow the route.";
        }

        return text;
    }

    private void OpenAR()
    {
        if (Application.CanStreamedLevelBeLoaded(arSceneName))
            SceneManager.LoadScene(arSceneName);
        else
            message = "AR scene is not in the build. In Unity: Tools > SSCA NAVI > Set Up Build Scenes.";
    }

    private void OnDestroy()
    {
        ClearRoute();
        if (arrowMaterial != null) Destroy(arrowMaterial);
        if (startMaterial != null) Destroy(startMaterial);
        if (arrowMesh != null) Destroy(arrowMesh);
    }
}
