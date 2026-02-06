using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SDL2;

namespace Hyleus.Soundboard.Framework.Structs;
public class TextBox() {
    private static TextBox _focusedTextBox = null;
    private static Dictionary<Keys, double> _pressed = [];
    private static KeyboardState _keyboard;

    public string Text = string.Empty;
    public Rectangle Bounds;
    public int Caret = 0;
    public int TextOffset;
    public int SelectionStart = -1;
    public int SelectionEnd;

    public bool IsFocused => _focusedTextBox == this;

    public static void ProcessInput(KeyboardState keyboard, double deltaTime) {
        if (_focusedTextBox == null)
            return;

        _keyboard = keyboard;
        
        var keys = _keyboard.GetPressedKeys();
        foreach (var k in keys) {
            if (!_pressed.ContainsKey(k)) {
                _focusedTextBox.PressKey(k);
                _pressed[k] = 0;
            } else {
                _pressed[k] += deltaTime;
                if (_pressed[k] > 0.666f) {
                    _focusedTextBox.PressKey(k);
                    _pressed[k] -= 0.066f;
                }
            }
        }
        foreach (var kvp in _pressed)
            if (!keys.Contains(kvp.Key))
                _pressed.Remove(kvp.Key);
    }

    public void Focus() {
        _focusedTextBox?.Unfocus();
        _pressed.Clear();
        _focusedTextBox = this;
    }

    public void Unfocus() {
        SelectionStart = -1;
    }

    public static int GetLastSpace(string before) {
        int i = 1;
        if (_pressed.ContainsKey(Keys.LeftControl) || _pressed.ContainsKey(Keys.RightControl))
            i = before.Length - int.Max(before[..^1].LastIndexOf(' ') + 1, 0);
        return int.Min(i, before.Length);
    }

    public static int GetNextSpace(string after) {
        int i = 1;
        if (_pressed.ContainsKey(Keys.LeftControl) || _pressed.ContainsKey(Keys.RightControl))
            i = after.Length - int.Max(after[1..].IndexOf(' ') - 1, 0);
        return int.Min(i, after.Length);
    }

    // evil demon level input hacking
    public void PressKey(Keys key) {
        string before = SelectionStart < 0 ? Text[..Caret] : Text[..SelectionStart];
        string after = SelectionStart < 0 ? Text[Caret..] : Text[SelectionEnd..];
        bool shift = _pressed.ContainsKey(Keys.LeftShift) || _pressed.ContainsKey(Keys.RightShift);
        bool control = _pressed.ContainsKey(Keys.LeftControl) || _pressed.ContainsKey(Keys.RightControl);
        switch (key) {
            case Keys.Back:
                if (SelectionStart < 0) {
                    int i = GetLastSpace(before);
                    if (before.Length > 0)
                        Text = before[..^i] + after;
                    Caret = int.Max(Caret - i, 0);
                } else {
                    Text = before + after;
                    Caret = before.Length;
                    SelectionStart = -1;
                }
                break;
            case Keys.Delete:
                int del = GetNextSpace(after);
                if (after.Length > 0)
                    Text = before + after[del..];
                break;
            case Keys.Left:
                if (shift && SelectionStart < 0)
                    SelectionEnd = Caret;
                Caret = int.Max(Caret - GetLastSpace(before), 0);
                if (shift)
                    SelectionStart = Caret;
                else
                    SelectionStart = -1;
                break;
            case Keys.Right:
                if (shift) {
                    if (SelectionStart < 0)
                        SelectionStart = Caret;
                } else {
                    SelectionStart = -1;
                }
                Caret = int.Min(Caret + GetNextSpace(after), Text.Length);
                SelectionEnd = Caret;
                break;
            case Keys.A:
                if (control) {
                    SelectionStart = 0;
                    SelectionEnd = Text.Length;
                    Caret = SelectionEnd;
                    break;
                }
                goto default;
            case Keys.V:
                if (control) {
                    // doesn't work :c
                    Text = before + SDL.SDL_GetClipboardText() + after;
                    break;
                }
                goto default;
            case Keys.Space:
                Text = before + ' ' + after;
                Caret++;
                break;
            default:
                if (control)
                    break;
                bool lower = _keyboard.CapsLock;
                if (!shift)
                    lower = !lower;

                string k = key.ToString();
                if (k.Length > 1)
                    break;
                if (lower)
                    k = k.ToString().ToLowerInvariant();
                Text = before + k + after;
                Caret++;
                break;
        }
    }
}