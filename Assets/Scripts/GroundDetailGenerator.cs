using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Unity.Mathematics;

public class GroundDetailGenerator : MonoBehaviour
{
    public static GroundDetailGenerator Instance { get; private set; }

    public Tilemap groundDetailA_Tilemap;
    public RuleTile groundDetailA_RuleTile;

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

    public void Generate()
    {
        groundDetailA_Tilemap.ClearAllTiles();

        // iterate over rooms and draw in using snoise
        var rooms = LevelGenerator.Instance.GetRooms();
        foreach (var room in rooms)
        {
            for (int x = room.xMin; x < room.xMax-1; x++)
            {
                for (int y = room.yMin; y < room.yMax-1; y++)
                {
                    var noiseValue = noise.snoise(new float2(x, y));
                    if (noiseValue > 0.618f)
                    {
                        //groundDetailA_Tilemap.SetTile(new Vector3Int(x, y, 0), groundDetailA_RuleTile);
                        Set3x2TileRectangle(x, y);
                    }
                }
            }
        }

        RemoveSingleTiles();
    }

    void Set3x2TileRectangle(int x, int y)
    {
        for (int i = x; i < x + 3; i++)
        {
            for (int j = y; j < y + 2; j++)
            {
                if (LevelGenerator.Instance.IsInRoomBounds(i, j))
                {
                    Debug.Log("In room bounds");
                    groundDetailA_Tilemap.SetTile(new Vector3Int(i, j, 0), groundDetailA_RuleTile);
                }
            }
        }
    }


    void RemoveSingleTiles()
    {
        var tilemapBounds = groundDetailA_Tilemap.cellBounds;
        for (int x = tilemapBounds.xMin; x < tilemapBounds.xMax-1; x++)
        {
            for (int y = tilemapBounds.yMin; y < tilemapBounds.yMax-1; y++)
            {

            }
        }
    }

    bool IsTileAdjacent(int x, int y, AdjacentSide adjacentSide)
    {
        Vector3Int checkVector = GetAdjacentSideVector3Int(x, y, adjacentSide);
        return groundDetailA_Tilemap.GetTile(checkVector) as RuleTile == groundDetailA_RuleTile;
    }

    Vector3Int GetAdjacentSideVector3Int(int x, int y, AdjacentSide adjacentSide)
    {
        switch (adjacentSide)
        {
            case AdjacentSide.TopLeft: return new Vector3Int(x - 1, y + 1, 0);
            case AdjacentSide.TopMiddle: return new Vector3Int(x, y + 1, 0);
            case AdjacentSide.TopRight: return new Vector3Int(x + 1, y + 1, 0);

            case AdjacentSide.MiddleLeft: return new Vector3Int(x - 1, y, 0);
            case AdjacentSide.MiddleRight: return new Vector3Int(x + 1, y, 0);

            case AdjacentSide.BottomLeft: return new Vector3Int(x - 1, y - 1, 0);
            case AdjacentSide.BottomMiddle: return new Vector3Int(x, y - 1, 0);
            case AdjacentSide.BottomRight: return new Vector3Int(x + 1, y - 1, 0);

            default: break;
        }

        return Vector3Int.zero;
    }

    public enum AdjacentSide
    {
        TopLeft, TopMiddle, TopRight,
        MiddleLeft, MiddleRight,
        BottomLeft, BottomMiddle, BottomRight
    }


}
