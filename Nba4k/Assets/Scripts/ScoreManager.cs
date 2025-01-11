using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI scoreText;
    private int score = 0;
    
    void Awake()
    {
        if(scoreText != null)
        {
            scoreText.text = "Score: 0";  // 초기 텍스트 설정
        }
    }

    public void AddScore()
    {
        score += 2;
        UpdateScoreUI();
    }

    void UpdateScoreUI()
    {
        if(scoreText != null)
        {
            scoreText.text = $"Score: {score}";
        }
    }
}