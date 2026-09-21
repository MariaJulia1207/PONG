using UnityEngine;

public class GoalArea : MonoBehaviour
{
    [SerializeField] private int goalOwner; // 1 = gol do jogador 1 / 2 = gol do jogador 2
    [SerializeField] private ScoreUIController scoreUIController;

    private Vector3 ballStartPosition = Vector3.zero;

    private void Start()
    {
        // Substituído FindObjectOfType por FindAnyObjectByType para eliminar o warning CS0618
        Ball b = FindAnyObjectByType<Ball>();
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

        // Adiciona ponto ao jogador correto
        if (goalOwner == 1)
        {
            scoreUIController?.AddGoalToPlayer(2);
        }
        else if (goalOwner == 2)
        {
            scoreUIController?.AddGoalToPlayer(1);
        }

        // Reseta a posição da bola para o centro
        other.transform.position = ballStartPosition;

        // Executa o novo lançamento seguro através do próprio script da bola
        ball.LaunchBall();
    }
}