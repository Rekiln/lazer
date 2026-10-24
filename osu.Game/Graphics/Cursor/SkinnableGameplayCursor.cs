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
    /// A ruleset-agnostic skin cursor that can be used outside gameplay.
    /// Skin textures are rendered at their native size and centred on the mouse
    /// position, while GameplayCursorSize controls their scale.
    /// </summary>
    public partial class SkinnableGameplayCursor : CompositeDrawable
    {
        public const float BASE_SIZE = 50f;

        private const float pressed_scale = 1.2f;

        private Container scaleContainer = null!;
        private IBindable<float> gameplayCursorSize = null!;
        private float expandFactor = 1f;

        /// <summary>
        /// Current gameplay cursor size including the pressed-state expansion.
        /// Used to keep the menu trail in sync with the cursor head.
        /// </summary>
        public float CursorScale => gameplayCursorSize.Value;
        public float ExpandedScale => expandFactor;
        public float CurrentScale => gameplayCursorSize.Value * expandFactor;

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
            expandFactor = pressed_scale;
            scaleContainer
                .ScaleTo(targetScale(1f))
                .ScaleTo(targetScale(expandFactor), 400, Easing.OutElasticHalf);
        }

        public void Contract()
        {
            expandFactor = 1f;
            scaleContainer.ScaleTo(targetScale(expandFactor), 400, Easing.OutQuad);
        }

        private void updateScale() => scaleContainer.Scale = targetScale(expandFactor);

        private Vector2 targetScale(float factor) => new Vector2(gameplayCursorSize.Value * factor);

        private void rebuildSprites()
        {
            ISkin? provider = skinSource?.FindProvider(s => s.GetTexture(@"cursor") != null);
            Texture? cursorTexture = provider?.GetTexture(@"cursor");
            Texture? middleTexture = provider?.GetTexture(@"cursormiddle");

            if (cursorTexture == null)
            {
                scaleContainer.Child = null;
                return;
            }

            var stack = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Child = new Sprite
                {
                    Texture = cursorTexture,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                },
            };

            if (middleTexture != null)
            {
                stack.Add(new Sprite
                {
                    Texture = middleTexture,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
            }

            scaleContainer.Child = stack;
            updateScale();
        }
    }
}
