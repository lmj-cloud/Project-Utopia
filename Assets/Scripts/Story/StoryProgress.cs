using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스토리 진행 플래그를 조회·기록·삭제하고 저장용 목록으로 제공한다.
/// </summary>
public class StoryProgress : MonoBehaviour
{
    [SerializeField] private List<string> completedFlags = new();

    /// <summary>
    /// 비어 있지 않은 진행 플래그가 기록되어 있는지 확인한다.
    /// </summary>
    public bool HasFlag(string flag)
    {
        return !string.IsNullOrWhiteSpace(flag) && completedFlags.Contains(flag);
    }

    // UnityEvent의 문자열 인수로 "ReadLetter" 같은 이름을 지정한다.
    /// <summary>
    /// 유효한 플래그를 진행 목록에 기록한다.
    /// </summary>
    public void SetFlag(string flag)
    {
        if (!string.IsNullOrWhiteSpace(flag) && !completedFlags.Contains(flag))
            completedFlags.Add(flag);
    }

    /// <summary>
    /// 지정 플래그를 진행 목록에서 제거한다.
    /// </summary>
    public void RemoveFlag(string flag) => completedFlags.Remove(flag);
    /// <summary>
    /// 현재 진행 플래그를 저장용 새 목록으로 복사한다.
    /// </summary>
    public List<string> CaptureFlags() => new List<string>(completedFlags);

    /// <summary>
    /// 현재 기록된 모든 스토리 플래그를 초기화한다.
    /// </summary>
    public void ClearProgress() => completedFlags.Clear();
}
