using UnityEngine;

public class HUDOverlay : MonoBehaviour
{
    private GameManager _gm;

    void Start()
    {
        _gm = GameManager.Instance;
    }

    void OnGUI()
    {
        if (_gm == null || _gm.State != GameState.Playing) return;

        // Dark background bar
        GUI.Box(new Rect(8, 8, 290, 70), "");

        GUIStyle scoreStyle = new GUIStyle(GUI.skin.label);
        scoreStyle.fontSize = 30;
        scoreStyle.fontStyle = FontStyle.Bold;
        scoreStyle.normal.textColor = Color.white;

        GUIStyle coinStyle = new GUIStyle(GUI.skin.label);
        coinStyle.fontSize = 30;
        coinStyle.fontStyle = FontStyle.Bold;
        coinStyle.normal.textColor = new Color(1f, 0.85f, 0.1f);

        GUI.Label(new Rect(18, 15, 200, 40), "Score: " + _gm.Score, scoreStyle);
        GUI.Label(new Rect(210, 15, 80, 40), "$" + _gm.Coins, coinStyle);
    }
}
