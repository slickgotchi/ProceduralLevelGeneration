using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TreeBaseGenerator : MonoBehaviour
{
    public static TreeBaseGenerator Instance { get; private set; }

    public Tilemap hangLeafTilemap;
    public Tilemap treeBaseTilemap;

    public RuleTile treeBase_RuleTile;

    public List<Sprite> searchTileSprites = new List<Sprite>();

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
        BoundsInt bounds = hangLeafTilemap.cellBounds;
        Vector3Int topLeft = new Vector3Int(bounds.xMin, bounds.yMax - 1, 0); // Start at top-left

        for (int y = topLeft.y; y >= bounds.yMin; y--) // Move downward
        {
            for (int x = topLeft.x; x < bounds.xMax; x++) // Move rightward
            {
                Vector3Int tilePosition = new Vector3Int(x, y, 0);
                TileBase hangLeafTile = hangLeafTilemap.GetTile(tilePosition);
                Sprite hangLeafTileSprite = hangLeafTilemap.GetSprite(tilePosition);
                if (hangLeafTileSprite == null) continue;

                foreach (var searchTileSprite in searchTileSprites)
                {
                    if (hangLeafTile is RuleTile hangLeafRuleTile)
                    {
                        //Sprite searchTileSprite = tilePair.searchTileSprite;
                        if (hangLeafTileSprite == searchTileSprite)
                        {
                            //Debug.Log("Found a matching RuleTile sprite at " + tilePosition);
                            PlaceTilesBelow(tilePosition);
                        }
                    }
                    else if (hangLeafTile == searchTileSprite)
                    {
                        //Debug.Log("Found a matching TileBase at " + tilePosition);
                        PlaceTilesBelow(tilePosition);
                    }
                }

            }
        }
    }

    private void PlaceTilesBelow(Vector3Int position)
    {
        for (int i = 0; i < 2; i++)
        {
            if (!LevelGenerator.Instance.IsInRoomBounds(position.x, position.y - i)) continue;
            Debug.Log("in bounds!");
            Vector3Int belowPosition = new Vector3Int(position.x, position.y - i, 0);
            //treeBaseTilemap.SetTile(belowPosition, belowTiles[i]);
            treeBaseTilemap.SetTile(belowPosition, treeBase_RuleTile);
        }
    }

}
