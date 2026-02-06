using System;

namespace Hyleus.Soundboard.Framework.Structs;
public struct ContextMenuSlider {
    public string Label { get; init; }

    public float Min { get; init; }
    public float Max { get; init; }

    public Func<float> GetValue { get; init; }
    public Action<float> SetValue { get; init; }

    public bool IsDragging { get; set; }

    public float NormalizedValue {
        get => float.Clamp((GetValue() - Min) / (Max - Min), 0f, 1f);
        set => SetFromNormalized(value);
    }

    public void SetFromNormalized(float t) {
        t = float.Clamp(t, 0f, 1f);
        SetValue(Min + t * (Max - Min));
    }

    public void SetIsDragging(bool bl) => IsDragging = bl;
}
