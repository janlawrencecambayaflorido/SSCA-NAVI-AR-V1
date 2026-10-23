using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

// SSCA NAVI: AR camera UI + cyan pathway arrows along the SSCA navigation graph.
// QR-free calibration: the student stands in the doorway of the starting room,
// back to the room and facing the hallway, then taps START. The route from the
// navigation graph is placed in the AR world relative to that position and heading.
// Room positions still come from the provisional (not to scale) floor layouts.
public class SSCAARGuide : MonoBehaviour
{
    [Header("AR Camera")]
    public Camera arCamera;

    [Header("Navigation Data (created automatically if empty)")]
    public SSCANavigationGraph navigationGraph;
    public SSCARoomNavigation roomNavigation;

    [Header("Cyan Pathway")]
    public float arrowSpacing = 0.8f;
    public float arrowHeight = 0.05f;
    public float visibleDistance = 12f;
    [Tooltip("Phone height above the floor, used when no AR floor plane is detected.")]
    public float phoneHeight = 1.3f;
    public float arrivalDistance = 1.5f;

    [Header("3D Dollhouse Map")]
    public string mapSceneName = "SSCA_NAVI_3D_Demo";

    private readonly List<GameObject> arrows = new List<GameObject>();
    private readonly List<Renderer> arrowRenderers = new List<Renderer>();
    private readonly List<float> arrowDistances = new List<float>(); // metres along the route
    private readonly List<int> arrowSegments = new List<int>();      // path index the arrow belongs to
    private List<SSCANavigationGraph.Waypoint> path = new List<SSCANavigationGraph.Waypoint>();
    private GameObject destinationMarker;
    private Vector3 destinationWorld;
    private float routeLength;
    private float progress;
    private int currentSegment;
    private bool arrived;
    private string message;

    // Graph space -> AR world space, set by Calibrate()
    private Vector3 worldAnchor;
    private Vector3 graphAnchor;
    private Quaternion graphToWorld = Quaternion.identity;

    private SSCARoomNavigation.RoomDestination startRoom;
    private SSCARoomNavigation.RoomDestination destinationRoom;

    private enum Menu { None, From, To }
    private Menu menu = Menu.None;
    private int menuFloor = 1;

    private Material arrowMaterial;
    private Mesh arrowMesh;
    private MaterialPropertyBlock arrowColors;
    private GUIStyle buttonStyle;
    private GUIStyle headerStyle;
    private GUIStyle statusStyle;

    private void Awake()
    {
        XRManagerSettings xrManager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (xrManager != null && xrManager.activeLoader == null)
        {
            xrManager.InitializeLoaderSync();
            if (xrManager.activeLoader != null)
                xrManager.StartSubsystems();
        }

        bool xrReady = xrManager != null && xrManager.activeLoader != null;

        if (xrReady)
        {
            ARSession arSession = Object.FindFirstObjectByType<ARSession>();
            if (arSession != null)
                arSession.enabled = true;

            ARCameraManager arCameraManager = Object.FindFirstObjectByType<ARCameraManager>();
            if (arCameraManager != null)
                arCameraManager.enabled = true;

            ARCameraBackground arCameraBackground = Object.FindFirstObjectByType<ARCameraBackground>();
            if (arCameraBackground != null)
                arCameraBackground.enabled = true;
        }

        if (arCamera == null)
        {
            arCamera = Camera.main;
            if (arCamera == null)
            {
                GameObject cameraGo = GameObject.FindGameObjectWithTag("MainCamera");
                if (cameraGo != null)
                    arCamera = cameraGo.GetComponent<Camera>();
            }
        }

        if (arCamera == null)
        {
            GameObject fallbackCamera = new GameObject("SSCA Fallback Camera");
            fallbackCamera.tag = "MainCamera";
            arCamera = fallbackCamera.AddComponent<Camera>();
            arCamera.clearFlags = CameraClearFlags.SolidColor;
            arCamera.backgroundColor = Color.black;
            arCamera.nearClipPlane = 0.05f;
            arCamera.transform.position = new Vector3(0f, 1.5f, 0f);
        }

        arrowMaterial = SSCAArrowMesh.CreateMaterial(SSCAArrowMesh.BrightCyan);
        arrowMesh = SSCAArrowMesh.Create();
        arrowColors = new MaterialPropertyBlock();
    }

