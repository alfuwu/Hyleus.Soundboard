using System;
using Hyleus.Soundboard.Framework.Structs;

namespace Hyleus.Soundboard.Framework.Extensions;
public static class EventExtensions {
    public static void RemoveAllListeners(this Ref<Delegate> @event) {
        foreach (var del in @event.Value?.GetInvocationList())
            @event.Value = Delegate.Remove(@event, del);
    }
}