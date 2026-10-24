// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;

namespace osu.Game.Graphics.Cursor
{
    /// <summary>
    /// Lightweight skin-based menu cursor trail. Samples the cursortrail texture
    /// along the mouse path and fades each sample out, without pulling the osu
    /// ruleset's shader-based gameplay trail into osu.Game.
    /// </summary>
    public partial class SkinnableMenuCursorTrail : Container
    {
        private const double fade_duration = 500;

        private Texture? currentTexture;
        private Vector2? lastPosition;
        private float distanceSinceLastPart;

        public SkinnableMenuCursorTrail()
        {
            RelativeSizeAxes = Axes.Both;
        }

        /// <param name="screenPosition">Current mouse position in screen space.</param>
        /// <param name="texture">Active skin's cursortrail texture.</param>
        /// <param name="cursorScale">Current gameplay cursor scale.</param>
        public void AddTrail(Vector2 screenPosition, Texture? texture, float cursorScale)
        {
            if (texture == null)
            {
                lastPosition = null;
                distanceSinceLastPart = 0;
                return;
            }

            if (currentTexture != texture)
            {
                Clear();
                currentTexture = texture;
                lastPosition = null;
                distanceSinceLastPart = 0;
            }

            Vector2 position = ToLocalSpace(screenPosition);

            if (!lastPosition.HasValue)
            {
                lastPosition = position;
                return;
            }

            Vector2 previous = lastPosition.Value;
            Vector2 difference = position - previous;
            float distance = difference.Length;

            if (distance > 0.01f)
            {
                Vector2 direction = difference / distance;
                float interval = MathF.Max(4f, texture.DisplayWidth * MathF.Max(cursorScale, 0.1f) / 2.5f);
                float firstPartDistance = interval - distanceSinceLastPart;

                for (float travelled = firstPartDistance; travelled < distance; travelled += interval)
                    addPart(previous + direction * travelled, texture, cursorScale);

                distanceSinceLastPart = (distanceSinceLastPart + distance) % interval;
            }

            lastPosition = position;
        }

        private void addPart(Vector2 position, Texture texture, float scale)
        {
            var part = new Sprite
            {
                Texture = texture,
                Position = position,
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                Scale = new Vector2(scale),
                Alpha = 0.75f,
                Blending = BlendingParameters.Additive,
            };

            Add(part);
            part.FadeOut(fade_duration, Easing.OutQuint).Expire();
        }
    }
}
