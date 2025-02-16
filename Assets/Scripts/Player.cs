using UnityEngine;

public class Player : MonoBehaviour
{
    public float moveSpeed = 5f;

    [Header("Sprites")]
    public Sprite spriteFront;
    public Sprite spriteBack;
    public Sprite spriteLeft;
    public Sprite spriteRight;

    public SpriteRenderer spriteRenderer;
    private Vector2 movement;

    void Start()
    {
        if (spriteFront) spriteRenderer.sprite = spriteFront; // Default sprite
    }

    void Update()
    {
        movement.x = Input.GetAxisRaw("Horizontal"); // A/D or Left/Right
        movement.y = Input.GetAxisRaw("Vertical");   // W/S or Up/Down

        UpdateSpriteDirection();
    }

    void FixedUpdate()
    {
        transform.position += (Vector3)movement.normalized * moveSpeed * Time.fixedDeltaTime;
    }

    void UpdateSpriteDirection()
    {
        if (movement.x > 0)
        {
            spriteRenderer.sprite = spriteRight;
        }
        else if (movement.x < 0)
        {
            spriteRenderer.sprite = spriteLeft;
        }
        else if (movement.y > 0)
        {
            spriteRenderer.sprite = spriteBack;
        }
        else if (movement.y < 0)
        {
            spriteRenderer.sprite = spriteFront;
        }
    }
}
