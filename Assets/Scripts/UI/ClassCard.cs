using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClassCard : MonoBehaviour
{
    public Button button;
    public Image fill;
    public UIButtonFx fx;
    public RawImage thumbnail;
    public TMP_Text nameLabel;
    public TMP_Text roleLabel;

    public void SetSelected(bool selected)
    {
        if (fill != null) fill.color = selected ? FrostboundUI.SurfaceSelected : FrostboundUI.Surface;
        if (fx != null) fx.forceActive = selected;
    }
}
