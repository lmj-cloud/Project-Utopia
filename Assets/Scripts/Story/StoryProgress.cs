using System.Collections.Generic;
using UnityEngine;

public class StoryProgress : MonoBehaviour
{
    [SerializeField] private List<string> completedFlags = new();

    public bool HasFlag(string flag)
    {
        return !string.IsNullOrWhiteSpace(flag) && completedFlags.Contains(flag);
    }

    // UnityEvent의 문자열 인수로 "ReadLetter" 같은 이름을 지정한다.
    public void SetFlag(string flag)
    {
        if (!string.IsNullOrWhiteSpace(flag) && !completedFlags.Contains(flag))
            completedFlags.Add(flag);
    }

    public void RemoveFlag(string flag) => completedFlags.Remove(flag);
    public List<string> CaptureFlags() => new List<string>(completedFlags);

    public void ClearProgress() => completedFlags.Clear();
}
