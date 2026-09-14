using UnityEngine;
using UnityEngine.UI;

public class ScoreUIController : MonoBehaviour
{
    [SerializeField] private Text player1ScoreText;
    [SerializeField] private Text player2ScoreText;

    private int player1Score;
    private int player2Score;

    private void Start()
    {
        UpdateScoreText();
    }

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

        UpdateScoreText();
    }

    public void ResetScore()
    {
        player1Score = 0;
        player2Score = 0;
        UpdateScoreText();
    }

    public int GetPlayerScore(int playerNumber)
    {
        return playerNumber == 1 ? player1Score : player2Score;
    }

    private void UpdateScoreText()
    {
        if (player1ScoreText != null)
        {
            player1ScoreText.text = player1Score.ToString();
        }

        if (player2ScoreText != null)
        {
            player2ScoreText.text = player2Score.ToString();
        }
    }
}
