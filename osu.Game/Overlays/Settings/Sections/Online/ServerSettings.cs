// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;

namespace osu.Game.Overlays.Settings.Sections.Online
{
    public partial class ServerSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Custom Server";

        private Bindable<string> apiUrl = null!;
        private OsuTextFlowContainer restartNotice = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, OsuColour colours)
        {
            apiUrl = config.GetBindable<string>(OsuSetting.ApiUrl);

            Children = new Drawable[]
            {
                new SettingsTextBox
                {
                    LabelText = "Custom API Server URL",
                    Current = apiUrl,
                },
                restartNotice = new OsuTextFlowContainer(cp =>
                {
                    cp.Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold);
                    cp.Colour = colours.Yellow;
                })
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Horizontal = 20, Top = 5 },
                    Alpha = 0,
                }
            };

            restartNotice.AddText("Restart is required for endpoint changes to take effect.");

            apiUrl.ValueChanged += _ => restartNotice.FadeIn(250, Easing.OutQuint);
        }
    }
}
