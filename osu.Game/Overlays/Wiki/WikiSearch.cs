// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Threading;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Resources.Localisation.Web;
using osuTK;

namespace osu.Game.Overlays.Wiki
{
    public partial class WikiSearch : CompositeDrawable
    {
        private GetWikiSuggestionRequest? request;
        private ScheduledDelegate? queryChangeDebounce;

        private readonly Bindable<ResultItem[]> resultItems = new Bindable<ResultItem[]>([]);

        private WikiSearchTextBox textBox = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        public WikiSearch()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            FillFlowContainer resultContainer;

            InternalChildren = new Drawable[]
            {
                textBox = new WikiSearchTextBox
                {
                    RelativeSizeAxes = Axes.X,
                    OnTextBoxFocusLost = onTextBoxFocusLost,
                },
                resultContainer = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.TopCentre,
                    Direction = FillDirection.Vertical,
                }
            };

            textBox.Current.BindValueChanged(_ =>
            {
                queryChangeDebounce = Scheduler.AddDelayed(performRequest, 200);
            });

            resultItems.BindValueChanged(e =>
            {
                resultContainer.Children = e.NewValue;
            });
        }

        private void performRequest()
        {
            request?.Cancel();

            request = new GetWikiSuggestionRequest(textBox.Current.Value, game.CurrentLanguage.Value);
            request.Success += response => resultItems.Value = response.Select(r => new ResultItem
            {
                Highlight = r.Highlight,
                ArticleUrl = $"{api.Endpoints.WebsiteUrl}/wiki/{r.Locale}/{r.Path}",
            }).ToArray();
            request.Failure += _ => resultItems.Value = [];

            api.PerformAsync(request);
        }

        private void onTextBoxFocusLost()
        {
            request?.Cancel();
            queryChangeDebounce?.Cancel();
            resultItems.Value = [];
        }

        private partial class WikiSearchTextBox : BasicSearchTextBox
        {
            public Action? OnTextBoxFocusLost;

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
                OnTextBoxFocusLost?.Invoke();
                BorderThickness = 0;
                EdgeEffect = new EdgeEffectParameters();
            }
        }

        private partial class ResultItem : OsuClickableContainer
        {
            public required string Highlight;
            public required string ArticleUrl;

            private Box background = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            [Resolved]
            private ILinkHandler? linkHandler { get; set; }

            public ResultItem()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                OsuTextFlowContainer textFlow;

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background6,
                    },
                    textFlow = new OsuTextFlowContainer(t =>
                    {
                        t.Colour = colourProvider.Content1;
                        t.Font = t.Font.With(size: 14);
                    })
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Vertical = 5, Horizontal = 20 },
                    },
                };

                string[] textPart = Highlight.Split('*');
                textFlow.AddText(textPart[0]);
                textFlow.AddText(textPart[1], t => t.Colour = colourProvider.Light2);
                textFlow.AddText(textPart[2]);
            }

            protected override bool OnClick(ClickEvent e)
            {
                linkHandler?.HandleLink(ArticleUrl);
                return base.OnClick(e);
            }

            protected override bool OnHover(HoverEvent e)
            {
                background.Colour = colourProvider.Background3;
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.Colour = colourProvider.Background6;
                base.OnHoverLost(e);
            }
        }
    }
}
