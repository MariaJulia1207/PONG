using UnityEngine;

public class GoalArea : MonoBehaviour
{
    [SerializeField] private int goalOwner; // 1 = Gol do P1 | 2 = Gol do P2
    [SerializeField] private ScoreUIController scoreUIController;

    private Vector3 ballStartPosition = Vector3.zero;

    private void Start()
    {
        Ball b = FindAnyObjectByType<Ball>();
        if (b != null)
        {
            ballStartPosition = b.transform.position;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Ball ball = other.GetComponent<Ball>();
        if (ball == null) return;

        if (goalOwner == 1)
        {
            scoreUIController?.AddGoalToPlayer(2);
        }
        else if (goalOwner == 2)
        {
            scoreUIController?.AddGoalToPlayer(1);
        }

        other.transform.position = ballStartPosition;
        ball.LaunchBall();
    }
}