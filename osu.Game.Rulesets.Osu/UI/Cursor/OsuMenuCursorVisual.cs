// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Game.Graphics.Cursor;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Osu.Skinning.Legacy;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI.Cursor
{
    /// <summary>
    /// The osu! gameplay cursor visuals hosted by the global menu cursor. This is deliberately
    /// not a CursorContainer: the menu remains the only cursor/input host, avoiding nested
    /// cursor coordinate transforms and preserving normal settings hit-testing.
    /// </summary>
    public partial class OsuMenuCursorVisual : CompositeDrawable, IMenuCursorVisual
    {
        private readonly Container<Drawable> fadeContainer;
        private readonly SkinnableDrawable cursorTrail;
        private readonly CursorRippleVisualiser rippleVisualiser;
        private readonly OsuCursor activeCursor;
        private readonly Bindable<bool> showTrail = new Bindable<bool>(true);

        private bool menuCursorEnabled;
        private int downCount;

        public OsuMenuCursorVisual()
        {
            RelativeSizeAxes = Axes.Both;
            InternalChild = fadeContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
                Children = new Drawable[]
                {
                    cursorTrail = new SkinnableDrawable(new OsuSkinComponentLookup(OsuSkinComponents.CursorTrail), _ => new DefaultCursorTrail(), confineMode: ConfineMode.NoScaling),
                    rippleVisualiser = new CursorRippleVisualiser(),
                    new SkinnableDrawable(new OsuSkinComponentLookup(OsuSkinComponents.CursorParticles), confineMode: ConfineMode.NoScaling),
                    activeCursor = new OsuCursor(),
                }
            };

            cursorTrail.OnSkinChanged += updateTrailScale;
        }

        [BackgroundDependencyLoader(true)]
        private void load(OsuRulesetConfigManager rulesetConfig)
        {
            rulesetConfig?.BindWith(OsuRulesetSetting.ShowCursorTrail, showTrail);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            showTrail.BindValueChanged(_ => updateTrailVisibility(), true);
            activeCursor.CursorScale.BindValueChanged(e =>
            {
                rippleVisualiser.CursorScale = new Vector2(e.NewValue);
                updateTrailScale();
            }, true);
            updateTrailScale();
            updateTrailVisibility();
        }

        // Keep receiving mouse moves while visually disabled so disjoint legacy trails track the latest position.
        // CollectTrail suppresses drawing until the menu cursor is enabled.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;

        public void SetMenuCursorEnabled(bool enabled)
        {
            if (menuCursorEnabled == enabled)
                return;

            menuCursorEnabled = enabled;
            if (!enabled)
            {
                downCount = 0;
                activeCursor.Contract();
            }

            updateTrailVisibility();
            fadeContainer.FadeTo(enabled ? 1 : 0, 100, Easing.OutQuint);
        }

        public void SetMenuCursorScale(float scale)
        {
            activeCursor.SetMenuCursorScale(scale);
            updateTrailScale();
        }

        public void SetMenuCursorPosition(Vector2 screenSpacePosition)
        {
            activeCursor.Position = ToLocalSpace(screenSpacePosition);
        }

        public void HandleMenuMouseDown()
        {
            if (!menuCursorEnabled)
                return;

            downCount++;
            updateExpandedState();
        }

        public void HandleMenuMouseUp()
        {
            if (!menuCursorEnabled)
                return;

            downCount = Math.Max(0, downCount - 1);
            updateExpandedState();
        }

        private void updateExpandedState()
        {
            if (downCount > 0)
                activeCursor.Expand();
            else
                activeCursor.Contract();
        }

        private void updateTrailScale()
        {
            if (cursorTrail.Drawable is CursorTrail trail)
            {
                float scale = activeCursor.CursorScale.Value;
                trail.CursorScale = new Vector2(scale);

                if (trail is LegacyCursorTrail legacyTrail)
                    legacyTrail.MenuCursorSizeOverride = scale;
            }
        }

        private void updateTrailVisibility()
        {
            bool shouldCollect = menuCursorEnabled && showTrail.Value;
            if (cursorTrail.Drawable is CursorTrail trail)
            {
                if (trail.CollectTrail != shouldCollect)
                {
                    trail.CollectTrail = shouldCollect;
                    trail.ResetTrail();
                }

                if (!shouldCollect)
                    trail.ResetTrail();

                cursorTrail.FadeTo(shouldCollect ? 1 : 0, 200, Easing.OutQuint);
            }
        }

        protected override void Update()
        {
            base.Update();

            if (cursorTrail.Drawable is CursorTrail trail)
            {
                trail.NewPartScale = activeCursor.CurrentExpandedScale;
                trail.PartRotation = activeCursor.CurrentRotation;
            }
        }

        private partial class DefaultCursorTrail : CursorTrail
        {
            [BackgroundDependencyLoader]
            private void load(TextureStore textures)
            {
                Texture = textures.Get(@"Cursor/cursortrail");
                Scale = new Vector2(1 / Texture.ScaleAdjust);
            }
        }
    }
}