    private void Start()
    {
        EnsureNavigationData();
    }

    // The AR scene does not need the graph objects; create them if missing.
    private void EnsureNavigationData()
    {
        if (navigationGraph == null)
            navigationGraph = Object.FindFirstObjectByType<SSCANavigationGraph>();
        if (roomNavigation == null)
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
            statusStyle.fontSize = 18;
            statusStyle.wordWrap = true;
            statusStyle.normal.textColor = Color.white;
        }
        float scale = Mathf.Max(0.6f, Screen.width / 420f);
        float height = Screen.height / scale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        GUI.Box(new Rect(8, 8, 405, 140), "");
        GUI.Label(new Rect(16, 10, 230, 28), "SSCA NAVI", headerStyle);
        if (GUI.Button(new Rect(300, 10, 104, 28), "3D Map"))
            OpenMap();
        if (GUI.Button(new Rect(14, 42, 192, 46), "From: " + RoomLabel(startRoom, "Where are you?"), buttonStyle))
            OpenMenu(Menu.From, startRoom);
        if (GUI.Button(new Rect(212, 42, 192, 46), "To: " + RoomLabel(destinationRoom, "Select Destination"), buttonStyle))
            OpenMenu(Menu.To, destinationRoom);
        if (GUI.Button(new Rect(14, 94, 192, 46), "START", buttonStyle))
        {
            menu = Menu.None;
            StartNavigation();
        }
        if (GUI.Button(new Rect(212, 94, 192, 46), "Clear", buttonStyle))
        {
            ClearNavigation();
            destinationRoom = null;
            menu = Menu.None;
        }

        if (menu != Menu.None)
            DrawRoomMenu();

        GUI.Box(new Rect(8, height - 104, 405, 96), "");
        GUI.Label(new Rect(18, height - 100, 388, 90), GetStatus(), statusStyle);

