using UnityEngine;
using UnityEngine.UI;

public class ScoreUIController : MonoBehaviour
{
    [Header("Textos de Placar (UI)")]
    [SerializeField] private Text player1ScoreText; // Se usar TextMeshPro, mude para TMP_Text
    [SerializeField] private Text player2ScoreText;

    private int player1Score = 0;
    private int player2Score = 0;

    private bool needsUIUpdate = false;

    // Evento para avisar o Servidor sem precisar do script dele importado
    public delegate void OnScoreChanged(int p1, int p2);
    public event OnScoreChanged OnScoreUpdated;

    private void Start()
    {
        UpdateScoreDisplay();
    }

    private void Update()
    {
        if (needsUIUpdate)
        {
            UpdateScoreDisplay();
            needsUIUpdate = false;
        }
    }

    public void AddGoalToPlayer(int playerNumber)
    {
        if (playerNumber == 1) player1Score++;
        else if (playerNumber == 2) player2Score++;

        needsUIUpdate = true;

        // Dispara o evento de atualização de placar
        OnScoreUpdated?.Invoke(player1Score, player2Score);
    }

    public void SetScore(int p1, int p2)
    {
        player1Score = p1;
        player2Score = p2;
        needsUIUpdate = true;
    }

    private void UpdateScoreDisplay()
    {
        if (player1ScoreText != null) player1ScoreText.text = player1Score.ToString();
        if (player2ScoreText != null) player2ScoreText.text = player2Score.ToString();
    }
}