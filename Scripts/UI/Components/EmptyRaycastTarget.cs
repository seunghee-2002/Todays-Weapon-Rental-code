using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class EmptyRaycastTarget : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        // 빈 메시로 인해 CanvasRenderer가 cull 처리하지 않도록 강제
        canvasRenderer.cull = false;
        canvasRenderer.SetAlpha(1f);
    }

    protected override void OnCanvasHierarchyChanged()
    {
        base.OnCanvasHierarchyChanged();
        if (isActiveAndEnabled)
            canvasRenderer.cull = false;
    }

    public override bool Raycast(Vector2 sp, Camera eventCamera)
    {
        return isActiveAndEnabled
            && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, sp, eventCamera);
    }
}