using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class HealthBar : MonoBehaviour
{
    public TextMeshProUGUI text;
    public RectTransform actualHealth;

    private void Update()
    {
        PlayerController player = GameManager.instance.humanPlayer;
        if (player)
        {
            int health = Mathf.Clamp(player.health, 0, 100);
            text.text = health.ToString();
            actualHealth.localScale = new Vector2(health / player.maxHealth, 1);
        }
    }
}
