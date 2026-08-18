// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics.UserInterface;
using osu.Game.Resources.Localisation.Web;
using osuTK;

namespace osu.Game.Overlays.Wiki
{
    public partial class WikiSearch : CompositeDrawable
    {
        public WikiSearch()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new[]
            {
                new WikiSearchTextBox
                {
                    RelativeSizeAxes = Axes.X,
                }
            };
        }

        private partial class WikiSearchTextBox : BasicSearchTextBox
        {
            private const int font_size = 18;
            private const int vertical_padding = 13;
            private const int horizontal_padding = 20;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            protected override float LeftRightPadding => horizontal_padding;

            public WikiSearchTextBox()
            {
                Height = font_size + vertical_padding * 2;
                FontSize = font_size;
                PlaceholderText = CommonStrings.InputSearch;
                Placeholder.Font = Placeholder.Font.With(size: font_size);
                CornerRadius = 0;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                BackgroundFocused = colourProvider.Background6;
                BackgroundUnfocused = colourProvider.Background6;
                BorderColour = colourProvider.Highlight2;
            }

            protected override SpriteIcon CreateIcon() => base.CreateIcon().With(s =>
            {
                s.Size = new Vector2(font_size);
                s.Margin = new MarginPadding { Right = horizontal_padding };
            });

            protected override void OnFocus(FocusEvent e)
            {
                base.OnFocus(e);
                BorderThickness = 2;
                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Glow,
                    Colour = colourProvider.Highlight2,
                    Radius = 10,
                };
            }

            protected override void OnFocusLost(FocusLostEvent e)
            {
                base.OnFocusLost(e);
                BorderThickness = 0;
                EdgeEffect = new EdgeEffectParameters();
            }
        }
    }
}
