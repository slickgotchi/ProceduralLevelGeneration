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

    public Tilemap groundDetailB_Tilemap;
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
        groundDetailA_Tilemap.ClearAllTiles();
        groundDetailB_Tilemap.ClearAllTiles();

        // iterate over rooms and draw in using snoise
        var rooms = LevelGenerator.Instance.GetRooms();
        foreach (var room in rooms)
        {
            for (int x = room.xMin; x < room.xMax-1; x++)
            {
                for (int y = room.yMin; y < room.yMax-1; y++)
                {
                    var noiseValue = (noise.snoise(new float2(x, y)) + 1) * 0.5f;
                    if (noiseValue > 0.87f)
                    {
                        SetTileRectangle(x, y, 6, 4, true);
                    }

                    noiseValue = (noise.snoise(new float2(x+10, y+10)) + 1) * 0.5f;
                    if (noiseValue > 0.86f)
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
                if (LevelGenerator.Instance.IsInRoomBounds(i, j))
                {
                    if (isA)
                    {
                        groundDetailA_Tilemap.SetTile(new Vector3Int(i, j, 0), groundDetailA_RuleTile);
                    }
                    else
                    {
                        groundDetailB_Tilemap.SetTile(new Vector3Int(i, j, 0), groundDetailB_RuleTile);
                    }
                }
            }
        }
    }
}
