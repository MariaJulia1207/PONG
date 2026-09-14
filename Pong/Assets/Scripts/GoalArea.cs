using UnityEngine;

public class GoalArea : MonoBehaviour
{
    [SerializeField] private int goalOwner; // 1 = gol do jogador 1 / 2 = gol do jogador 2
    [SerializeField] private ScoreUIController scoreUIController;

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
        if (other.GetComponent<Ball>() == null)
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

        Rigidbody2D ballRb = other.GetComponent<Rigidbody2D>();
        if (ballRb != null)
        {
            ballRb.linearVelocity = Vector2.zero;
            other.transform.position = Vector3.zero;
        }
    }
}
