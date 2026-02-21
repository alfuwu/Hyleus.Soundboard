using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SDL2;

namespace Hyleus.Soundboard.Framework.Structs;
public partial class TextBox() {
    private static TextBox _focusedTextBox = null;
    private static readonly Dictionary<Keys, double> _pressed = [];
    private static KeyboardState _keyboard;
    [GeneratedRegex(@"^(-?\d+)?(\.\d*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex Number();
    private static readonly Regex NumberRegex = Number();

    public string Text = string.Empty;
    public Rectangle Bounds;
    public int Caret = 0;
    public int TextOffset;
    public int SelectionStart = -1;
    public int SelectionEnd;
    public bool Numerical;
    public float Transparency;

    public delegate void Event();
    public delegate bool FocusedEvent(TextBox self, TextBox focusChanged);
    public delegate bool TextChangedEvent(TextBox self, string text);
    public event TextChangedEvent OnInputEntered;
    public event FocusedEvent OnFocused;
    public event FocusedEvent OnUnfocused;
    public event TextChangedEvent OnInputChanged;
    public event Event OnReset;

    public bool IsFocused => _focusedTextBox == this;

    public static void ProcessInput(KeyboardState keyboard, double deltaTime) {
        if (_focusedTextBox == null)
            return;

        _keyboard = keyboard;
        
        var keys = _keyboard.GetPressedKeys();
        foreach (var k in keys) {
            if (!_pressed.ContainsKey(k)) {
                _focusedTextBox.PressKey(k, ' ', false);
                _pressed[k] = 0;
            } else {
                _pressed[k] += deltaTime;
                if (_pressed[k] > 0.666f) {
                    _focusedTextBox.PressKey(k, ' ', false);
                    _pressed[k] -= 0.066f;
                }
            }
        }
        foreach (var kvp in _pressed)
            if (!keys.Contains(kvp.Key))
                _pressed.Remove(kvp.Key);
    }
    public static void ProcessInput(Keys key, char character) => _focusedTextBox?.PressKey(key, character);

    public void Reset() {
        Text = string.Empty;
        Caret = 0;
        SelectionStart = -1;
        OnReset?.Invoke();
    }

    public void Focus() {
        if (IsFocused)
            return;
        TextBox oldFocus = _focusedTextBox;
        if (OnFocused?.Invoke(this, oldFocus) == false || oldFocus?.Unfocus(this) == false)
            return;
        _focusedTextBox = this;
    }

    public bool Unfocus(TextBox newFocus = null) {
        if (!IsFocused || OnUnfocused?.Invoke(this, newFocus) == false)
            return false;
        _pressed.Clear();
        SelectionStart = -1;

        if (Numerical && Text.Length > 0) {
            int olen = Text.Length;
            Text = double.Parse(Text).ToString("0.00");
            Caret -= olen - Text.Length;
        }

        _focusedTextBox = null;
        return true;
    }

    public static int GetLastSpace(string before, bool control) {
        int i = 1;
        if (control)
            i = before.Length - int.Max(before[..^1].LastIndexOf(' ') + 1, 0);
        return int.Min(i, before.Length);
    }

    public static int GetNextSpace(string after, bool control) {
        int i = 1;
        int idx = after.Length > 1 ? after[1..].IndexOf(' ') : -1;
        if (!control)
            return int.Min(i, after.Length);
        i = idx == -1 ?
            after.Length :
            int.Max(idx + 2, 0);
        return int.Min(i, after.Length);
    }

    // evil demon level input hacking
    // theres gotta be a better way to do this
    // right?
    public void PressKey(Keys key, char character, bool allowDefaulting = true) {
        string before = SelectionStart < 0 ? Text[..Caret] : Text[..SelectionStart];
        string after = SelectionStart < 0 ? Text[Caret..] : Text[SelectionEnd..];
        bool shift = _keyboard.IsKeyDown(Keys.LeftShift) || _keyboard.IsKeyDown(Keys.RightShift);
        bool control = _keyboard.IsKeyDown(Keys.LeftControl) || _keyboard.IsKeyDown(Keys.RightControl);
        string oldText = Text;
        int oldCaret = Caret;

        switch (key) {
            case Keys.Back:
                if (!allowDefaulting)
                    break;
                if (SelectionStart < 0) {
                    int i = GetLastSpace(before, control);
                    if (before.Length > 0)
                        Text = before[..^i] + after;
                    Caret = int.Max(Caret - i, 0);
                    break;
                }
                Text = before + after;
                Caret = before.Length;
                SelectionStart = -1;
                break;
            case Keys.Delete:
                if (!allowDefaulting)
                    break;
                if (SelectionStart < 0) {
                    int del = GetNextSpace(after, control);
                    if (after.Length > 0)
                        Text = before + after[del..];
                    break;
                }
                Text = before + after;
                Caret = before.Length;
                SelectionStart = -1;
                break;
            case Keys.Up:
                if (shift && SelectionStart < 0)
                    SelectionEnd = Caret;
                Caret = 0;
                if (shift)
                    SelectionStart = Caret;
                else
                    SelectionStart = -1;
                break;
            case Keys.Left:
                if (shift && SelectionStart < 0)
                    SelectionEnd = Caret;
                Caret = (!shift && SelectionStart >= 0) ? SelectionStart : int.Max(Caret - GetLastSpace(before, control), 0);
                if (shift)
                    SelectionStart = Caret;
                else
                    SelectionStart = -1;
                break;
            case Keys.Down:
                if (shift) {
                    if (SelectionStart < 0)
                        SelectionStart = Caret;
                } else {
                    SelectionStart = -1;
                }
                Caret = Text.Length;
                SelectionEnd = Caret;
                break;
            case Keys.Right:
                if (shift) {
                    if (SelectionStart < 0)
                        SelectionStart = Caret;
                } else {
                    SelectionStart = -1;
                }
                Caret = int.Min(Caret + GetNextSpace(after, control), Text.Length);
                SelectionEnd = Caret;
                break;
            case Keys.Enter:
                if (OnInputEntered?.Invoke(this, Text) != false)
                    Unfocus();
                break;
            case Keys.A:
                if (control) {
                    SelectionStart = 0;
                    SelectionEnd = Text.Length;
                    Caret = SelectionEnd;
                    break;
                }
                goto default;
            case Keys.C:
                if (control) {
                    if (SelectionStart > 0)
                        SDL.SDL_SetClipboardText(Text[SelectionStart..SelectionEnd]);
                    break;
                }
                goto default;
            case Keys.V:
                if (control) {
                    // doesn't work :c
                    var clippy = SDL.SDL_GetClipboardText();
                    Text = before + clippy + after;
                    SelectionStart = -1;
                    Caret = before.Length + clippy.Length;
                    break;
                }
                goto default;
            case Keys.X:
                if (control) {
                    if (SelectionStart > 0)
                        SDL.SDL_SetClipboardText(Text[SelectionStart..SelectionEnd]);
                    Text = before + after;
                    SelectionStart = -1;
                    Caret = before.Length;
                    break;
                }
                goto default;
            default:
                if (control || !allowDefaulting)
                    break;

                Text = before + character + after;
                Caret++;
                SelectionStart = -1;
                break;
        }

        if (Numerical && !NumberRegex.IsMatch(Text)) {
            Text = oldText;
            Caret = oldCaret;
        }

        if (oldText != Text)
            OnInputChanged?.Invoke(this, oldText);
    }

    public void SubscribeUntilReset(Action subscribe, Action unsubscribe) {
        subscribe();

        OnReset += Clean;
        return;

        void Clean() {
            unsubscribe();
            OnReset -= Clean;
        }
    }
}