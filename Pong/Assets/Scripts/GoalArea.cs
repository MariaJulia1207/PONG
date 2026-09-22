using UnityEngine;

public class GoalArea : MonoBehaviour
{
    [Header("Configuração da Baliza")]
    [SerializeField] private int goalOwner; // 1 = Baliza do P1 (ponto para P2) | 2 = Baliza do P2 (ponto para P1)
    
    [SerializeField] private UdpServerController serverController;

    private Vector3 ballStartPosition = Vector3.zero;

    private void Start()
    {
        // Procura o servidor na cena se não tiver sido atribuído no Inspector
        if (serverController == null)
        {
            serverController = FindAnyObjectByType<UdpServerController>();
        }

        // Guarda a posição inicial da bola para repor
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

        // Atribui o ponto ao jogador adversário
        if (goalOwner == 1)
        {
            serverController?.AddPointToPlayer(2);
        }
        else if (goalOwner == 2)
        {
            serverController?.AddPointToPlayer(1);
        }

        // Reposiciona a bola no centro e a relança através do servidor/física
        other.transform.position = ballStartPosition;
        ball.LaunchBall();
    }
}