using System.Reflection;
using Hyleus.Soundboard.Framework.Enums;

namespace Hyleus.Soundboard.Framework.Structs;
public struct SettingsItem(PropertyInfo prop, string name, string description, SettingsControlType controlType) {
    public readonly PropertyInfo Property { get; } = prop;
    public string Name { get; set; }= name;
    public string Description { get; set; } = description;
    public readonly SettingsControlType ControlType { get; } = controlType;

    public readonly object Get() => Property.GetValue(null);
    public readonly void Set(object value) => Property.SetValue(null, value);
}