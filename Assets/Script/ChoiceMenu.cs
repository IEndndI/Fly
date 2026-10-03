using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChoiceMenu : MonoBehaviour
{
    public RectTransform container;   // 按钮生成在哪里
    public Button buttonPrefab;       // 按钮预制体
    public int chosenIndex = -1;      // 玩家选了第几个，-1 表示还没选

    public void Show(List<string> texts)
    {
        chosenIndex = -1;
        for (int i = 0; i < texts.Count; i++)
        {
            int index = i;
            Button b = Instantiate(buttonPrefab, container);
            b.GetComponentInChildren<TextMeshProUGUI>().text = texts[i];
            b.onClick.AddListener(() => chosenIndex = index);
        }
    }

    public void Clear()
    {
        foreach (Transform child in container) Destroy(child.gameObject);
    }


}