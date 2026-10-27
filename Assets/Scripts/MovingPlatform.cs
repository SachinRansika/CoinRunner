using UnityEngine;

// Optional (Challenge 5). Not used by the default scene.
public class MovingPlatform : MonoBehaviour
{
    public float distance = 3f;
    public float speed = 1f;

    private Vector3 startPos;
    private Rigidbody2D rb;

    void Start()
    {
        startPos = transform.position;
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        float x = Mathf.PingPong(Time.time * speed, distance);
        rb.MovePosition(startPos + new Vector3(x, 0f, 0f));
    }
}
