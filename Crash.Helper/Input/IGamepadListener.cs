using System;

namespace Crash.Helper.Input
{
    public interface IGamepadListener : IDisposable
    {
        event EventHandler<GamepadButtonEventArgs> ButtonPressed;
        event EventHandler<GamepadButtonEventArgs> ButtonReleased;
    }
}
