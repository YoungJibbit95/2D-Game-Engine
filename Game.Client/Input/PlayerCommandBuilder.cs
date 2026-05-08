using Game.Core.Entities;
using Microsoft.Xna.Framework.Input;

namespace Game.Client.Input;

public static class PlayerCommandBuilder
{
    public static PlayerCommand Build(InputManager input)
    {
        var moveAxis = 0f;

        if (input.IsKeyDown(Keys.A) || input.IsKeyDown(Keys.Left))
        {
            moveAxis -= 1f;
        }

        if (input.IsKeyDown(Keys.D) || input.IsKeyDown(Keys.Right))
        {
            moveAxis += 1f;
        }

        var jump = input.IsKeyDown(Keys.Space) || input.IsKeyDown(Keys.W) || input.IsKeyDown(Keys.Up);
        return new PlayerCommand(moveAxis, jump);
    }
}
