// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Configuration;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Graphics.Cursor
{
    /// <summary>
    /// A ruleset-agnostic skin cursor which follows the legacy gameplay cursor's visual rules
    /// while remaining usable from osu.Game (without a dependency on osu.Game.Rulesets.Osu).
    /// </summary>
    public partial class SkinnableGameplayCursor : CompositeDrawable
    {
        public const float BASE_SIZE = 50f;
        private const float pressed_scale = 1.3f;
        private const int revolution_duration = 10000;

        private Container scaleContainer = null!;
        private Sprite cursorSprite = null!;
        private IBindable<float> gameplayCursorSize = null!;
        private bool cursorExpand = true;

        /// <summary>The unexpanded gameplay cursor size multiplier.</summary>
        public float CursorScale => gameplayCursorSize.Value;

        /// <summary>The scale of the cursor head itself, matching gameplay's CurrentExpandedScale.</summary>
        public float ExpandedScale => cursorSprite?.Scale.X ?? 1f;

        /// <summary>The combined head scale, useful for diagnostics only.</summary>
        public float CurrentScale => CursorScale * ExpandedScale;

        /// <summary>The current skin cursor rotation, used for rotating trail pieces.</summary>
        public float CurrentRotation => cursorSprite?.Rotation ?? 0f;

        [Resolved(canBeNull: true)]
        private ISkinSource? skinSource { get; set; }

        public SkinnableGameplayCursor()
        {
            Size = new Vector2(BASE_SIZE);
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            gameplayCursorSize = config.GetBindable<float>(OsuSetting.GameplayCursorSize);

            InternalChild = scaleContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };

            rebuildSprites();
            gameplayCursorSize.BindValueChanged(_ => updateScale(), true);

            if (skinSource != null)
                skinSource.SourceChanged += onSkinSourceChanged;
        }

        private void onSkinSourceChanged() => Schedule(rebuildSprites);

        protected override void Dispose(bool isDisposing)
        {
            if (skinSource != null)
                skinSource.SourceChanged -= onSkinSourceChanged;

            base.Dispose(isDisposing);
        }

        public void Expand()
        {
            if (!cursorExpand || cursorSprite == null)
                return;

            // Gameplay expands the cursor head, not the whole cursor stack.
            cursorSprite.ScaleTo(1f).ScaleTo(pressed_scale, 100, Easing.Out);
        }

        public void Contract()
        {
            if (cursorSprite != null)
                cursorSprite.ScaleTo(1f, 100, Easing.Out);
        }

        private void updateScale() => scaleContainer.Scale = new Vector2(gameplayCursorSize.Value);

        private void rebuildSprites()
        {
            ISkin? provider = skinSource?.FindProvider(s => s.GetTexture(@"cursor") != null);
            Texture? cursorTexture = provider?.GetTexture(@"cursor");
            Texture? middleTexture = provider?.GetTexture(@"cursormiddle");

            cursorExpand = provider?.GetConfig<MenuCursorSkinConfiguration, bool>(MenuCursorSkinConfiguration.CursorExpand)?.Value ?? true;
            bool cursorCentre = provider?.GetConfig<MenuCursorSkinConfiguration, bool>(MenuCursorSkinConfiguration.CursorCentre)?.Value ?? true;
            bool cursorRotate = provider?.GetConfig<MenuCursorSkinConfiguration, bool>(MenuCursorSkinConfiguration.CursorRotate)?.Value ?? true;
            Anchor spriteOrigin = cursorCentre ? Anchor.Centre : Anchor.TopLeft;

            if (cursorTexture == null)
            {
                cursorSprite = null!;
                scaleContainer.Child = null;
                return;
            }

            var stack = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
            };

            cursorSprite = new Sprite
            {
                Texture = cursorTexture,
                Anchor = Anchor.Centre,
                Origin = spriteOrigin,
            };
            stack.Add(cursorSprite);

            if (middleTexture != null)
            {
                stack.Add(new Sprite
                {
                    Texture = middleTexture,
                    Anchor = Anchor.Centre,
                    Origin = spriteOrigin,
                });
            }

            scaleContainer.Child = stack;
            updateScale();

            if (cursorRotate)
                cursorSprite.Spin(revolution_duration, RotationDirection.Clockwise);
        }

        // String-based lookup avoids taking a compile-time dependency on osu.Game.Rulesets.Osu.
        private enum MenuCursorSkinConfiguration
        {
            CursorCentre,
            CursorRotate,
            CursorExpand,
        }
    }
}
