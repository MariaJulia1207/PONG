using UnityEngine;

public class GoalArea : MonoBehaviour
{
    [SerializeField] private int goalOwner; // 1 = gol do jogador 1 / 2 = gol do jogador 2
    [SerializeField] private ScoreUIController scoreUIController;

    private Vector3 ballStartPosition = Vector3.zero;

    private void Start()
    {
        Ball b = FindObjectOfType<Ball>();
        if (b != null)
        {
            ballStartPosition = b.transform.position;
        }
    }

    public void SetGoalOwner(int playerNumber)
    {
        goalOwner = playerNumber;
    }

    public void SetScoreUIController(ScoreUIController controller)
    {
        scoreUIController = controller;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Ball ball = other.GetComponent<Ball>();
        if (ball == null)
        {
            return;
        }

        if (goalOwner == 1)
        {
            scoreUIController?.AddGoalToPlayer(2);
        }
        else if (goalOwner == 2)
        {
            scoreUIController?.AddGoalToPlayer(1);
        }

        Rigidbody2D ballRb = ball.rb != null ? ball.rb : other.GetComponent<Rigidbody2D>();
        if (ballRb != null)
        {
            // stop and reset to the recorded initial position
            ballRb.linearVelocity = Vector2.zero;
            other.transform.position = ballStartPosition;

            // relaunch the ball similarly to Ball.Start()
            bool isRight = UnityEngine.Random.value >= 0.5f;
            float xVelocity = isRight ? 1f : -1f;
            float yVelocity = UnityEngine.Random.Range(-1f, 1f);
            if (Mathf.Abs(yVelocity) < 0.2f)
            {
                yVelocity = UnityEngine.Random.value >= 0.5f ? 0.5f : -0.5f;
            }

            float speed = ball.startingSpeed;
            ballRb.linearVelocity = new Vector2(xVelocity * speed, yVelocity * speed);
        }
    }
}
