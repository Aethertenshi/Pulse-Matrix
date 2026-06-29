using System;
using System.Collections.Generic;
using Rei2D;
using Rei2D.Rendering;

namespace Matrix.CoreGame;

public class GlowBar : Container
{
    public float GlowRadius { get; set; } = 8f;
    public bool ChromaticAberration { get; set; } = true;

    public override void CollectRenderItems(List<RenderItem> items, IRenderMetrics metrics)
    {
        if (SkipDraw || !Visible || Color.A == 0) return;

        items.Add(new GlowRenderItem(Bounds, Color, GlowRadius, ChromaticAberration));

        foreach (var child in _children)
            child.CollectRenderItems(items, metrics);
    }
}
