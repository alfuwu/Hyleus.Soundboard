using System;
using Microsoft.Xna.Framework;

namespace Hyleus.Soundboard.Framework.Interpolation;
public class Tween<T> {
    private readonly T _start;
    private readonly T _end;
    private readonly float _duration;
    private readonly Func<T, T, float, T> _lerpFunc;
    private readonly Interpolation.Easing _ease;
    public readonly Action<T> setter;

    private float _elapsed;

    public bool IsFinished => _elapsed >= _duration;

    public Tween(
        T start,
        T end,
        float duration,
        Func<T, T, float, T> lerpFunc,
        Action<T> s,
        Interpolation.Easing ease = null
    ) {
        _start = start;
        _end = end;
        _duration = duration;
        _lerpFunc = lerpFunc;
        _ease = ease ?? Interpolation.Linear;
        setter = s ?? throw new ArgumentNullException(nameof(s));
    }

    public void Cancel() => Interpolation.Cancel(this);

    public void Update(GameTime gameTime) {
        if (IsFinished) {
            setter(_end);
            return;
        }

        _elapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;
        float t = Math.Clamp(_elapsed / _duration, 0f, 1f);
        t = _ease(t);

        setter(_lerpFunc(_start, _end, t));
    }
}
