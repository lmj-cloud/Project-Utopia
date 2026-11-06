using System;
using UnityEngine;

public enum TutorialKeyLayout { None, WASD, Interact, MouseLeft, MouseRight, Medicine, Inventory, QuickSlot, Forward, Weapons, Knife }

/// <summary>Inspector에서 구성한 실제 키 이미지 묶음과 WASD 성공 색상만 표시한다.</summary>
public class TutorialKeyImages : MonoBehaviour
{
    [Serializable]
    private class KeyGroup
    {
        public TutorialKeyLayout layout;
        public GameObject root;
        public UnityEngine.UI.Image[] directionKeys = new UnityEngine.UI.Image[0];
    }
    [SerializeField] private KeyGroup[] groups = new KeyGroup[0];
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color completedColor = new Color(.35f, .95f, .55f, 1f);

    public void Refresh(TutorialKeyLayout layout, int directionMask)
    {
        foreach (KeyGroup group in groups)
        {
            if (group.root == null) continue;
            bool visible = group.layout == layout;
            group.root.SetActive(visible);
            if (!visible) continue;
            for (int i = 0; i < group.directionKeys.Length; i++)
            {
                if (group.directionKeys[i] != null)
                    group.directionKeys[i].color = (directionMask & (1 << i)) != 0 ? completedColor : normalColor;
            }
        }
    }
}
