using UnityEngine;

public class GoalArea : MonoBehaviour
{
    [Header("Configuração da Baliza")]
    [Tooltip("1 = Área da Equipe 1 (Ponto para Equipe 2) | 2 = Área da Equipe 2 (Ponto para Equipe 1)")]
    [SerializeField] private int teamOwner = 1;

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
        Ball ball = other.GetComponent<Ball>();
        if (ball == null) return;

        // Atribui o ponto à equipe adversária
        if (teamOwner == 1)
        {
            serverController?.AddPointToTeam(2);
        }
        else if (teamOwner == 2)
        {
            serverController?.AddPointToTeam(1);
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