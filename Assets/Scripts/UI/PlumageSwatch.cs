using UnityEngine;
using UnityEngine.UI;

public class PlumageSwatch : MonoBehaviour
{
    public Button button;
    public Image halo;
    public Image ring;
    public Image fill;

    public void Setup(Color color, string displayName)
    {
        fill.color = color;
        halo.color = color;
        gameObject.name = "Swatch_" + displayName;
    }

    public void SetSelected(bool selected)
    {
        ring.color = selected ? Color.white : FrostboundUI.Surface;
        halo.enabled = selected;
    }
}
