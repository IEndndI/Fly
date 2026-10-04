using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterStage : MonoBehaviour
{
    public RectTransform stageRoot;      // 角色生成在哪个物体下面
    public Image characterPrefab;        // 立绘预制体
    public Vector2 defaultPos = new Vector2(960, 540); // 新角色没写位置时站的地方

    // 名字 → 这个角色的 Image，台上有谁都记在这里
    private Dictionary<string, Image> onStage = new Dictionary<string, Image>();

    // 让某个角色显示某张图；hasPos 为 true 时顺便移动到 aePos
    public void Show(string characterName, Sprite sprite, Sprite[] lines, bool hasPos, Vector2 aePos, float height)
    {
        Image img;
        if (!onStage.TryGetValue(characterName, out img))
        {
            // 台上还没有这个人：按预制体生成一个
            img = Instantiate(characterPrefab, stageRoot);
            img.name = characterName;
            onStage[characterName] = img;

            // 新登场又没写位置，就站默认位置
            if (!hasPos)
            {
                hasPos = true;
                aePos = defaultPos;
            }
        }

        img.sprite = sprite;
        if (hasPos) img.rectTransform.anchoredPosition = AEToUnity(aePos);

        img.sprite = sprite;
        img.GetComponentInChildren<BoilSprite>().SetFrames(lines);           // 空1
        if (height > 0)
        {
            float width = height * sprite.rect.width / sprite.rect.height;    // 空2：保持原图比例
            img.rectTransform.sizeDelta = new Vector2(width, height);         // 空3
        }
        if (hasPos) img.rectTransform.anchoredPosition = AEToUnity(aePos);
    
    }

    public void Hide(string characterName)
    {
        if (onStage.TryGetValue(characterName, out Image img))
        {
            Destroy(img.gameObject);
            onStage.Remove(characterName);
        }
        else
        {
            Debug.LogWarning("台上没有这个角色，无法隐藏：" + characterName);
        }
    }

    public void HideAll()
    {
        foreach (Image img in onStage.Values)
        {
            Destroy(img.gameObject);
        }
        onStage.Clear();
    }

    // 给效果用：按名字找到这个角色的 RectTransform，找不到返回 null
    public RectTransform GetTarget(string characterName)
    {
        if (onStage.TryGetValue(characterName, out Image img)) return img.rectTransform;
        return null;
    }

    // AE 坐标（左上角原点，y 向下）→ Unity 坐标（y 向上）
    Vector2 AEToUnity(Vector2 aePos)
    {
        return new Vector2(aePos.x, -aePos.y);
    }
}