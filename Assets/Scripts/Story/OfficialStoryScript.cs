using System;
using UnityEngine;

/// <summary>팀 대본의 발화 ID와 원문만 보관한다. 진행 상태나 호감도 데이터는 저장하지 않는다.</summary>
[CreateAssetMenu(menuName = "UTOPIA/Official Story Script")]
public class OfficialStoryScript : ScriptableObject
{
    [Serializable]
    public class Line
    {
        public string id;
        public int section;
        public string speaker;
        public DialogueSpeaker speakerKind;
        [TextArea] public string text;
    }
    public Line[] lines = new Line[0];
}
