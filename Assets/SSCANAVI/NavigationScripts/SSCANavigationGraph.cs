
using System.Collections.Generic;
using UnityEngine;

// SSCA NAVI: walking graph for Floors 1-4, generated from SSCAFloorLayouts.
// Hallway centre lines, one doorway waypoint per room, and stair waypoints
// that follow the 3D steps (foot, landing, head).
public class SSCANavigationGraph : MonoBehaviour
{
    [System.Serializable]
    public class Waypoint
    {
        public string id;
        public int floor;
        public Vector3 position;
        public List<string> connections = new List<string>();
    }

    public List<Waypoint> waypoints = new List<Waypoint>();

    private void Awake()
    {
        CreateWaypoints();

        Debug.Log(
            "SSCA NAVI: Initialized " +
            waypoints.Count +
            " waypoints.");
    }

    private static float FloorY(int floor)
    {
        return (floor - 1) * SSCAFloorLayouts.FloorHeight;
    }

    private void CreateWaypoints()
    {
        if (waypoints == null)
            waypoints = new List<Waypoint>();

        waypoints.Clear();

        for (int floor = 1; floor <= SSCAFloorLayouts.FloorCount; floor++)
        {
            // Hallway end points
            foreach (SSCAFloorLayouts.HallwayLayout hallway in SSCAFloorLayouts.GetHallways(floor))
            {
                if (!hallway.HasPath)
                    continue;

                string p = "F" + floor + "_" + SSCAFloorLayouts.ToId(hallway.Name);
                FindOrAddWaypoint(p + "_START", floor, hallway.PathStart + Vector3.up * FloorY(floor));
                FindOrAddWaypoint(p + "_END", floor, hallway.PathEnd + Vector3.up * FloorY(floor));
            }

            // Room doorways, each joined to the hallway outside its door
            foreach (SSCAFloorLayouts.RoomLayout room in SSCAFloorLayouts.GetRooms(floor))
            {
                string doorId = SSCAFloorLayouts.GetDoorWaypointId(room, floor);
                AddWaypoint(doorId, floor, room.Door + Vector3.up * FloorY(floor));
                ConnectToHallway(doorId, floor, room.DoorFacing);
            }
        }

        // Staircases: foot -> landing -> head, following the 3D steps
        foreach (SSCAFloorLayouts.StairLayout stair in SSCAFloorLayouts.GetStairs())
        {
            string previous = null;

            for (int i = 0; i < stair.Path.Length; i++)
            {
                Vector3 point = stair.Path[i];
                int floor = point.y >= 0.99f ? stair.FromFloor + 1 : stair.FromFloor;
                Vector3 position = new Vector3(
                    point.x,
                    FloorY(stair.FromFloor) + point.y * SSCAFloorLayouts.FloorHeight,
                    point.z);

                string id = "F" + floor + "_STAIRS_" + stair.Name + "_" +
                            stair.FromFloor + "TO" + (stair.FromFloor + 1) + "_" + i;
                AddWaypoint(id, floor, position);

                if (previous != null)
                    ConnectBoth(previous, id);

                // The first and last points step onto the hallways.
                if (i == 0 || i == stair.Path.Length - 1)
                    ConnectToHallway(id, floor, Vector3.zero);

                previous = id;
            }
        }

        // Join neighbouring waypoints along every hallway centre line
        for (int floor = 1; floor <= SSCAFloorLayouts.FloorCount; floor++)
            ChainHallways(floor);
    }

    // Walks straight out of the door (facing) to the first hallway centre line;
    // if there is none in front, uses the nearest hallway point instead.
    private void ConnectToHallway(string waypointId, int floor, Vector3 facing)
    {
        Waypoint from = waypoints.Find(w => w.id == waypointId);
        Vector2 point = new Vector2(from.position.x, from.position.z);
        Vector2 hallwayPoint;

        if (facing == Vector3.zero ||
            !RayToHallway(floor, point, new Vector2(facing.x, facing.z), out hallwayPoint))
        {
            hallwayPoint = NearestHallwayPoint(floor, point);
        }

        string hallwayId = FindOrAddWaypoint(
            waypointId + "_HALL",
            floor,
            new Vector3(hallwayPoint.x, FloorY(floor), hallwayPoint.y));

        ConnectBoth(waypointId, hallwayId);
    }

    private bool RayToHallway(int floor, Vector2 origin, Vector2 direction, out Vector2 hit)
    {
        hit = origin;
        float best = Mathf.Infinity;

        foreach (SSCAFloorLayouts.HallwayLayout hallway in SSCAFloorLayouts.GetHallways(floor))
        {
            if (!hallway.HasPath)
                continue;

            Vector2 a = new Vector2(hallway.PathStart.x, hallway.PathStart.z);
            Vector2 e = new Vector2(hallway.PathEnd.x, hallway.PathEnd.z) - a;
            float denominator = Cross(direction, e);
            if (Mathf.Abs(denominator) < 0.0001f)
                continue; // parallel

            Vector2 w = a - origin;
            float t = Cross(w, e) / denominator;   // distance along the ray
            float s = Cross(w, direction) / denominator; // position along the hallway

            if (t > 0.01f && s >= -0.001f && s <= 1.001f && t < best)
            {
                best = t;
                hit = origin + direction * t;
            }
        }

        return best < Mathf.Infinity;
    }

