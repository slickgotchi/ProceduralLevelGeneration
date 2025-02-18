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
                    if (noiseValue > 0.5f)
                    {
                        groundDetailA_Tilemap.SetTile(new Vector3Int(x, y, 0), groundDetailA_RuleTile);
                    }
                }
            }
        }
    }
}
