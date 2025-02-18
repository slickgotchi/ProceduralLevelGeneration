using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Tilemaps;

public class LevelGenerator : MonoBehaviour
{
    public static LevelGenerator Instance { get; private set; }

    public int roomWidth = 38;
    public int roomHeight = 24;
    public int maxBorderWallDepth = 5;
    public float simplexSmoothness = 0.03f;
    public int minRooms = 4;
    public int maxRooms = 10;
    public int minCorridorWidth = 4;
    public int maxCorridorWidth = 7;
    public int maxCorridorOffset = 3;

    [Header("Tilemaps")]
    public Tilemap groundTilemap;
    public Tilemap treetopTilemap;
    public Tilemap treebaseTilemap;
    public Tilemap treetopBorderTilemap;
    public Tilemap treeHangLeavesTilemap;

    [Header("Tiles")]
    public RuleTile treetopRuleTile;
    public TileBase baseGroundTile;
    public TileBase treetopBorderTile;
    public RuleTile treeHangLeavesRuleTile;

    public GameObject tilePrefab;
    private int[,] map;

    private List<RectInt> m_rooms = new List<RectInt>();

    public GameObject playerPrefab;
    public GameObject player;

    public struct CorridorPair
    {
        public int fromRoomIndex;
        public int toRoomIndex;
    }