        GUI.matrix = Matrix4x4.identity;
    }

    private void OpenMenu(Menu which, SSCARoomNavigation.RoomDestination current)
    {
        menu = menu == which ? Menu.None : which;
        if (current != null)
            menuFloor = current.floor;
    }

    // Floor tabs + the rooms on that floor (fits a phone screen without scrolling).
    private void DrawRoomMenu()
    {
        if (roomNavigation == null)
            return;

        SSCARoomNavigation.RoomDestination room =
            SSCARoomMenu.Draw(154, roomNavigation.rooms, ref menuFloor, buttonStyle);

        if (room != null)
        {
            if (menu == Menu.From)
                startRoom = room;
            else
                destinationRoom = room;
            menu = Menu.None;
            ClearNavigation();
        }
    }

    private void OpenMap()
    {
        if (Application.CanStreamedLevelBeLoaded(mapSceneName))
            SceneManager.LoadScene(mapSceneName);
        else
            message = "3D Map is not in the build. In Unity: Tools > SSCA NAVI > Set Up Build Scenes.";
    }

    private static string RoomLabel(SSCARoomNavigation.RoomDestination room, string empty)
    {
        return room == null ? empty : room.roomName;
    }

    private string GetStatus()
    {
        if (!string.IsNullOrEmpty(message))
            return message;

        if (startRoom == null || destinationRoom == null)
            return "Choose where you are (From) and where you want to go (To).";

        if (arrows.Count == 0)
            return "Stand in the doorway of " + startRoom.roomName +
                   " with your back to the room, facing the hallway. Then tap START.";

        if (arrived)
            return "You have arrived at " + destinationRoom.roomName + "!";

        int remaining = Mathf.CeilToInt(Mathf.Max(0f, routeLength - progress));
        SSCANavigationGraph.Waypoint here = path[currentSegment];
        SSCANavigationGraph.Waypoint next = path[currentSegment + 1];

        // Find where the route next changes floor (the end of the next staircase).
        int floorChange = -1;
        for (int k = currentSegment + 1; k < path.Count; k++)
        {
            if (path[k].floor != here.floor)
            {
                floorChange = k;
                break;
            }
        }

        if (floorChange >= 0)
        {
            SSCANavigationGraph.Waypoint arrival = path[floorChange];
            string direction = arrival.floor > here.floor ? "UP" : "DOWN";
            bool onStairs = Mathf.Abs(next.position.y - here.position.y) > 0.01f;

            if (onStairs)
                return "Go " + direction + " the stairs to Floor " + arrival.floor + ".\n" +
                       remaining + " m left";

            return "Follow the cyan arrows to the " +
                   (SSCANavigationGraph.GetStairName(arrival.id) ?? "nearest") + " stairs, then go " +
                   direction.ToLower() + " to Floor " + destinationRoom.floor + ".\n" + remaining + " m left";
        }

        return "Follow the cyan arrows to " + destinationRoom.roomName +
               " (Floor " + destinationRoom.floor + ").\n" + remaining + " m left";
    }

    private void StartNavigation()
    {
        ClearNavigation();

        if (arCamera == null || arrowMaterial == null)
        {
            Debug.LogWarning("SSCA AR: no usable camera/shader available.");
            return;
        }

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
        if (path.Count < 2)
        {
            message = "No route found from " + startRoom.roomName + " to " + destinationRoom.roomName + ".";
            return;
        }

        Calibrate();
        BuildPathway();
        Debug.Log("SSCA AR route: " + startRoom.roomName + " -> " + destinationRoom.roomName +
                  " (" + path.Count + " waypoints, " + routeLength.ToString("0.0") + " m, " +
                  arrows.Count + " arrows)");
    }

    // Maps the start doorway in the graph onto the phone's current position,
    // and the doorway's hallway direction onto the phone's current heading.
    private void Calibrate()
    {
        Vector3 cameraPosition = arCamera.transform.position;
        worldAnchor = new Vector3(cameraPosition.x, FindFloorHeight(cameraPosition), cameraPosition.z);

        Vector3 facing = arCamera.transform.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.04f)
        {
            // Phone pointed straight at the floor: its top edge points forward.
            facing = arCamera.transform.up;
            facing.y = 0f;
        }
        if (facing.sqrMagnitude < 0.0001f)
            facing = Vector3.forward;
        facing.Normalize();

        float yaw = Vector3.SignedAngle(startRoom.hallwayDirection, facing, Vector3.up);
        graphToWorld = Quaternion.AngleAxis(yaw, Vector3.up);
        graphAnchor = navigationGraph.GetWaypoint(startRoom.waypointId).position;
    }

    private float FindFloorHeight(Vector3 cameraPosition)
    {
        // Uses a detected AR floor plane when an ARRaycastManager is on the XR Origin.
        ARRaycastManager raycastManager = Object.FindFirstObjectByType<ARRaycastManager>();
        if (raycastManager != null)
        {
            List<ARRaycastHit> hits = new List<ARRaycastHit>();
            if (raycastManager.Raycast(new Ray(cameraPosition, Vector3.down), hits, TrackableType.PlaneWithinPolygon))
                return hits[0].pose.position.y;
        }
        return cameraPosition.y - phoneHeight;
    }

    private Vector3 ToWorld(Vector3 graphPosition)
    {
        return worldAnchor + graphToWorld * (graphPosition - graphAnchor);
    }

    private void BuildPathway()
    {
        routeLength = 0f;
        float nextArrow = arrowSpacing * 0.5f;

        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 from = ToWorld(path[i].position) + Vector3.up * arrowHeight;
            Vector3 to = ToWorld(path[i + 1].position) + Vector3.up * arrowHeight;
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.01f) continue;
            Vector3 direction = delta / length;

            // Arrows keep even spacing across corners; on stairs they tilt with the slope.
            while (nextArrow < routeLength + length)
            {
                CreateArrow(from + direction * (nextArrow - routeLength), direction);
                arrowDistances.Add(nextArrow);
                arrowSegments.Add(i);
                nextArrow += arrowSpacing;
            }
            routeLength += length;
        }

        destinationWorld = ToWorld(path[path.Count - 1].position);
        CreateDestinationMarker();
        progress = 0f;
        currentSegment = 0;
        arrived = false;
    }

    private void Update()
    {
        if (arrows.Count == 0 || arCamera == null)
            return;

        Vector3 cameraPosition = arCamera.transform.position;
        float feetY = cameraPosition.y - phoneHeight;

        // Progress = the arrow closest to the student (same floor weighs more).
        int nearest = 0;
        float best = float.MaxValue;
        for (int i = 0; i < arrows.Count; i++)
        {
            Vector3 offset = arrows[i].transform.position - cameraPosition;
            float score = new Vector2(offset.x, offset.z).magnitude +
                          Mathf.Abs(arrows[i].transform.position.y - feetY) * 2f;
            if (score < best)
            {
                best = score;
                nearest = i;
            }
        }
        progress = arrowDistances[nearest];
        currentSegment = arrowSegments[nearest];

        Vector3 toDestination = destinationWorld - cameraPosition;
        arrived = new Vector2(toDestination.x, toDestination.z).magnitude < arrivalDistance &&
                  Mathf.Abs(destinationWorld.y - feetY) < 1.5f;

        for (int i = 0; i < arrows.Count; i++)
        {
            Vector3 position = arrows[i].transform.position;
            Vector3 offset = position - cameraPosition;
            bool show = !arrived &&
                        arrowDistances[i] >= progress - 1f &&
                        new Vector2(offset.x, offset.z).magnitude < visibleDistance &&
                        Mathf.Abs(position.y - feetY) < 2.5f;

            if (arrows[i].activeSelf != show)
                arrows[i].SetActive(show);

            if (show)
            {
                // A bright pulse travels along the path in the walking direction.
                Color color = SSCAArrowMesh.Pulse(arrowDistances[i]);
                arrowColors.SetColor("_BaseColor", color);
                arrowColors.SetColor("_Color", color);
                arrowRenderers[i].SetPropertyBlock(arrowColors);
            }
        }

        if (destinationMarker != null)
            destinationMarker.transform.position =
                destinationWorld + Vector3.up * (1f + Mathf.Sin(Time.time * 2f) * 0.1f);
    }

    private void CreateArrow(Vector3 location, Vector3 direction)
    {
        GameObject arrow = new GameObject("Cyan AR Arrow");
        arrow.transform.position = location;
        arrow.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        arrow.AddComponent<MeshFilter>().sharedMesh = arrowMesh;
        MeshRenderer meshRenderer = arrow.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = arrowMaterial;
        arrows.Add(arrow);
        arrowRenderers.Add(meshRenderer);
    }

    private void CreateDestinationMarker()
    {
        destinationMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        destinationMarker.name = "SSCA Destination Marker";
        Destroy(destinationMarker.GetComponent<Collider>());
        destinationMarker.transform.localScale = new Vector3(0.35f, 1f, 0.35f);
        destinationMarker.transform.position = destinationWorld + Vector3.up;
        destinationMarker.GetComponent<Renderer>().sharedMaterial = arrowMaterial;
    }

    public void ClearNavigation()
    {
        foreach (GameObject arrow in arrows)
            if (arrow != null) Destroy(arrow);
        arrows.Clear();
        arrowRenderers.Clear();
        arrowDistances.Clear();
        arrowSegments.Clear();
        path = new List<SSCANavigationGraph.Waypoint>();
        if (destinationMarker != null) Destroy(destinationMarker);
        destinationMarker = null;
        arrived = false;
        message = null;
    }

    private void OnDestroy()
    {
        XRManagerSettings xrManager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (xrManager != null && xrManager.activeLoader != null)
        {
            xrManager.StopSubsystems();
            xrManager.DeinitializeLoader();
        }

        ClearNavigation();
        if (arrowMaterial != null) Destroy(arrowMaterial);
        if (arrowMesh != null) Destroy(arrowMesh);
    }
}
