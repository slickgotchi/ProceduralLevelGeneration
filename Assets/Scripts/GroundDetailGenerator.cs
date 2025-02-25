using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Unity.Mathematics;

public class GroundDetailGenerator : MonoBehaviour
{
    public static GroundDetailGenerator Instance { get; private set; }

    public Tilemap ground_Tilemap;
    public Tilemap groundDetailA_Tilemap;
    public Tilemap groundDetailB_Tilemap;

    public TileBase ground_Tile;
    public RuleTile groundDetailA_RuleTile;
    public RuleTile groundDetailB_RuleTile;

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
        ground_Tilemap.ClearAllTiles();
        groundDetailA_Tilemap.ClearAllTiles();
        groundDetailB_Tilemap.ClearAllTiles();

        // iterate over rooms and draw in using snoise
        var rooms = LevelGenerator_v2.Instance.GetRooms();
        foreach (var room in rooms)
        {
            for (int x = room.rectInt.xMin; x < room.rectInt.xMax; x++)
            {
                for (int y = room.rectInt.yMin; y < room.rectInt.yMax; y++)
                {
                    // set ground tile
                    ground_Tilemap.SetTile(new Vector3Int(x, y, 0), ground_Tile);

                    var noiseValue = (noise.snoise(new float2(x, y)) + 1) * 0.5f;
                    if (noiseValue > 0.87f)
                    {
                        SetTileRectangle(x, y, 6, 4, true);
                    }

                    noiseValue = (noise.snoise(new float2(x+10, y+10)) + 1) * 0.5f;
                    if (noiseValue > 0.87f)
                    {
                        SetTileRectangle(x, y, 3, 2, false);
                    }
                }
            }
        }
    }

    void SetTileRectangle(int x, int y, int width, int height, bool isA)
    {
        for (int i = x; i < x + width; i++)
        {
            for (int j = y; j < y + height; j++)
            {
                if (LevelGenerator_v2.Instance.IsInRoomBounds(i, j))
                {
                    if (isA)
                    {
                        Debug.Log("Set detail A");
                        groundDetailA_Tilemap.SetTile(new Vector3Int(i, j, 0), groundDetailA_RuleTile);
                    }
                    else
                    {
                        Debug.Log("Set detail B");
                        groundDetailB_Tilemap.SetTile(new Vector3Int(i, j, 0), groundDetailB_RuleTile);
                    }
                }
            }
        }
    }
}
