using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class TimelinePlayer : MonoBehaviour
{
    public PlayableDirector director;
    public List<PlayableAsset> timelines;   // 所有演出，在 Inspector 里拖进来
    public CharacterStage stage;

    void Update()
    {
        // 临时测试用：按 T 键播放推镜头
        if (Input.GetKeyDown(KeyCode.T)) Play("推镜头");
    }

    public void Play(string timelineName)
    {
        PlayableAsset found = null;
        foreach (PlayableAsset t in timelines)
        {
            if (t.name == timelineName) found = t;
        }

        if (found == null)
        {
            Debug.LogWarning("找不到演出：" + timelineName);
            return;
        }

        director.playableAsset = found;
        BindCharacters(found);
        director.Play();
    }

    public float GetDuration(string timelineName)
    {
        foreach (PlayableAsset t in timelines)
        {
            if (t.name == timelineName) return (float)t.duration;
        }
        return 0f;
    }

    void BindCharacters(PlayableAsset asset)
{
    TimelineAsset timeline = asset as TimelineAsset;
    if (timeline == null) return;

    foreach (TrackAsset track in timeline.GetOutputTracks())
    {
        
        if (track is AnimationTrack)
        {
            RectTransform target = stage.GetTarget(track.name);
            
            if (target != null)
            {
                Animator animator = target.GetComponent<Animator>();
                
                director.SetGenericBinding(track, animator);
            }
        }
        //PlayableAsset	"可以播放的资源"，一个很宽泛的大类
        //TimelineAsset	具体的一种：一段 Timeline 演出，比如你的 推镜头、小明入场
        //TrackAsset	"轨道"这个大类，所有种类的轨道都算
        //AnimationTrack	具体的一种：动画轨道，就是你拖物体进去时选的那个
        //RectTransform	UI 物体的位置大小组件，之前用过
        //Animator	动画器组件，你刚给预制体加的那个
    }
}
}