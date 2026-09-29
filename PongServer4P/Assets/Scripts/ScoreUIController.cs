using UnityEngine;
using UnityEngine.UI;
// Descomente a linha abaixo se estiver usando TextMeshPro em vez da UI padrão
// using TMPro;

public class ScoreUIController : MonoBehaviour
{
    [Header("Textos do Placar (UI)")]
    [SerializeField] private Text player1ScoreText; // Se usar TMPro, mude para: private TMP_Text player1ScoreText;
    [SerializeField] private Text player2ScoreText; // Se usar TMPro, mude para: private TMP_Text player2ScoreText;

    private int player1Score = 0;
    private int player2Score = 0;
    private bool needsUIUpdate = false;

    // Evento que avisa o servidor quando um ponto é marcado
    public delegate void OnScoreChanged(int p1, int p2);
    public event OnScoreChanged OnScoreUpdated;

    private void Start()
    {
        UpdateScoreDisplay();
    }

    private void Update()
    {
        // Atualiza a UI na Main Thread da Unity
        if (needsUIUpdate)
        {
            UpdateScoreDisplay();
            needsUIUpdate = false;
        }
    }

    /// <summary>
    /// Chamado pelas áreas de gol (GoalArea) quando a bola marca ponto.
    /// </summary>
    /// <param name="playerNumber">1 para Jogador 1, 2 para Jogador 2</param>
    public void AddGoalToPlayer(int playerNumber)
    {
        if (playerNumber == 1)
        {
            player1Score++;
        }
        else if (playerNumber == 2)
        {
            player2Score++;
        }

        needsUIUpdate = true;

        // Dispara o evento para o Servidor transmitir aos Clientes
        OnScoreUpdated?.Invoke(player1Score, player2Score);
    }

    /// <summary>
    /// Reinicia o placar para 0 x 0 (opcional para novas partidas)
    /// </summary>
    public void ResetScore()
    {
        player1Score = 0;
        player2Score = 0;
        needsUIUpdate = true;
        OnScoreUpdated?.Invoke(player1Score, player2Score);
    }

    private void UpdateScoreDisplay()
    {
        if (player1ScoreText != null) 
            player1ScoreText.text = player1Score.ToString();

        if (player2ScoreText != null) 
            player2ScoreText.text = player2Score.ToString();
    }
}