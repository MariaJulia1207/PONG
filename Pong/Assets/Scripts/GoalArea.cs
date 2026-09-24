using UnityEngine;

public class GoalArea : MonoBehaviour
{
    [Header("Configuração da Baliza")]
    [Tooltip("1 = Baliza do P1 (Golo do P2) | 2 = Baliza do P2 (Golo do P1)")]
    [SerializeField] private int goalOwner = 1;

    [SerializeField] private UdpServerController serverController;

    private void Start()
    {
        if (serverController == null)
        {
            serverController = FindAnyObjectByType<UdpServerController>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Garante que apenas o objeto com o script Ball ou Tag "Ball" ativa o gol
        Ball ball = other.GetComponent<Ball>();
        if (ball == null) return;

        // Atribui o ponto ao jogador adversário
        if (goalOwner == 1)
        {
            serverController?.AddPointToPlayer(2);
        }
        else if (goalOwner == 2)
        {
            serverController?.AddPointToPlayer(1);
        }

        // Zera a física da bola e reseta para o centro (0,0)
        Rigidbody2D ballRb = ball.GetComponent<Rigidbody2D>();
        if (ballRb != null)
        {
            ballRb.linearVelocity = Vector2.zero;
            ballRb.angularVelocity = 0f;
        }
        ball.transform.position = Vector3.zero;

        // Relança a bola
        ball.LaunchBall();
    }
}