    private List<CorridorPair> m_corridorPairs = new List<CorridorPair>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Start()
    {
        player = Instantiate(playerPrefab);
        Camera.main.transform.parent = player.transform;
        GenerateLevel();
        DrawLevel();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            GenerateLevel();
            DrawLevel();
        }
    }

    void ClearLevel()
    {
        var tilePrefabs = FindObjectsByType<TilePrefab>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var tp in tilePrefabs)
        {
            Destroy(tp.gameObject);
        }

        m_rooms.Clear();
        m_corridorPairs.Clear();

        groundTilemap.ClearAllTiles();
        treetopTilemap.ClearAllTiles();
        treebaseTilemap.ClearAllTiles();
        treetopBorderTilemap.ClearAllTiles();
        treeHangLeavesTilemap.ClearAllTiles();
}

    void GenerateLevel()
    {
        ClearLevel();

        PlaceAllRooms();
        PutAllRoomsInPositiveCoordinates();
        ConvertBordersToWalls();
        ApplySimplexWalls();
        CreateCorridors();

        CleanStrandedSingleWallTopTiles();
        CleanStrandedTwinWallTopTiles();
        CleanStrandedSingleWallTopTiles();  // do this pass again after the twin stranded walls
        CleanStrandedTwinWallTopTiles();

        CleanStrandedSingleGroundTiles();

        CreateTreetopBorderWalls();

        CreateHangLeavesTilemap();

        // create treebases
        TreeBaseGenerator.Instance.Generate();

        // place player in room 0
        player.transform.position = new Vector3(
            (m_rooms[0].xMin + m_rooms[0].xMax) / 2,
            (m_rooms[0].yMin + m_rooms[0].yMax) / 2,
            0);
    }

    void PlaceAllRooms()
    {
        int totalRooms = UnityEngine.Random.Range(minRooms, maxRooms + 1);
        map = new int[roomWidth * maxRooms, roomHeight * maxRooms]; // large enough to accept the case where rooms are in a line

        // place rooms
        int safetyCounter = 0;
        for (int i = 0; i < totalRooms; i++)
        {
            if (!TryPlaceNewRoom(i))
            {
                i--;
            }

            safetyCounter++;
            if (safetyCounter > 100)
            {
                Debug.LogWarning("Had to break out of TryPlaceNewRoom() loop!");
                break;
            }
        }
    }

    void PutAllRoomsInPositiveCoordinates()
    {
        // we now need to fill all rooms BUT first we need to move all rooms into the positive axis so the map works only with positive index
        int lowestX = 0;
        int lowestY = 0;
        for (int i = 0; i < m_rooms.Count; i++)
        {
            if (m_rooms[i].xMin < lowestX) lowestX = m_rooms[i].xMin;
            if (m_rooms[i].yMin < lowestY) lowestY = m_rooms[i].yMin;
        }

        // offset all rooms by lowestX and lowestY to get into positive plane and then fill the rooms
        for (int i = 0; i < m_rooms.Count; i++)
        {
            RectInt room = new RectInt(
                m_rooms[i].xMin - lowestX,
                m_rooms[i].yMin - lowestY,
                roomWidth,
                roomHeight);

            m_rooms[i] = room;

            FillRoomWithGroundTiles(m_rooms[i]);
        }
    }

    bool TryPlaceNewRoom(int roomIndex)
    {
        if (roomIndex == 0)
        {
            RectInt firstRoom = new RectInt(0, 0, roomWidth, roomHeight);
            m_rooms.Add(firstRoom);
            return true;
        }
        else
        {
            int parentRoomIndex = UnityEngine.Random.Range(0, m_rooms.Count);
            RectInt parentRoom = m_rooms[parentRoomIndex];
            List<Vector2Int> possiblePositions = new List<Vector2Int>
            {
                new Vector2Int(parentRoom.xMin, parentRoom.yMin + roomHeight), // top
                new Vector2Int(parentRoom.xMin, parentRoom.yMin - roomHeight), // bottom
                new Vector2Int(parentRoom.xMin - roomWidth, parentRoom.yMin), // left
                new Vector2Int(parentRoom.xMin + roomWidth, parentRoom.yMin)    // right
            };

            // shuffle up the list
            ShuffleList(possiblePositions);

            foreach (var pos in possiblePositions)
            {
                RectInt newRoom = new RectInt(pos.x, pos.y, roomWidth, roomHeight);

                if (!OverlapExistingRoom(newRoom))
                {
                    m_rooms.Add(newRoom);
                    m_corridorPairs.Add(new CorridorPair { fromRoomIndex = parentRoomIndex, toRoomIndex = roomIndex });
                    return true;
                }
            }

            return false;
        }
    }

    bool OverlapExistingRoom(RectInt newRoom)
    {
        foreach (var room in m_rooms)
        {
            if (room.Overlaps(newRoom))
            {
                return true;
            }
        }
        return false;
    }

    void FillRoomWithGroundTiles(RectInt room)
    {
        for (int x = room.xMin; x < room.xMax; x++)
        {
            for (int y = room.yMin; y < room.yMax; y++)
            {
                map[x, y] = 1;
            }
        }
    }

    void ConvertBordersToWalls()
    {
        foreach (var room in m_rooms)
        {
            // Loop through all tiles inside the room
            for (int x = room.xMin; x < room.xMax; x++)
            {
                for (int y = room.yMin; y < room.yMax; y++)
                {
                    if (map[x, y] == 1) // Only check ground tiles inside room
                    {
                        // Check adjacent tiles within the room bounds
                        if (x == room.xMin || x == room.xMin + 1 || x == room.xMax - 1 || x == room.xMax - 2 ||
                            y == room.yMin || y == room.yMin + 1 || y == room.yMax - 1 || y == room.yMax -2)
                        {
                            map[x, y] = 2; // Convert to wall if it's on the room boundary
                        }
                    }
                }
            }
        }
    }

    void ApplySimplexWalls()
    {
        var roomCount = 0;

        int randOffsetX = UnityEngine.Random.Range(0, roomWidth);
        int randOffsetY = UnityEngine.Random.Range(0, roomHeight);

        foreach (var room in m_rooms)
        {
            // simplex walls along bottom
            randOffsetX = UnityEngine.Random.Range(0, roomWidth);
            for (int x = room.xMin; x < room.xMax; x++)
            {
                var simp = (noise.snoise(new float2(x * simplexSmoothness + randOffsetX, room.yMin * simplexSmoothness)) + 1) * 0.5f;
                int wallDepth = (int)(simp * maxBorderWallDepth);
                for (int y = 0; y < wallDepth; y++)
                {
                    map[x, room.yMin + y] = 2;
                }
            }

            // simplexSmoothness walls along top
            randOffsetX = UnityEngine.Random.Range(0, roomWidth);
            for (int x = room.xMin; x < room.xMax; x++)
            {
                // draw along top of room
                var simp = (noise.snoise(new float2(x * simplexSmoothness + randOffsetX, room.yMax * simplexSmoothness)) + 1) * 0.5f;
                int wallDepth = (int)(simp * maxBorderWallDepth);
                for (int y = 0; y < wallDepth; y++)
                {
                    map[x, room.yMax - y - 1] = 2;
                }
            }

            // simplexSmoothness walls along left
            randOffsetY = UnityEngine.Random.Range(0, roomHeight);
            for (int y = room.yMin; y < room.yMax; y++)
            {
                // draw along top of room
                var simp = (noise.snoise(new float2(room.xMin * simplexSmoothness, y * simplexSmoothness + randOffsetY)) + 1) * 0.5f;
                int wallDepth = (int)(simp * maxBorderWallDepth);
                for (int x = 0; x < wallDepth; x++)
                {
                    map[room.xMin + x, y] = 2;
                }
            }

            // simplexSmoothness walls along right
            randOffsetY = UnityEngine.Random.Range(0, roomHeight);
            for (int y = room.yMin; y < room.yMax; y++)
            {
                // draw along top of room
                var simp = (noise.snoise(new float2(room.xMax * simplexSmoothness, y * simplexSmoothness + randOffsetY)) + 1) * 0.5f;
                int wallDepth = (int)(simp * maxBorderWallDepth);
                for (int x = 0; x < wallDepth; x++)
                {
                    map[room.xMax - x - 1, y] = 2;
                }
            }

            roomCount++;
        }
    }

    void CreateCorridors()
    {
        foreach (var corridorPair in m_corridorPairs)
        {
            var fromRoom = m_rooms[corridorPair.fromRoomIndex];
            var toRoom = m_rooms[corridorPair.toRoomIndex];

            // create a basic 4 wide corridor from centre to centre
            Vector2Int start = new Vector2Int(fromRoom.xMin + (int)(roomWidth / 2),
                fromRoom.yMin + (int)(roomHeight / 2));
            Vector2Int finish = new Vector2Int(toRoom.xMin + (int)(roomWidth / 2),
                toRoom.yMin + (int)(roomHeight / 2));

            int corridorWidth = UnityEngine.Random.Range(minCorridorWidth, maxCorridorWidth + 1);
            int halfCorridorWidth = (int)(corridorWidth * 0.5f);
            int corridorOffset = UnityEngine.Random.Range(-maxCorridorOffset, maxCorridorOffset+1);

            // check if vertical or horizontal
            if (math.abs(finish.x - start.x) > 0.01f)
            {
                // horizontal
                for (int y = -halfCorridorWidth + corridorOffset; y < corridorWidth- halfCorridorWidth + corridorOffset; y++)
                {
                    for (int x = 0; x < roomWidth; x++)
                    {
                        int direction = finish.x > start.x ? 1 : -1;
                        map[start.x + direction * x, start.y + y] = 1;
                    }
                }
            }
            else
            {
                // vertical
                for (int x = -halfCorridorWidth + corridorOffset; x < corridorWidth- halfCorridorWidth + corridorOffset; x++)
                {
                    for (int y = 0; y < roomHeight; y++)
                    {
                        int direction = finish.y > start.y ? 1 : -1;
                        map[start.x + x, start.y + direction * y] = 1;
                    }
                }
            }
        }
    }

    void CleanStrandedSingleWallTopTiles()
    {
        for (int x = 1; x < map.GetLength(0)-1; x++)
        {
            for (int y = 1; y < map.GetLength(1)-1; y++)
            {
                TryDeleteStrandedWallTile(x, y);
            }
        }
    }

    void TryDeleteStrandedWallTile(int x, int y)
    {
        if (x <= 0 || y <= 0) return;

        if (map[x, y] == 2)
        {
            int emptySideCount = CountNeighbourTiles(x, y, 1);
            if (emptySideCount >= 3)
            {
                map[x, y] = 1;

                // find the wall tile adjacent and check that too
                if (map[x - 1, y] == 2) TryDeleteStrandedWallTile(x - 1, y);   // left
                if (map[x + 1, y] == 2) TryDeleteStrandedWallTile(x + 1, y);   // right
                if (map[x, y + 1] == 2) TryDeleteStrandedWallTile(x, y + 1);   // top
                if (map[x, y - 1] == 2) TryDeleteStrandedWallTile(x, y - 1);   // bottom
            }
        }
    }

    void CleanStrandedTwinWallTopTiles()
    {
        for (int x = 2; x < map.GetLength(0)-2; x++)
        {
            for (int y = 2; y < map.GetLength(1)-2; y++)
            {
                if (map[x-1,y] != 2 && map[x,y] == 2 && map[x+1,y] == 2 && map[x+2, y] != 2)
                {
                    if ((map[x,y-1] != 2  && map[x+1,y-1] != 2) ||
                            (map[x,y+1] != 2 && map[x+1, y+1] != 2)) {

                        map[x, y] = 1;
                        map[x + 1, y] = 1;
                    } 
                }
            }
        }

        for (int x = 2; x < map.GetLength(0) - 2; x++)
        {
            for (int y = 2; y < map.GetLength(1) - 2; y++)
            {
                if (map[x, y-1] != 2 && map[x, y] == 2 && map[x, y+1] == 2 && map[x, y+2] != 2)
                {
                    if ((map[x-1, y] != 2 && map[x - 1, y + 1] != 2) ||
                            (map[x + 1, y] != 2 && map[x + 1, y + 1] != 2))
                    {

                        map[x, y] = 1;
                        map[x + 1, y] = 1;
                    }
                }
            }
        }
    }

    void CleanStrandedSingleGroundTiles()
    {
        for (int x = 1; x < map.GetLength(0) - 1; x++)
        {
            for (int y = 1; y < map.GetLength(1) - 1; y++)
            {
                TryDeleteStrandedGroundTile(x, y);
            }
        }
    }

    void TryDeleteStrandedGroundTile(int x, int y)
    {
        if (x <= 0 || y <= 0) return;

        if (map[x, y] == 1)
        {
            int emptySideCount = CountNeighbourTiles(x, y, 2);
            if (emptySideCount >= 3)
            {
                map[x, y] = 2;

                // find the wall tile adjacent and check that too
                if (map[x - 1, y] == 1) TryDeleteStrandedGroundTile(x - 1, y);   // left
                if (map[x + 1, y] == 1) TryDeleteStrandedGroundTile(x + 1, y);   // right
                if (map[x, y + 1] == 1) TryDeleteStrandedGroundTile(x, y + 1);   // top
                if (map[x, y - 1] == 1) TryDeleteStrandedGroundTile(x, y - 1);   // bottom
            }
        }
    }

    void CreateTreetopBorderWalls()
    {
        foreach (var room in m_rooms)
        {
            // put tiles in corners
            treetopBorderTilemap.SetTile(new Vector3Int(room.xMin, room.yMin, 0), treetopBorderTile);
            treetopBorderTilemap.SetTile(new Vector3Int(room.xMax-1, room.yMin, 0), treetopBorderTile);
            treetopBorderTilemap.SetTile(new Vector3Int(room.xMin, room.yMax-1, 0), treetopBorderTile);
            treetopBorderTilemap.SetTile(new Vector3Int(room.xMax-1, room.yMax-1, 0), treetopBorderTile);

            if (!IsRoomAbove(room))
            {
                for (int x = room.xMin; x < room.xMax-1; x++)
                {
                    treetopBorderTilemap.SetTile(new Vector3Int(x, room.yMax - 1, 0), treetopBorderTile);
                }
            }
            if (!IsRoomBelow(room))
            {
                for (int x = room.xMin; x < room.xMax - 1; x++)
                {
                    treetopBorderTilemap.SetTile(new Vector3Int(x, room.yMin, 0), treetopBorderTile);
                }
            }
            if (!IsRoomOnLeft(room))
            {
                for (int y = room.yMin; y < room.yMax - 1; y++)
                {
                    treetopBorderTilemap.SetTile(new Vector3Int(room.xMin, y, 0), treetopBorderTile);
                }
            }
            if (!IsRoomOnRight(room))
            {
                for (int y = room.yMin; y < room.yMax - 1; y++)
                {
                    treetopBorderTilemap.SetTile(new Vector3Int(room.xMax-1, y, 0), treetopBorderTile);
                }
            }
        }
    }

    void CreateHangLeavesTilemap()
    {
        foreach (var room in m_rooms)
        {
            for (int x = room.xMin; x < room.xMax; x++)
            {
                for (int y = room.yMin+1; y < room.yMax; y++)
                {
                    if (map[x,y] == 2)
                    {
                        treeHangLeavesTilemap.SetTile(new Vector3Int(x, y - 1, 0), treeHangLeavesRuleTile);
                    }
                }
            }
        }
    }

    bool IsRoomAbove(RectInt currentRoom)
    {
        foreach (var testRoom in m_rooms)
        {
            if (currentRoom.yMax == testRoom.yMin && currentRoom.xMin == testRoom.xMin)
            {
                return true;
            }
        }
        return false;
    }

    bool IsRoomBelow(RectInt currentRoom)
    {
        foreach (var testRoom in m_rooms)
        {
            if (currentRoom.yMin == testRoom.yMax && currentRoom.xMin == testRoom.xMin)
            {
                return true;
            }
        }

        return false;
    }

    bool IsRoomOnLeft(RectInt currentRoom)
    {
        foreach (var testRoom in m_rooms)
        {
            if (currentRoom.yMin == testRoom.yMin && currentRoom.xMin == testRoom.xMax)
            {
                return true;
            }
        }

        return false;
    }

    bool IsRoomOnRight(RectInt currentRoom)
    {
        foreach (var testRoom in m_rooms)
        {
            if (currentRoom.yMin == testRoom.yMin && currentRoom.xMax == testRoom.xMin)
            {
                return true;
            }
        }

        return false;
    }

    int CountNeighbourTiles(int x, int y, int neighbourTileType)
    {
        int sideCount = 0;
        if (map[x - 1, y] == neighbourTileType) sideCount++;   // left
        if (map[x + 1, y] == neighbourTileType) sideCount++;   // right
        if (map[x, y + 1] == neighbourTileType) sideCount++;   // top
        if (map[x, y - 1] == neighbourTileType) sideCount++;   // bottom

        return sideCount;
    }

    void DrawLevel()
    {
        // clear tree tops
        treetopTilemap.ClearAllTiles();

        for (int x = 0; x < map.GetLength(0); x++)
        {
            for (int y = 0; y < map.GetLength(1); y++)
            {
                if (map[x, y] != 0)
                {
                    // treat this as a void and don't install anything
                    groundTilemap.SetTile(new Vector3Int(x, y, 0), baseGroundTile);
                }

                if (map[x, y] == 2)
                {
                    treetopTilemap.SetTile(new Vector3Int(x, y, 0), treetopRuleTile);
                }
            }
        }
    }

    void SetTreetopTileBase(int x, int y)
    {

    }

    void ShuffleList<T>(List<T> list)
    {
        for (int i= 0; i < list.Count; i++)
        {
            int randIndex = UnityEngine.Random.Range(i, list.Count);
            (list[i], list[randIndex]) = (list[randIndex], list[i]);
        }
    }

    public bool IsInRoomBounds(int x, int y)
    {
        foreach (var room in m_rooms)
        {
            if (x >= room.xMin && x < room.xMax && y >= room.yMin && y < room.yMax)
            {
                return true;
            }
        }

        return false;
    }
}
