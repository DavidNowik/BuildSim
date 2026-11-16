using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FloatingTextManager : MonoBehaviour
{
    public static FloatingTextManager Instance { get; private set; }

    [Header("References")]
    public GameObject textContainer;
    public GameObject textPrefab;

    private readonly List<FloatingText> floatingTexts = new List<FloatingText>();

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        foreach (FloatingText txt in floatingTexts)
            txt.UpdateFloatingText();
    }

    // --- Public API ---
    public void ShowText(string text, Vector3 position)
    {
        ShowText(text, 40, Color.black, position, 3f);
    }

    public void ShowText(string text, Color color, Vector3 position)
    {
        ShowText(text, 40, color, position, 3f);
    }

    public void ShowText(string text, int fontSize, Color color, Vector3 position, float duration)
    {
        FloatingText floatingText = GetFloatingText();

        floatingText.txt.text = text;
        floatingText.txt.fontSize = fontSize;
        floatingText.txt.color = color;

        floatingText.go.transform.position = Camera.main.WorldToScreenPoint(position); // World → Screen
        floatingText.duration = duration;

        floatingText.Show(position);
    }

    // --- Internal pooling ---
    private FloatingText GetFloatingText()
    {
        FloatingText txt = floatingTexts.Find(t => !t.active);
        if (txt == null)
        {
            txt = new FloatingText();
            txt.go = Instantiate(textPrefab);
            txt.go.transform.SetParent(textContainer.transform, false);
            txt.txt = txt.go.GetComponent<Text>();

            floatingTexts.Add(txt);
        }
        return txt;
    }
}
