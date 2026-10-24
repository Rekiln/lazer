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
    /// Skin cursor trail for menus, following the sampling rules used by osu!'s
    /// gameplay LegacyCursorTrail without depending on the osu! ruleset assembly.
    /// </summary>
    public partial class SkinnableMenuCursorTrail : Container
    {
        private const double disjoint_trail_time_separation = 1000.0 / 60.0;
        private const double disjoint_fade_duration = 150;
        private const double continuous_fade_duration = 500;

        private Texture? currentTexture;
        private Vector2? lastPosition;
        private Vector2? currentPosition;
        private float distanceSinceLastPart;
        private double lastTrailTime;
        private bool currentTrailIsDisjoint;
        private float currentCursorScale = 1;
        private float currentPartScale = 1;

        public SkinnableMenuCursorTrail()
        {
            RelativeSizeAxes = Axes.Both;
        }

        /// <summary>
        /// Updates trail sampling from the current mouse position. Skin trails without
        /// cursormiddle use time-based, non-interpolated samples; those with cursormiddle
        /// use interpolated samples along the movement path.
        /// </summary>
        public void AddTrail(Vector2 screenPosition, Texture? texture, float cursorScale, float partScale, bool disjointTrail)
        {
            if (texture == null)
            {
                resetTrail();
                return;
            }

            if (currentTexture != texture || currentTrailIsDisjoint != disjointTrail)
            {
                Clear();
                currentTexture = texture;
                currentTrailIsDisjoint = disjointTrail;
                lastPosition = null;
                currentPosition = null;
                distanceSinceLastPart = 0;
                lastTrailTime = Time.Current;
            }

            Vector2 position = ToLocalSpace(screenPosition);
            currentPosition = position;
            currentCursorScale = cursorScale;
            currentPartScale = partScale;

            // osu!'s disjoint trail does not interpolate mouse movement. It samples the
            // latest known position from Update() at roughly 60 Hz instead.
            if (disjointTrail)
            {
                lastPosition = position;
                return;
            }

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

                // Match CursorTrail.IntervalMultiplier for LegacyCursorTrail. The
                // cursor size is cancelled for sizes >= 1, keeping spacing consistent.
                float interval = texture.DisplayWidth * cursorScale / 2.5f / MathF.Max(cursorScale, 1f);
                if (interval > 0)
                {
                    float firstPartDistance = interval - distanceSinceLastPart;
                    float stopAt = distance - interval;

                    for (float travelled = firstPartDistance; travelled < stopAt; travelled += interval)
                        addPart(previous + direction * travelled, texture, cursorScale, partScale, false);

                    distanceSinceLastPart = (distanceSinceLastPart + distance) % interval;
                }
            }

            lastPosition = position;
        }

        protected override void Update()
        {
            base.Update();

            if (!currentTrailIsDisjoint || currentTexture == null || !currentPosition.HasValue)
                return;

            if (Time.Current - lastTrailTime < disjoint_trail_time_separation)
                return;

            lastTrailTime = Time.Current;
            addPart(currentPosition.Value, currentTexture, currentCursorScale, currentPartScale, true);
        }

        private void addPart(Vector2 position, Texture texture, float cursorScale, float partScale, bool disjoint)
        {
            var part = new Sprite
            {
                Texture = texture,
                Position = position,
                Origin = Anchor.Centre,
                Scale = new Vector2(cursorScale * partScale),
                Alpha = 1,
                Blending = disjoint ? BlendingParameters.Inherit : BlendingParameters.Additive,
            };

            Add(part);
            part.FadeOut(disjoint ? disjoint_fade_duration : continuous_fade_duration).Expire();
        }

        private void resetTrail()
        {
            Clear();
            currentTexture = null;
            lastPosition = null;
            currentPosition = null;
            distanceSinceLastPart = 0;
            lastTrailTime = Time.Current;
            currentCursorScale = 1;
            currentPartScale = 1;
        }
    }
}
