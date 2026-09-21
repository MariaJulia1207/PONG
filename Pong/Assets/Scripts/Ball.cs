using UnityEngine;

public class Ball : MonoBehaviour
{
    public Rigidbody2D rb;
    public float startingSpeed = 12f;

    void Start()
    {
        LaunchBall();
    }

    public void LaunchBall()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        float xVelocity = Random.value < 0.5f ? -1f : 1f;
        float yVelocity = Random.Range(0.4f, 0.8f) * (Random.value < 0.5f ? -1f : 1f);

        Vector2 direction = new Vector2(xVelocity, yVelocity).normalized;
        rb.linearVelocity = direction * startingSpeed;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (Mathf.Abs(rb.linearVelocity.y) < 0.2f)
        {
            float bounceY = Random.Range(0.3f, 0.6f) * (Random.value < 0.5f ? 1f : -1f);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, bounceY);
        }
    }

    private void FixedUpdate()
    {
        if (rb == null || rb.linearVelocity == Vector2.zero) return;
        rb.linearVelocity = rb.linearVelocity.normalized * startingSpeed;
    }
}