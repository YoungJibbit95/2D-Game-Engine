using Game.Core.Entities;
using Game.Core.Settings;
using Microsoft.Xna.Framework.Input;

namespace Game.Client.Input;

public static class PlayerCommandBuilder
{
    public static PlayerCommand Build(InputManager input)
    {
        return Build(input, new KeyBindingSettings());
    }

    public static PlayerCommand Build(InputManager input, KeyBindingSettings bindings)
    {
        var moveAxis = 0f;

        if (input.IsBindingDown(bindings.MoveLeft))
        {
            moveAxis -= 1f;
        }

        if (input.IsBindingDown(bindings.MoveRight))
        {
            moveAxis += 1f;
        }

        var jump = input.IsBindingDown(bindings.Jump);
        return new PlayerCommand(moveAxis, jump);
    }
}
