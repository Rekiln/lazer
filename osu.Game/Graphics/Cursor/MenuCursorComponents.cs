// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osuTK;

namespace osu.Game.Graphics.Cursor
{
    /// <summary>Visual components provided by a ruleset for the global menu cursor.</summary>
    public enum MenuCursorComponents
    {
        GameplayCursorVisual,
    }

    /// <summary>Adapter between the global menu cursor host and a ruleset's cursor visuals.</summary>
    public interface IMenuCursorVisual
    {
        void SetMenuCursorEnabled(bool enabled);

        void SetMenuCursorScale(float scale);

        void SetMenuCursorPosition(Vector2 screenSpacePosition);

        void HandleMenuMouseDown();

        void HandleMenuMouseUp();
    }
}
