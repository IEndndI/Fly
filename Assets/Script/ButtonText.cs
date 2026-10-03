using UnityEngine;
using TMPro; // 引入 TextMeshPro 的工具箱，因为要操作 TMP 文本

public class ButtonTest : MonoBehaviour
{
    // 这个方法之后要绑定到按钮的点击事件上
    public TextMeshProUGUI targetText;
    public void OnButtonClick()//void：意思是"这个方法执行完之后，不返回任何结果"
    {
        
        Debug.Log("按钮被点击了！"); // Debug.Log 会把文字打印到 Unity 的 Console（控制台）窗口
        targetText.text = "文字被成功修改了！"; // 直接给 .text 属性赋新的字符串，画面就会更新
    }
    //class ButtonTest：声明一个"类"，可以理解成一张设计图纸，名字叫 ButtonTest。这张图纸描述了"一个东西应该具备哪些功能"。
//MonoBehaviour：意思是"这张图纸是照着 MonoBehaviour 这个模板改的"（专业说法叫"继承"）。MonoBehaviour 是 Unity 规定的"能挂到 GameObject 身上的脚本"必须遵守的模板——不写这个，这个脚本就没法拖到 Inspector 里挂载，Unity 不认识它。
//public：这个词管的是"谁能看到/使用这个东西"。public = 公开的，任何地方都能访问；后面还有别的权限级别（比如 private = 只有自己内部能用），但这个 demo 阶段你只需要知道：写 public 基本不会错，且是 Unity 组件类必须的写法，更细的权限区别以后遇到实际需要隐藏数据的场景我们再讲。
}