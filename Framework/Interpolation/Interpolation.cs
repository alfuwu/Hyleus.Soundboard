using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Hyleus.Soundboard.Framework.Interpolation;
public static class Interpolation {

    // delegate for easing functions
    public delegate float Easing(float t);

    // common easing functions
    public static float Linear(float t) => t;
    public static float EaseInQuad(float t) => t * t;
    public static float EaseOutQuad(float t) => t * (2 - t);
    public static float EaseInOutQuad(float t) =>
        t < 0.5f ? 2 * t * t : -1 + (4 - 2 * t) * t;

    private static Dictionary<string, Tween<object>> _tweens = [];

    public static void Update(GameTime delta) {
        List<string> keysToRemove = [];

        foreach (KeyValuePair<string, Tween<object>> kvp in _tweens) {
            Tween<object> tween = kvp.Value;
            tween.Update(delta);
            if (tween.IsFinished)
                keysToRemove.Add(kvp.Key);
        }

        foreach (string key in keysToRemove)
            _tweens.Remove(key);
    }

    public static void Cancel<T>(Tween<T> tween) {
        string key = null;
        foreach (KeyValuePair<string, Tween<object>> kvp in _tweens) {
            if (kvp.Value is Tween<T> t && t == tween) {
                key = kvp.Key;
                break;
            }
        }

        if (key != null)
            _tweens.Remove(key);
    }

    /// <summary>
    /// Underlying interpolation function.
    /// <para>Intended that you use one of the already defined <see cref="To"/> functions instead of this.</para>
    /// </summary>
    public static Tween<object> Interpolate<T>(
        string key,
        T start,
        T end,
        float duration,
        Func<T, T, float, T> lerpFunc,
        Action<T> setter,
        Easing ease = null
    ) {
        Tween<object> tween = new(
            start,
            end,
            duration,
            (o1, o2, t) => lerpFunc((T)o1, (T)o2, t),
            v => setter((T)v),
            ease
        );
        _tweens[key] = tween;
        return tween;
    }

    public static Tween<object> To(
        string key,
        Action<float> setter,
        float start,
        float end,
        float duration,
        Easing ease = null
    ) =>
        Interpolate(key, start, end, duration, float.Lerp, setter, ease);

    public static Tween<object> To(
        string key,
        Action<double> setter,
        double start,
        double end,
        float duration,
        Easing ease = null
    ) =>
        Interpolate(key, start, end, duration, (d1, d2, t) => double.Lerp(d1, d2, t), setter, ease);

    public static Tween<object> To(
        string key,
        Action<Vector2> setter,
        Vector2 start,
        Vector2 end,
        float duration,
        Easing ease = null
    ) =>
        Interpolate(key, start, end, duration, Vector2.Lerp, setter, ease);

    public static Tween<object> To(
        string key,
        Action<Color> setter,
        Color start,
        Color end,
        float duration,
        Easing ease = null
    ) =>
        Interpolate(key, start, end, duration, Color.Lerp, setter, ease);

    public static Tween<object> To(
        string key,
        Action<Quaternion> setter,
        Quaternion start,
        Quaternion end,
        float duration,
        Easing ease = null
    ) =>
        Interpolate(key, start, end, duration, Quaternion.Lerp, setter, ease);

    public static void To(
        string key,
        Action<string> setter,
        string start,
        string end,
        float duration,
        Easing ease = null
    ) {
        int maxLength = Math.Max(start.Length, end.Length);

        Interpolate(
            key,
            0,
            maxLength,
            duration,
            (s, e, t) => (int)(s + (e - s) * t),
            i => {
                char[] result = new char[maxLength];
                for (int c = 0; c < maxLength; c++)
                    if (c < i) // use character from end if index < progress, otherwise from start
                        result[c] = c < end.Length ? end[c] : ' ';
                    else
                        result[c] = c < start.Length ? start[c] : ' ';
                setter(new string(result).TrimEnd());
            },
            ease
        );
    }

    public static void ToRandom(
        string key,
        Action<string> setter,
        string start,
        string end,
        float duration,
        Easing ease = null
    ) {
        int maxLength = Math.Max(start.Length, end.Length);
        char[] result = new char[maxLength];
        for (int i = 0; i < maxLength; i++)
            result[i] = i < start.Length ? start[i] : ' ';

        // generate random reveal order
        int[] indices = [.. Enumerable.Range(0, maxLength).OrderBy(_ => Guid.NewGuid())];

        Interpolate(
            key,
            0,
            maxLength,
            duration,
            (s, e, t) => (int)(s + (e - s) * t),
            i => {
                for (int j = 0; j < i && j < indices.Length; j++) {
                    int idx = indices[j];
                    if (idx < end.Length)
                        result[idx] = end[idx];
                }
                setter(new string(result).TrimEnd());
            },
            ease
        );
    }
}
