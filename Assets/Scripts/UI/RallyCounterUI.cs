using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RallyCounterUI : MonoBehaviour
{
    [SerializeField] private GameBall ball;
    [SerializeField] private TMP_Text countLabel;
    [SerializeField] private Image background;
    private int displayedCount = -1;

    private void OnEnable()
    {
        displayedCount = -1;
        Refresh();
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (countLabel == null) return;
        int count = ball != null ? ball.ReflectionCount : 0;
        if (count == displayedCount) return;
        displayedCount = count;
        bool visible = count > 0;
        countLabel.text = visible ? count.ToString() : string.Empty;
        countLabel.enabled = visible;
        if (background != null) background.enabled = true;
    }
}
