using System.Collections;
using System.Collections.Generic; // List 这个类型在这个工具箱里
using UnityEngine;
using TMPro;

public class TypewriterEffect : MonoBehaviour
{
    public TextMeshProUGUI textComponent;
    public float typingSpeed = 0.05f;

    // 用 List 装一组台词，代替原来写死的一句话
    public List<string> dialogueLines = new List<string>()
    {
        "这是第一句台词。",
        "这是第二句台词，点击后会切换到这里。",
        "这是最后一句台词了。"
    };

    private int currentLineIndex = 0; // 记录当前显示到第几句了（从 0 开始数）
    private bool isSkipping = false;
    private bool isTyping = false;

    void Start()
    {
        StartCoroutine(TypeText(dialogueLines[currentLineIndex]));
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (isTyping)
            {
                // 情况一：正在打字，点击 = 跳过动画，直接显示完整这一句
                isSkipping = true;
            }
            else
            {
                // 情况二：这一句已经显示完了，点击 = 推进到下一句
                AdvanceToNextLine();
            }
        }
    }

    void AdvanceToNextLine()
    {
        currentLineIndex++; // 索引加一，指向下一句

        // 检查有没有超出列表范围（比如已经是最后一句了）
        if (currentLineIndex < dialogueLines.Count)
        {
            StartCoroutine(TypeText(dialogueLines[currentLineIndex]));
        }
        else
        {
            Debug.Log("台词已经全部播完了。");
        }
    }

    IEnumerator TypeText(string lineToType)
    {
        textComponent.text = "";
        isTyping = true;
        isSkipping = false;

        foreach (char letter in lineToType)
        {
            if (isSkipping)
            {
                textComponent.text = lineToType;
                break;
            }

            textComponent.text += letter;
            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
    }
}