    private Vector2 NearestHallwayPoint(int floor, Vector2 point)
    {
        Vector2 nearest = point;
        float best = Mathf.Infinity;

        foreach (SSCAFloorLayouts.HallwayLayout hallway in SSCAFloorLayouts.GetHallways(floor))
        {
            if (!hallway.HasPath)
                continue;

            Vector2 candidate = ClosestPointOnSegment(
                point,
                new Vector2(hallway.PathStart.x, hallway.PathStart.z),
                new Vector2(hallway.PathEnd.x, hallway.PathEnd.z));

            float distance = Vector2.Distance(point, candidate);
            if (distance < best)
            {
                best = distance;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private void ChainHallways(int floor)
    {
        foreach (SSCAFloorLayouts.HallwayLayout hallway in SSCAFloorLayouts.GetHallways(floor))
        {
            if (!hallway.HasPath)
                continue;

            Vector2 a = new Vector2(hallway.PathStart.x, hallway.PathStart.z);
            Vector2 b = new Vector2(hallway.PathEnd.x, hallway.PathEnd.z);

            List<Waypoint> line = waypoints.FindAll(w =>
            {
                Vector2 p = new Vector2(w.position.x, w.position.z);
                return w.floor == floor &&
                       Mathf.Abs(w.position.y - FloorY(floor)) < 0.01f &&
                       Vector2.Distance(p, ClosestPointOnSegment(p, a, b)) < 0.05f;
            });

            line.Sort((first, second) =>
                Vector2.Distance(a, new Vector2(first.position.x, first.position.z))
                    .CompareTo(Vector2.Distance(a, new Vector2(second.position.x, second.position.z))));

            for (int i = 0; i < line.Count - 1; i++)
                ConnectBoth(line[i].id, line[i + 1].id);
        }
    }

    private static float Cross(Vector2 first, Vector2 second)
    {
        return first.x * second.y - first.y * second.x;
    }

    private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Vector2.Dot(point - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f);
        return a + ab * Mathf.Clamp01(t);
    }

    private string FindOrAddWaypoint(string id, int floor, Vector3 position)
    {
        Waypoint existing = waypoints.Find(
            w => w.floor == floor &&
                 Vector3.Distance(w.position, position) < 0.05f);

        if (existing != null)
            return existing.id;

        AddWaypoint(id, floor, position);
        return id;
    }

    private void ConnectBoth(string firstId, string secondId)
    {
        Waypoint first = waypoints.Find(w => w.id == firstId);
        Waypoint second = waypoints.Find(w => w.id == secondId);

        if (first == null || second == null)
        {
            Debug.LogWarning(
                "SSCA NAVI: Cannot connect " + firstId + " and " + secondId);
            return;
        }

        if (firstId == secondId)
            return;

        if (!first.connections.Contains(secondId))
            first.connections.Add(secondId);

        if (!second.connections.Contains(firstId))
            second.connections.Add(firstId);
    }

    private void AddWaypoint(
        string id,
        int floor,
        Vector3 position,
        params string[] connections)
    {
        if (waypoints.Exists(w => w.id == id))
        {
            return;
        }

        Waypoint point = new Waypoint();
        point.id = id;
        point.floor = floor;
        point.position = position;
        point.connections.AddRange(connections);
        waypoints.Add(point);
    }

    // "F2_STAIRS_NW_1TO2_3" -> "North-West"; null if the waypoint is not on stairs.
    public static string GetStairName(string waypointId)
    {
        int start = waypointId.IndexOf("STAIRS_");
        if (start < 0)
            return null;

        string name = waypointId.Substring(start + 7);
        int end = name.IndexOf('_');
        if (end >= 0)
            name = name.Substring(0, end);

        switch (name)
        {
            case "NW": return "North-West";
            case "SW": return "South-West";
            case "SE": return "South-East";
            case "W": return "West";
            default: return name;
        }
    }

    public Waypoint GetWaypoint(string id)
    {
        if (waypoints == null || waypoints.Count == 0)
            CreateWaypoints();

        return waypoints.Find(w => w.id == id);
    }

    private void OnDrawGizmos()
    {
        if (waypoints == null)
            return;

        // Draw all waypoint nodes and connections
        foreach (Waypoint point in waypoints)
        {
            Vector3 position =
                transform.TransformPoint(point.position);

            // Cyan waypoint markers
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(
                position + Vector3.up * 0.25f,
                0.25f);

            // Green hallway and staircase connections
            Gizmos.color = Color.green;

            foreach (string connectedId in point.connections)
            {
                Waypoint connected =
                    waypoints.Find(w => w.id == connectedId);

                if (connected == null)
                    continue;

                Vector3 end =
                    transform.TransformPoint(connected.position);

                Gizmos.DrawLine(
                    position + Vector3.up * 0.25f,
                    end + Vector3.up * 0.25f);
            }
        }

        // STEP 17.5 - Draw selected route in yellow
        if (currentPath != null && currentPath.Count > 0)
        {
            Gizmos.color = Color.yellow;

            for (int i = 0; i < currentPath.Count; i++)
            {
                Vector3 position =
                    transform.TransformPoint(
                        currentPath[i].position);

                position += Vector3.up * 0.4f;

                // Yellow route waypoint markers
                Gizmos.DrawSphere(position, 0.20f);

                // Yellow route connecting lines
                if (i < currentPath.Count - 1)
                {
                    Vector3 next =
                        transform.TransformPoint(
                            currentPath[i + 1].position);

                    next += Vector3.up * 0.4f;

                    Gizmos.DrawLine(position, next);
                }
            }
        }
    }

    public List<Waypoint> FindShortestPath(
        string startId,
        string destinationId)
    {
        if (waypoints == null || waypoints.Count == 0)
        {
            CreateWaypoints();
        }

        startId = startId.Trim();
        destinationId = destinationId.Trim();

        Dictionary<string, Waypoint> nodes =
            new Dictionary<string, Waypoint>();

        foreach (Waypoint node in waypoints)
            nodes[node.id] = node;

        List<Waypoint> emptyPath = new List<Waypoint>();

        if (!nodes.ContainsKey(startId) ||
            !nodes.ContainsKey(destinationId))
        {
            Debug.LogError(
                "SSCA NAVI: Invalid waypoint IDs. " +
                "Start=" + startId +
                ", Destination=" + destinationId +
                ", Available=" + string.Join(", ", nodes.Keys));

            return emptyPath;
        }

        Dictionary<string, float> distances =
            new Dictionary<string, float>();

        Dictionary<string, string> previous =
            new Dictionary<string, string>();

        HashSet<string> unvisited =
            new HashSet<string>();

        foreach (Waypoint node in waypoints)
        {
            distances[node.id] = Mathf.Infinity;
            unvisited.Add(node.id);
        }

        distances[startId] = 0f;

        while (unvisited.Count > 0)
        {
            string current = null;
            float bestDistance = Mathf.Infinity;

            foreach (string id in unvisited)
            {
                if (distances[id] < bestDistance)
                {
                    bestDistance = distances[id];
                    current = id;
                }
            }

            if (current == null || float.IsInfinity(bestDistance))
                break;

            if (current == destinationId)
                break;

            unvisited.Remove(current);

            foreach (string neighborId in
                     nodes[current].connections)
            {
                if (!nodes.ContainsKey(neighborId) ||
                    !unvisited.Contains(neighborId))
                    continue;

                float distance = Vector3.Distance(
                    nodes[current].position,
                    nodes[neighborId].position);

                float newDistance =
                    distances[current] + distance;

                if (newDistance < distances[neighborId])
                {
                    distances[neighborId] = newDistance;
                    previous[neighborId] = current;
                }
            }
        }

        if (startId != destinationId &&
            !previous.ContainsKey(destinationId))
        {
            Debug.LogWarning("No connected route found.");
            return emptyPath;
        }

        List<Waypoint> path = new List<Waypoint>();

        string step = destinationId;
        path.Add(nodes[step]);

        while (step != startId)
        {
            step = previous[step];
            path.Add(nodes[step]);
        }

        path.Reverse();
        return path;
    }

    [Header("Route Testing")]
    public string startWaypoint = "F1_ROOM_101_DOOR";
    public string destinationWaypoint = "F3_H_S_LIBRARY_DOOR";

    private List<Waypoint> currentPath = new List<Waypoint>();

    [ContextMenu("Test Shortest Path")]
    public void TestShortestPath()
    {
        currentPath = FindShortestPath(
            startWaypoint,
            destinationWaypoint);

        if (currentPath.Count == 0)
            return;

        string result = "SSCA NAVI ROUTE: ";

        foreach (Waypoint point in currentPath)
            result += point.id + " → ";

        Debug.Log(result + "DESTINATION REACHED");
    }

    public List<Waypoint> ShowRoute(
        string startId,
        string destinationId)
    {
        currentPath = FindShortestPath(
            startId, destinationId);

        if (currentPath.Count == 0)
        {
            Debug.LogWarning(
                "SSCA NAVI: Cannot display route.");
        }

        return currentPath;
    }
}
