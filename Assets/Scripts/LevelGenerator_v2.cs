using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class LevelGenerator_v2 : MonoBehaviour
{
    public static LevelGenerator_v2 Instance { get; private set; }

    // screen dimensions:
    //  width  = 27 GU
    //  height = 17 GU
    [Header("Level Dimensions")]
    public int minLevelWidth = 27 * 4;
    public int maxLevelWidth = 27 * 6;
    public int minLevelHeight = 17 * 4;
    public int maxLevelHeight = 17 * 6;
    public int targetNumberRooms = 7;

    [Header("Tilemaps to Populate")]
    public Tilemap baseWorking_Tilemap;
    public Tilemap ground_Tilemap;
    public Tilemap topLeaf_Tilemap;
    public Tilemap hangLeaf_Tilemap;
    public Tilemap treeTrunk_Tilemap;
    public Tilemap border_Tilemap;

    [Header("Tiles")]
    public TileBase floorTile;
    public TileBase doorTile;
    public TileBase border_Tile;
    public RuleTile treeTrunk_RuleTile;
    public RuleTile topLeaf_RuleTile;
    public RuleTile hangLeaf_RuleTile;

    [Header("Level Gen Seed")]
    public int masterSeed = 123;

    // working params 
    private int m_levelWidth;
    private int m_levelHeight;

    // the map represents our room with int codings:
    //  0 = void
    //  1 = floor
    //  2 = door
    private int[,] m_map;

    private List<Room> m_rooms = new List<Room>();

    public enum GridType
    {
        Void, Floor, Wall
    }

    public enum RoomType
    {
        OverlappingRectangles, CellularAutomata, OverlappingEllipses, SingleRectangle, SingleEllipse
    }

    public enum Direction { Null, Left, Right, Up, Down }

    public class Room
    {
        public RectInt rectInt;
        public int[,] map;
        public Room parent; // Reference to the parent Room (can be null if no parent)
        public Direction directionToParent;

        public Room()
        {
            rectInt = new RectInt();
            map = null; // Initialize as null; you’ll set it later
            parent = null; // No parent by default
            directionToParent = Direction.Null;
        }
    }

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

    private void Start()
    {
        Random.InitState(masterSeed);

        GenerateLevel();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            GenerateLevel();
        }
    }

    void GenerateLevel()
    {
        ClearExistingLevel();

        // determine level dimensions
        m_levelWidth = Random.Range(minLevelWidth, maxLevelWidth + 1);
        m_levelHeight = Random.Range(minLevelHeight, maxLevelHeight + 1);

        Debug.Log("m_levelWidth: " + m_levelWidth + ", m_levelHeight: " + m_levelHeight);

        m_map = new int[m_levelWidth, m_levelHeight];
        for (int x = 0; x < m_levelWidth; x++)
        {
            for (int y = 0; y < m_levelHeight; y++)
            {
                m_map[x, y] = 0; // Fill with voids
            }
        }

        int roomEdgeBuffer = 2;
        int levelEdgeBuffer = 4;

        // place rooms
        GenerateRooms(roomEdgeBuffer, levelEdgeBuffer);

        BuildCorridors(roomEdgeBuffer);

        PrepForWallTilemapping(levelEdgeBuffer);
        PrepForWallTilemapping(levelEdgeBuffer);

        // render the map
        RenderMap();
    }

    public enum IterationPattern { LeftRight_UpDown, LeftRight_DownUp, RightLeft_UpDown, RightLeft_DownUp }

    int [,] GenerateRoomMap(RoomType roomType, int roomEdgeBuffer)
    {
        // SINGLE RECTANGLE
        if (roomType == RoomType.SingleRectangle)
        {
            var maxRoomWidth = Random.Range(13, 27) + roomEdgeBuffer * 2;
            var maxRoomHeight = Random.Range(8, 17) + roomEdgeBuffer * 2;
            var tempRoom = new int[maxRoomWidth, maxRoomHeight];

            for (int x = 0; x < maxRoomWidth; x++)
            {
                for (int y = 0; y < maxRoomHeight; y++)
                {
                    if (x < 2 || x > maxRoomWidth - 3 || y < 2 || y > maxRoomHeight - 3)
                    {
                        tempRoom[x, y] = 2;
                    }
                    else
                    {
                        tempRoom[x, y] = 1;
                    }
                }
            }

            return tempRoom;
        }

        // OVERLAPPING RECTANGLES
        if (roomType == RoomType.OverlappingRectangles)
        {
            var minWidth = 15;
            var maxWidth = 30;
            var minHeight = 10;
            var maxHeight = 20;

            // Use the provided width and height ranges as a guideline for the overall room size
            var roomWidth = Random.Range(minWidth, maxWidth + 1) + roomEdgeBuffer * 2;
            var roomHeight = Random.Range(minHeight, maxHeight + 1) + roomEdgeBuffer * 2;
            var tempRoom = new int[roomWidth, roomHeight];

            // Generate two random rectangles that may overlap
            // Rectangle 1: Random size and position within bounds, ensuring it fits with buffer
            int rect1Width = Random.Range((int)(roomWidth * 0.5f + 1), (int)(roomWidth)); ; // Brogue uses 4–25 width
            int rect1Height = Random.Range((int)(roomHeight * 0.5f + 1), (int)(roomHeight*0.5f)); // Brogue uses 2–7 height
            int rect1X = Random.Range(roomEdgeBuffer, roomWidth - rect1Width - roomEdgeBuffer);
            int rect1Y = Random.Range(roomEdgeBuffer, roomHeight - rect1Height - roomEdgeBuffer);

            // Rectangle 2: Random size and position, potentially overlapping Rectangle 1
            int rect2Width = Random.Range((int)(roomWidth * 0.5f + 1), (int)(roomWidth*0.5f));
            int rect2Height = Random.Range((int)(roomHeight * 0.5f + 1), (int)(roomHeight)); ;
            int rect2X = Random.Range(roomEdgeBuffer, roomWidth - rect2Width - roomEdgeBuffer);
            int rect2Y = Random.Range(roomEdgeBuffer, roomHeight - rect2Height - roomEdgeBuffer);

            // Fill both rectangles with floor tiles (overlapping areas will naturally be floor)
            for (int x = 0; x < roomWidth; x++)
            {
                for (int y = 0; y < roomHeight; y++)
                {
                    if ((x >= rect1X && x < rect1X + rect1Width && y >= rect1Y && y < rect1Y + rect1Height) ||
                        (x >= rect2X && x < rect2X + rect2Width && y >= rect2Y && y < rect2Y + rect2Height))
                    {
                        if (tempRoom[x, y] != (int)GridType.Wall) // Don’t overwrite walls
                            tempRoom[x, y] = (int)GridType.Floor;
                    }
                }
            }

            // Resize the room to fit the bounds of the overlapped rectangles, maintaining edge buffer
            tempRoom = ResizeRoomToFloorBounds(tempRoom, roomEdgeBuffer);
            tempRoom = FillVoidsWithWalls(tempRoom);

            return tempRoom;
        }

        // CELLULAR AUTOMATA
        if (roomType == RoomType.CellularAutomata)
        {
            var maxRoomWidth = Random.Range(18, 36) + roomEdgeBuffer * 2;
            var maxRoomHeight = Random.Range(12, 25) + roomEdgeBuffer * 2;
            var tempRoom = new int[maxRoomWidth, maxRoomHeight];

            for (int x = 0; x < maxRoomWidth; x++)
            {
                for (int y = 0; y < maxRoomHeight; y++)
                {
                    if (x < 2 || x > maxRoomWidth - 3 || y < 2 || y > maxRoomHeight - 3)
                    {
                        tempRoom[x, y] = 2;
                    }
                    else
                    {
                        //var perlinNoise = Mathf.PerlinNoise(x, y);
                        tempRoom[x, y] = Random.value > 0.42f ? 1 : 0;
                    }
                }
            }

            // perform 5 smoothing passes
            for (int i = 0; i < 5; i++)
            {
                for (int x = 0; x < maxRoomWidth; x++)
                {
                    for (int y = 0; y < maxRoomHeight; y++)
                    {
                        if (x < 2 || x > maxRoomWidth - 3 || y < 2 || y > maxRoomHeight - 3)
                        {
                            // do nothing
                        }
                        else
                        {
                            if (tempRoom[x, y] == (int)GridType.Floor)
                            {
                                var floorGridNeighbours = CountGridNeighbours(tempRoom, GridType.Floor, x, y);
                                if (floorGridNeighbours < 3)
                                {
                                    tempRoom[x, y] = (int)GridType.Void;
                                }
                            }
                            else if (tempRoom[x, y] == (int)GridType.Void)
                            {
                                if (CountGridNeighbours(tempRoom, GridType.Floor, x, y) >= 5)
                                {
                                    tempRoom[x, y] = (int)GridType.Floor;
                                }
                            }
                        }
                    }
                }
            }

            tempRoom = TrimIsolatedEdgeFloorTiles(tempRoom, roomEdgeBuffer);
            tempRoom = KeepLargestContinuousFloorSection(tempRoom);
            tempRoom = ResizeRoomToFloorBounds(tempRoom, roomEdgeBuffer);
            tempRoom = FillVoidsWithWalls(tempRoom);

            // Add new function to fill isolated wall islands with floor tiles
            //tempRoom = FillIsolatedWallIslands(tempRoom, roomEdgeBuffer);

            return tempRoom;
        }

        // SINGLE CIRCLE
        if (roomType == RoomType.SingleEllipse)
        {
            var roomWidth = Random.Range(15, 30) + roomEdgeBuffer * 2;
            var roomHeight = Random.Range(10, 20) + roomEdgeBuffer * 2;
            var tempRoom = new int[roomWidth, roomHeight];

            // Initialize with walls for the edge buffer and voids for the interior
            for (int x = 0; x < roomWidth; x++)
            {
                for (int y = 0; y < roomHeight; y++)
                {
                    if (x < roomEdgeBuffer || x > roomWidth - 1 - roomEdgeBuffer ||
                        y < roomEdgeBuffer || y > roomHeight - 1 - roomEdgeBuffer)
                    {
                        tempRoom[x, y] = (int)GridType.Wall;
                    }
                    else
                    {
                        tempRoom[x, y] = (int)GridType.Void;
                    }
                }
            }

            // Calculate the center of the room, ensuring it's within the interior (after buffer)
            int centerX = roomWidth / 2;
            int centerY = roomHeight / 2;

            // Calculate the radius/axes to fit snugly against the edge buffers
            // Use the smaller of (width - buffer*2)/2 or (height - buffer*2)/2 to ensure the circle fits
            int innerWidth = roomWidth - (roomEdgeBuffer * 2);  // Width inside buffers
            int innerHeight = roomHeight - (roomEdgeBuffer * 2); // Height inside buffers
            float radiusX = (innerWidth - 1) / 2.0f;  // Half the inner width, minus 1 for snug fit
            float radiusY = (innerHeight - 1) / 2.0f; // Half the inner height, minus 1 for snug fit

            // Fill the room with a circle/ellipse that touches the inner edges of the buffer
            for (int x = 0; x < roomWidth; x++)
            {
                for (int y = 0; y < roomHeight; y++)
                {
                    if (x < roomEdgeBuffer || x > roomWidth - 1 - roomEdgeBuffer ||
                        y < roomEdgeBuffer || y > roomHeight - 1 - roomEdgeBuffer)
                    {
                        // Keep walls as walls
                    }
                    else
                    {
                        // Use ellipse equation: (x - centerX)^2 / radiusX^2 + (y - centerY)^2 / radiusY^2 <= 1
                        float dx = (x - centerX) / radiusX;
                        float dy = (y - centerY) / radiusY;
                        if (dx * dx + dy * dy <= 1.0f)
                        {
                            tempRoom[x, y] = (int)GridType.Floor;
                        }
                    }
                }
            }

            tempRoom = TrimIsolatedEdgeFloorTiles(tempRoom, roomEdgeBuffer);
            tempRoom = ResizeRoomToFloorBounds(tempRoom, roomEdgeBuffer);
            tempRoom = FillVoidsWithWalls(tempRoom);

            return tempRoom;
        }

        // OVERLAPPING CIRCLES
        if (roomType == RoomType.OverlappingEllipses)
        {
            var roomWidth = Random.Range(15, 30) + roomEdgeBuffer * 2;
            var roomHeight = Random.Range(10, 20) + roomEdgeBuffer * 2;
            var tempRoom = new int[roomWidth, roomHeight];

            // Initialize with walls for the edge buffer and voids for the interior
            for (int x = 0; x < roomWidth; x++)
            {
                for (int y = 0; y < roomHeight; y++)
                {
                    if (x < roomEdgeBuffer || x > roomWidth - 1 - roomEdgeBuffer ||
                        y < roomEdgeBuffer || y > roomHeight - 1 - roomEdgeBuffer)
                    {
                        tempRoom[x, y] = (int)GridType.Wall;
                    }
                    else
                    {
                        tempRoom[x, y] = (int)GridType.Void;
                    }
                }
            }


            int innerHeight = roomHeight - (roomEdgeBuffer * 2); // Height inside buffers
            int innerWidth = roomWidth - (roomEdgeBuffer * 2);  // Width inside buffers

            var numCircles = Random.Range(2, 3 + 1);
            for (int i = 0; i < numCircles; i++)
            {
                float radiusX = (innerWidth - 1) * 0.5f * Random.Range(0.5f, 1.0f);
                float radiusY = (innerHeight - 1) * 0.5f * Random.Range(0.5f, 1.0f);

                int centerX = (int)Random.Range(roomEdgeBuffer + radiusX, roomWidth - (roomEdgeBuffer + radiusX));
                int centerY = (int)Random.Range(roomEdgeBuffer + radiusY, roomHeight - (roomEdgeBuffer + radiusY));

                for (int x = 0; x < roomWidth; x++)
                {
                    for (int y = 0; y < roomHeight; y++)
                    {
                        if (x < roomEdgeBuffer || x > roomWidth - 1 - roomEdgeBuffer ||
                            y < roomEdgeBuffer || y > roomHeight - 1 - roomEdgeBuffer)
                        {
                            // Keep walls as walls
                        }
                        else
                        {
                            // Use ellipse equation: (x - centerX)^2 / radiusX^2 + (y - centerY)^2 / radiusY^2 <= 1
                            float dx = (x - centerX) / radiusX;
                            float dy = (y - centerY) / radiusY;
                            if (dx * dx + dy * dy <= 1.0f)
                            {
                                tempRoom[x, y] = (int)GridType.Floor;
                            }
                        }
                    }
                }
            }

            tempRoom = TrimIsolatedEdgeFloorTiles(tempRoom, roomEdgeBuffer);
            tempRoom = KeepLargestContinuousFloorSection(tempRoom);
            tempRoom = ResizeRoomToFloorBounds(tempRoom, roomEdgeBuffer);
            tempRoom = FillVoidsWithWalls(tempRoom);

            return tempRoom;
        }

        return new int[1, 1];
    }

    bool GenerateRooms(int roomEdgeBuffer, int levelEdgeBuffer)
    {
        int numFailAttempts = 5;

        for (int i = 0; i < targetNumberRooms; i++)
        {
            // 1. Randomly pick room type
            int roomType = Random.Range(0, 3);
            var tempRoom = GenerateRoomMap((RoomType)roomType, roomEdgeBuffer);

            if (i == 0)
            {
                var firstRoom = new Room
                {
                    rectInt = new RectInt(m_levelWidth / 2, m_levelHeight / 2, tempRoom.GetLength(0), tempRoom.GetLength(1)),
                    map = tempRoom
                };

                TryAddRoom(firstRoom, null);
                continue;
            }

            // pick random existing room
            var randRoomIndex = Random.Range(0, m_rooms.Count);

            var existingRoom = m_rooms[randRoomIndex];

            // build list of possible locations for new room around existing room
            var newRoom = new Room
            {
                rectInt = new RectInt(0, 0, tempRoom.GetLength(0), tempRoom.GetLength(1)),
                map = tempRoom
            };

            List<Vector2Int> positions = new List<Vector2Int>();

            // add top/bottom positions
            int startX = existingRoom.rectInt.xMin - (newRoom.rectInt.width - roomEdgeBuffer * 2 - 2);
            int finishX = existingRoom.rectInt.xMax - (roomEdgeBuffer * 2 + 2);
            int topY = existingRoom.rectInt.yMax;
            int bottomY = existingRoom.rectInt.yMin - (newRoom.rectInt.height);

            for (int x = startX; x <= finishX; x++)
            {
                positions.Add(new Vector2Int(x, topY));
                positions.Add(new Vector2Int(x, bottomY));
            }

            // add left/right positions
            int startY = existingRoom.rectInt.yMin - (newRoom.rectInt.height - roomEdgeBuffer * 2 - 2);
            int finishY = existingRoom.rectInt.yMax - (roomEdgeBuffer * 2 + 2);
            int rightX = existingRoom.rectInt.xMax;
            int leftX = existingRoom.rectInt.xMin - (newRoom.rectInt.width);

            for (int y = startY; y <= finishY; y++)
            {
                positions.Add(new Vector2Int(leftX, y));
                positions.Add(new Vector2Int(rightX, y));
            }

            ListExtensions.Shuffle(positions);

            bool isAdded = false;

            for (int j = 0; j < positions.Count; j++)
            {
                // set the position to first member of shuffled list
                newRoom.rectInt.x = positions[j].x;
                newRoom.rectInt.y = positions[j].y;

                if (TryAddRoom(newRoom, existingRoom, levelEdgeBuffer))
                {
                    isAdded = true;
                    break;
                }
            }

            positions.Clear();

            if (!isAdded && numFailAttempts > 0)
            {
                numFailAttempts--;
                i--;
            }
        }
     
        Debug.Log("Placed succesfully");
        return true;
    }

    void BuildCorridors(int roomEdgeBuffer)
    {
        for (int i = 0; i < m_rooms.Count; i++)
        {
            var room = m_rooms[i];
            var parentRoom = room.parent;
            if (parentRoom == null) continue;

            if (room.directionToParent == Direction.Left)
            {
                int lowerY = Unity.Mathematics.math.max(room.rectInt.yMin, parentRoom.rectInt.yMin) + roomEdgeBuffer;
                int upperY = Unity.Mathematics.math.min(room.rectInt.yMax-1, parentRoom.rectInt.yMax-1) - roomEdgeBuffer;
                int y = Random.Range(lowerY, upperY-1);

                // fill to right
                bool isUpperDone = false;
                bool isLowerDone = false;
                for (int x = room.rectInt.xMin; x < room.rectInt.xMax; x++)
                {
                    if (m_map[x, y] == 1) isLowerDone = true;
                    if(!isLowerDone) m_map[x, y] = 1;

                    if (m_map[x, y + 1] == 1) isUpperDone = true;
                    if (!isUpperDone) m_map[x, y + 1] = 1;
                }

                // fill to left
                isUpperDone = false;
                isLowerDone = false;
                for (int x = parentRoom.rectInt.xMax-1; x > parentRoom.rectInt.xMin; x--)
                {
                    if (m_map[x, y] == 1) isLowerDone = true;
                    if (!isLowerDone) m_map[x, y] = 1;

                    if (m_map[x, y + 1] == 1) isUpperDone = true;
                    if (!isUpperDone) m_map[x, y + 1] = 1;
                }
            }

            if (room.directionToParent == Direction.Right)
            {
                int lowerY = Unity.Mathematics.math.max(room.rectInt.yMin, parentRoom.rectInt.yMin) + roomEdgeBuffer;
                int upperY = Unity.Mathematics.math.min(room.rectInt.yMax-1, parentRoom.rectInt.yMax-1) - roomEdgeBuffer;
                int y = Random.Range(lowerY, upperY -1);

                // fill to left
                bool isUpperDone = false;
                bool isLowerDone = false;
                for (int x = room.rectInt.xMax - 1; x > room.rectInt.xMin; x--)
                {
                    if (m_map[x, y] == 1) isLowerDone = true;
                    if (!isLowerDone) m_map[x, y] = 1;

                    if (m_map[x, y + 1] == 1) isUpperDone = true;
                    if (!isUpperDone) m_map[x, y + 1] = 1;
                }

                // fill to right
                isUpperDone = false;
                isLowerDone = false;
                for (int x = parentRoom.rectInt.xMin; x < parentRoom.rectInt.xMax; x++)
                {
                    if (m_map[x, y] == 1) isLowerDone = true;
                    if (!isLowerDone) m_map[x, y] = 1;

                    if (m_map[x, y + 1] == 1) isUpperDone = true;
                    if (!isUpperDone) m_map[x, y + 1] = 1;
                }
            }

            if (room.directionToParent == Direction.Down)
            {
                int lowerX = Unity.Mathematics.math.max(room.rectInt.xMin, parentRoom.rectInt.xMin) + roomEdgeBuffer;
                int upperX = Unity.Mathematics.math.min(room.rectInt.xMax-1, parentRoom.rectInt.xMax-1) - roomEdgeBuffer;
                int x = Random.Range(lowerX, upperX -1);

                // fill room up
                bool isUpperDone = false;
                bool isLowerDone = false;
                for (int y = room.rectInt.yMin; y < room.rectInt.yMax; y++)
                {
                    if (m_map[x, y] == 1) isLowerDone = true;
                    if (!isLowerDone) m_map[x, y] = 1;

                    if (m_map[x + 1, y] == 1) isUpperDone = true;
                    if (!isUpperDone) m_map[x + 1, y] = 1;
                }

                // fill parent down
                isUpperDone = false;
                isLowerDone = false;
                for (int y = parentRoom.rectInt.yMax-1; y > parentRoom.rectInt.yMin; y--)
                {
                    if (m_map[x, y] == 1) isLowerDone = true;
                    if (!isLowerDone) m_map[x, y] = 1;

                    if (m_map[x + 1, y] == 1) isUpperDone = true;
                    if (!isUpperDone) m_map[x + 1, y] = 1;
                }
            }

            if (room.directionToParent == Direction.Up)
            {
                int lowerX = Unity.Mathematics.math.max(room.rectInt.xMin, parentRoom.rectInt.xMin) + roomEdgeBuffer;
                int upperX = Unity.Mathematics.math.min(room.rectInt.xMax-1, parentRoom.rectInt.xMax-1) - roomEdgeBuffer;
                int x = Random.Range(lowerX, upperX -1);

                // fill room down
                bool isUpperDone = false;
                bool isLowerDone = false;
                for (int y = room.rectInt.yMax-1; y > room.rectInt.yMin; y--)
                {
                    if (m_map[x, y] == 1) isLowerDone = true;
                    if (!isLowerDone) m_map[x, y] = 1;

                    if (m_map[x + 1, y] == 1) isUpperDone = true;
                    if (!isUpperDone) m_map[x + 1, y] = 1;
                }

                // fill parent up
                isUpperDone = false;
                isLowerDone = false;
                for (int y = parentRoom.rectInt.yMin; y < parentRoom.rectInt.yMax; y++)
                {
                    if (m_map[x, y] == 1) isLowerDone = true;
                    if (!isLowerDone) m_map[x, y] = 1;

                    if (m_map[x + 1, y] == 1) isUpperDone = true;
                    if (!isUpperDone) m_map[x + 1, y] = 1;
                }
            }
        }
    }

    void PrepForWallTilemapping(int levelEdgeBuffer)
    {
        int floorTile = (int)GridType.Floor;
        int wallTile = (int)GridType.Wall;

        int mapLengthX = m_map.GetLength(0);
        int mapLengthY = m_map.GetLength(1);

        // 1. first we fill in all the slightly missing tiles
        for (int x = 0; x < mapLengthX; x++)
        {
            for (int y = 0; y < mapLengthY; y++)
            {
                // check fill to right
                if (x - 1 >= 0 && m_map[x,y] == wallTile && m_map[x-1,y] == floorTile)
                {
                    bool isNeedFilling = false;
                    for (int i = 0; i < 4; i++)
                    {
                        if (x + i < mapLengthX && m_map[x + i, y] == floorTile) isNeedFilling = true;
                    }

                    if (isNeedFilling)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (x + i >= mapLengthX || m_map[x + i, y] == floorTile) break;

                            m_map[x + i, y] = floorTile;
                            //m_map[x + i, y - 1] = floorTile;
                        }
                    }
                }

                // check fill to left
                if (x + 1 < mapLengthX && m_map[x, y] == wallTile && m_map[x + 1, y] == floorTile)
                {
                    bool isNeedFilling = false;
                    for (int i = 0; i < 4; i++)
                    {
                        if (x - i >= 0 && m_map[x - i, y] == floorTile) isNeedFilling = true;
                    }

                    if (isNeedFilling)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (x - i < 0 || m_map[x - i, y] == floorTile) break;

                            m_map[x - i, y] = floorTile;
                        }
                    }
                }

                // check fill up
                if (y-1 >= 0 && m_map[x, y] == wallTile && m_map[x, y-1] == floorTile)
                {
                    bool isNeedFilling = false;
                    for (int i = 0; i < 4; i++)
                    {
                        if (y + i < mapLengthY && m_map[x, y + i] == floorTile) isNeedFilling = true;
                    }

                    if (isNeedFilling)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (y + i >= mapLengthY || m_map[x, y + i] == floorTile) break;

                            m_map[x, y + i] = floorTile;
                        }
                    }
                }

                // check fill down
                if (y+1 < mapLengthY && m_map[x, y] == wallTile && m_map[x, y+1] == floorTile)
                {
                    bool isNeedFilling = false;
                    for (int i = 0; i < 4; i++)
                    {
                        if (y - i >= 0 && m_map[x, y - i] == floorTile) isNeedFilling = true;
                    }

                    if (isNeedFilling)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (y - i < 0 || m_map[x, y - i] == floorTile) break;

                            m_map[x, y - i] = floorTile;
                        }
                    }
                }
            }
        }

        
        // 2. second we want to give any top row wall tiles (up against voids) have an extra 3 tiles for tilemapping tree tops
        for (int x = levelEdgeBuffer; x < mapLengthX - levelEdgeBuffer; x++)
        {
            for (int y = mapLengthY - levelEdgeBuffer; y >= levelEdgeBuffer; y--)
            {
                if (m_map[x,y] == (int)GridType.Void && m_map[x,y-1] == (int)GridType.Wall)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if (m_map[x,y+i] == (int)GridType.Void)
                        {
                            m_map[x, y + i] = (int)GridType.Wall;
                        }
                    }
                }
            }
        }

        // 3. check for isolated on both sides floor tiles
        for (int x = levelEdgeBuffer; x < mapLengthX - levelEdgeBuffer; x++)
        {
            for (int y = mapLengthY - levelEdgeBuffer; y >= levelEdgeBuffer; y--)
            {
                if (m_map[x, y] == (int)GridType.Floor && m_map[x, y - 1] == (int)GridType.Wall && m_map[x, y +1] == (int)GridType.Wall)
                {
                    m_map[x, y - 1] = (int)GridType.Floor;
                    m_map[x, y + 1] = (int)GridType.Floor;
                }

                if (m_map[x, y] == (int)GridType.Floor && m_map[x-1, y] == (int)GridType.Wall && m_map[x+1, y] == (int)GridType.Wall)
                {
                    m_map[x-1, y] = (int)GridType.Floor;
                    m_map[x+1, y] = (int)GridType.Floor;
                }
            }
        }

    }

    bool TryAddRoom(Room newRoom, Room parentRoom, int levelEdgeBuffer = 2)
    {
        // 1. check room still in bounds of level (and add on a buffer
        if (newRoom.rectInt.xMin < levelEdgeBuffer || newRoom.rectInt.xMax >= m_map.GetLength(0) - levelEdgeBuffer ||
            newRoom.rectInt.yMin < levelEdgeBuffer || newRoom.rectInt.yMax >= m_map.GetLength(1) - levelEdgeBuffer)
        {
            return false;
        }

        // see if new room position is acceptable
        for (int x = 0; x < newRoom.rectInt.width; x++)
        {
            for (int y = 0; y < newRoom.rectInt.height; y++)
            {
                if (m_map[newRoom.rectInt.x + x, newRoom.rectInt.y + y] != 0)
                {
                    return false;
                }
            }
        }

        // do parent stuff
        newRoom.parent = parentRoom;
        if (parentRoom != null)
        {
            if (newRoom.rectInt.xMin == parentRoom.rectInt.xMax) newRoom.directionToParent = Direction.Left;
            if (newRoom.rectInt.xMax == parentRoom.rectInt.xMin) newRoom.directionToParent = Direction.Right;
            if (newRoom.rectInt.yMin == parentRoom.rectInt.yMax) newRoom.directionToParent = Direction.Down;
            if (newRoom.rectInt.yMax == parentRoom.rectInt.yMin) newRoom.directionToParent = Direction.Up;
        }

        // ok room is ok, we can add it
        m_rooms.Add(newRoom);

        Debug.Log("Placed room at x: " + newRoom.rectInt.x + ", y: " + newRoom.rectInt.y);

        // map the room to m_map (for rendering)
        for (int x = 0; x < newRoom.rectInt.width; x++)
        {
            for (int y = 0; y < newRoom.rectInt.height; y++)
            {
                if (newRoom.map[x, y] == 1)
                {
                    m_map[newRoom.rectInt.x + x, newRoom.rectInt.y + y] = 1;
                }
                else if (newRoom.map[x, y] == 2)
                {
                    m_map[newRoom.rectInt.x + x, newRoom.rectInt.y + y] = 2;
                }
            }
        }

        return true;
    }

    private int CountGridNeighbours(int[,] map, GridType gridType, int x, int y)
    {
        int count = 0;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;   // ignore ourselves

                int nx = x + dx;
                int ny = y + dy;

                if (nx < 0 || nx >= map.GetLength(0) || ny < 0 || ny >= map.GetLength(1)) continue;

                if (map[nx, ny] == (int)gridType) count++;
            }
        }

        return count;
    }

    void RenderMap(int levelEdgeBuffer = 2)
    {
        int mapLengthX = m_map.GetLength(0);
        int mapLengthY = m_map.GetLength(1);

        for (int x = levelEdgeBuffer; x < mapLengthX - levelEdgeBuffer; x++)
        {
            for (int y = levelEdgeBuffer; y < mapLengthY - levelEdgeBuffer; y++)
            {
                if (m_map[x, y] == 0)
                {
                    // draw leafTop tile if the tile below is a wall
                    if (m_map[x, y - 1] == 2)
                    {
                        topLeaf_Tilemap.SetTile(new Vector3Int(x, y, 0), topLeaf_RuleTile);

                        // draw hangleaf below
                        hangLeaf_Tilemap.SetTile(new Vector3Int(x, y - 1, 0), hangLeaf_RuleTile);
                    }
                }

                if (m_map[x, y] == 1 ||
                    x == 0 || y == 0 || x == mapLengthX-1 || y == mapLengthY-1)
                {
                    baseWorking_Tilemap.SetTile(new Vector3Int(x, y, 0), floorTile);

                    // draw leafTop tile if the tile below is a wall
                    if (m_map[x,y-1] == 2)
                    {
                        topLeaf_Tilemap.SetTile(new Vector3Int(x, y, 0), topLeaf_RuleTile);

                        // draw hangleaf below
                        hangLeaf_Tilemap.SetTile(new Vector3Int(x, y - 1, 0), hangLeaf_RuleTile);
                    }
                }

                if (m_map[x, y] == 2)
                {
                    baseWorking_Tilemap.SetTile(new Vector3Int(x, y, 0), doorTile);

                    treeTrunk_Tilemap.SetTile(new Vector3Int(x, y, 0), treeTrunk_RuleTile);

                    // draw tree top tile if its a wall tile AND there's not a floor tile 2 tiles below it
                    if (m_map[x, y-2] != 1)
                    {
                        topLeaf_Tilemap.SetTile(new Vector3Int(x, y, 0), topLeaf_RuleTile);

                        // draw hangleaf below
                        hangLeaf_Tilemap.SetTile(new Vector3Int(x, y - 1, 0), hangLeaf_RuleTile);
                    }
                }
            }
        }

        // finally draw the border
        for (int x = levelEdgeBuffer; x < mapLengthX - levelEdgeBuffer; x++)
        {
            for (int y = levelEdgeBuffer; y < mapLengthY - levelEdgeBuffer; y++)
            {
                if (m_map[x,y] == (int)GridType.Wall)
                {
                    int countVoids = CountGridNeighbours(m_map, GridType.Void, x, y);
                    if (countVoids > 0)
                    {
                        border_Tilemap.SetTile(new Vector3Int(x, y, 0), border_Tile);

                        if (m_map[x,y+1] == (int)GridType.Void)
                        {
                            border_Tilemap.SetTile(new Vector3Int(x, y + 1, 0), border_Tile);
                        }

                        if (m_map[x, y - 1] == (int)GridType.Void)
                        {
                            border_Tilemap.SetTile(new Vector3Int(x, y - 1, 0), border_Tile);
                        }
                    }
                }
            }
        }


    }

    void ClearExistingLevel()
    {
        baseWorking_Tilemap.ClearAllTiles();
        ground_Tilemap.ClearAllTiles();
        treeTrunk_Tilemap.ClearAllTiles();
        topLeaf_Tilemap.ClearAllTiles();
        hangLeaf_Tilemap.ClearAllTiles();
        border_Tilemap.ClearAllTiles();

        m_rooms.Clear();
    }

    #region Room Utilities

    // Helper function to trim isolated floor tiles along the cardinal edges of the ellipse
    private int[,] TrimIsolatedEdgeFloorTiles(int[,] room, int edgeBuffer)
    {
        int roomWidth = room.GetLength(0);
        int roomHeight = room.GetLength(1);
        int[,] tempRoom = (int[,])room.Clone(); // Create a copy to modify

        // perform smoothing pass
        for (int x = 0; x < roomWidth; x++)
        {
            for (int y = 0; y < roomHeight; y++)
            {
                if (x < 2 || x > roomWidth - 3 || y < 2 || y > roomHeight - 3)
                {
                    // do nothing
                }
                else
                {
                    if (tempRoom[x, y] == (int)GridType.Floor)
                    {
                        var floorGridNeighbours = CountGridNeighbours(tempRoom, GridType.Floor, x, y);

                        if ((tempRoom[x, y + 1] == (int)GridType.Void && tempRoom[x, y - 1] == (int)GridType.Void) ||
                            (tempRoom[x + 1, y] == (int)GridType.Void && tempRoom[x + 1, y] == (int)GridType.Void))
                        {
                            if (floorGridNeighbours <= 3)
                            {
                                tempRoom[x, y] = (int)GridType.Void;
                            }
                        }

                    }
                }
            }
        }

        return tempRoom;
    }

    private int[,] KeepLargestContinuousFloorSection(int[,] room)
    {
        int width = room.GetLength(0);
        int height = room.GetLength(1);
        List<List<Vector2Int>> regions = new List<List<Vector2Int>>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        // Find all connected regions of floor tiles using flood-fill
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (room[x, y] == (int)GridType.Floor && !visited.Contains(new Vector2Int(x, y)))
                {
                    List<Vector2Int> region = new List<Vector2Int>();
                    Queue<Vector2Int> queue = new Queue<Vector2Int>();
                    queue.Enqueue(new Vector2Int(x, y));
                    visited.Add(new Vector2Int(x, y));

                    while (queue.Count > 0)
                    {
                        Vector2Int current = queue.Dequeue();
                        int cx = current.x;
                        int cy = current.y;
                        region.Add(current);

                        // Check all 4 adjacent tiles (you can use 8 for diagonals if desired)
                        int[] dx = { 0, 0, -1, 1 };
                        int[] dy = { -1, 1, 0, 0 };
                        for (int i = 0; i < 4; i++)
                        {
                            int nx = cx + dx[i];
                            int ny = cy + dy[i];
                            Vector2Int neighbor = new Vector2Int(nx, ny);
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height &&
                                room[nx, ny] == (int)GridType.Floor && !visited.Contains(neighbor))
                            {
                                visited.Add(neighbor);
                                queue.Enqueue(neighbor);
                            }
                        }
                    }
                    regions.Add(region);
                }
            }
        }

        // Find the largest region manually without LINQ
        if (regions.Count == 0)
            return room; // No floor tiles, return as is

        List<Vector2Int> largestRegion = regions[0];
        int maxSize = largestRegion.Count;

        // Iterate through regions to find the one with the most tiles
        for (int i = 1; i < regions.Count; i++)
        {
            if (regions[i].Count > maxSize)
            {
                largestRegion = regions[i];
                maxSize = regions[i].Count;
            }
        }

        int[,] newRoom = new int[width, height];

        // Copy walls and voids from the original room
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (room[x, y] == (int)GridType.Wall)
                    newRoom[x, y] = (int)GridType.Wall;
                else
                    newRoom[x, y] = (int)GridType.Void;
            }
        }

        // Fill in only the largest region with floor tiles
        foreach (Vector2Int pos in largestRegion)
        {
            newRoom[pos.x, pos.y] = (int)GridType.Floor;
        }

        return newRoom;
    }

    // 2. Resize the room to fit the bounds of the largest continuous floor section, maintaining edge buffer
    private int[,] ResizeRoomToFloorBounds(int[,] room, int edgeBuffer)
    {
        int width = room.GetLength(0);
        int height = room.GetLength(1);

        // Find the bounds of floor tiles (ignoring walls)
        int minX = width - 1, maxX = 0, minY = height - 1, maxY = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (room[x, y] == (int)GridType.Floor)
                {
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }
        }

        // If no floor tiles exist, return the original room (or handle as needed)
        if (minX > maxX || minY > maxY)
            return room;

        // Calculate new dimensions, ensuring edge buffer
        int newWidth = (maxX - minX + 1) + (edgeBuffer * 2);
        int newHeight = (maxY - minY + 1) + (edgeBuffer * 2);

        // Create new room with resized dimensions
        int[,] resizedRoom = new int[newWidth, newHeight];

        // Fill new room with walls for the edge buffer
        for (int x = 0; x < newWidth; x++)
        {
            for (int y = 0; y < newHeight; y++)
            {
                if (x < edgeBuffer || x >= newWidth - edgeBuffer ||
                    y < edgeBuffer || y >= newHeight - edgeBuffer)
                {
                    resizedRoom[x, y] = (int)GridType.Wall;
                }
                else
                {
                    resizedRoom[x, y] = (int)GridType.Void;
                }
            }
        }

        // Copy the floor tiles from the original room, offset by the edge buffer
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                if (room[x, y] == (int)GridType.Floor)
                {
                    int newX = x - minX + edgeBuffer;
                    int newY = y - minY + edgeBuffer;
                    resizedRoom[newX, newY] = (int)GridType.Floor;
                }
            }
        }

        return resizedRoom;
    }

    // Updated helper function to fill isolated wall islands with floor tiles
    private int[,] FillIsolatedWallIslands(int[,] room, int edgeBuffer)
    {
        int width = room.GetLength(0);
        int height = room.GetLength(1);
        int[,] newRoom = (int[,])room.Clone(); // Create a copy to modify
        List<List<Vector2Int>> wallRegions = new List<List<Vector2Int>>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        // Find all connected regions of wall tiles using flood-fill
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (room[x, y] == (int)GridType.Wall && !visited.Contains(new Vector2Int(x, y)))
                {
                    List<Vector2Int> region = new List<Vector2Int>();
                    Queue<Vector2Int> queue = new Queue<Vector2Int>();
                    queue.Enqueue(new Vector2Int(x, y));
                    visited.Add(new Vector2Int(x, y));

                    bool touchesBorder = false;
                    int regionSize = 0;

                    while (queue.Count > 0)
                    {
                        Vector2Int current = queue.Dequeue();
                        int cx = current.x;
                        int cy = current.y;
                        region.Add(current);
                        regionSize++;

                        // Check if this tile touches the border (within edgeBuffer of any edge)
                        if (cx < edgeBuffer || cx >= width - edgeBuffer ||
                            cy < edgeBuffer || cy >= height - edgeBuffer)
                        {
                            touchesBorder = true;
                            break;
                        }

                        // Use 8-directional connectivity to better capture wall regions
                        int[] dx = { -1, -1, -1, 0, 0, 1, 1, 1 };
                        int[] dy = { -1, 0, 1, -1, 1, -1, 0, 1 };
                        for (int i = 0; i < 8; i++)
                        {
                            int nx = cx + dx[i];
                            int ny = cy + dy[i];
                            Vector2Int neighbor = new Vector2Int(nx, ny);
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height &&
                                room[nx, ny] == (int)GridType.Wall && !visited.Contains(neighbor))
                            {
                                // Check if the neighbor touches the border
                                if (nx < edgeBuffer || nx >= width - edgeBuffer ||
                                    ny < edgeBuffer || ny >= height - edgeBuffer)
                                {
                                    touchesBorder = true;
                                    break;
                                }
                                visited.Add(neighbor);
                                queue.Enqueue(neighbor);
                            }
                        }
                        if (touchesBorder) break;
                    }

                    // Log the region for debugging
                    Debug.Log($"Found wall region at {x}, {y} with size {regionSize} tiles, touchesBorder = {touchesBorder}");

                    // Only add the region to the list if it doesn’t touch the border
                    if (!touchesBorder)
                    {
                        wallRegions.Add(region);
                    }
                }
            }
        }

        // Fill all internal wall regions (those not touching the border) with floor tiles
        foreach (var region in wallRegions)
        {
            Debug.Log($"Filling internal wall region of size {region.Count} with floor tiles");
            foreach (var pos in region)
            {
                newRoom[pos.x, pos.y] = (int)GridType.Floor;
            }
        }

        return newRoom;
    }

    private int[,] FillVoidsWithWalls(int[,] room)
    {
        for (int x = 0; x < room.GetLength(0); x++)
        {
            for (int y = 0; y < room.GetLength(1); y++)
            {
                if (room[x,y] == 0)
                {
                    room[x, y] = 2;
                }
            }
        }

        return room;
    }

    #endregion
}
