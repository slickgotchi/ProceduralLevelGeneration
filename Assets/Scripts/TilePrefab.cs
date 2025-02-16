using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TilePrefab : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;
    public Color groundColor;
    public Color wallColor;

    public enum TileType { Null, Ground, Wall }

    public void Init(TileType tileType)
    {
        if (tileType == TileType.Ground) spriteRenderer.color = groundColor;
        else if (tileType == TileType.Wall) spriteRenderer.color = wallColor;
    }
}
