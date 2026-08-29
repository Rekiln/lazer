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
using osu.Game.Overlays;

namespace osu.Game.Overlays.Settings.Sections.Online
{
    public partial class ProxySettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Network Proxy";

        private Bindable<string> proxyUrl = null!;
        private Bindable<string> proxyUsername = null!;
        private Bindable<string> proxyPassword = null!;
        private OsuTextFlowContainer restartNotice = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, OverlayColourProvider colourProvider, OsuColour colours)
        {
            proxyUrl = config.GetBindable<string>(OsuSetting.ProxyUrl);
            proxyUsername = config.GetBindable<string>(OsuSetting.ProxyUsername);
            proxyPassword = config.GetBindable<string>(OsuSetting.ProxyPassword);

            Children = new Drawable[]
            {
                new SettingsTextBox
                {
                    LabelText = "URL",
                    Current = proxyUrl,
                    PlaceholderText = "http://127.0.0.1:7890",
                },
                new OsuTextFlowContainer(cp =>
                {
                    cp.Font = OsuFont.GetFont(size: 12);
                    cp.Colour = colourProvider.Content2;
                })
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Horizontal = 20, Bottom = 5 },
                }.With(t => t.AddText("Supports HTTP, HTTPS, and SOCKS5 proxy protocols.")),
                new SettingsTextBox
                {
                    LabelText = "Username",
                    Current = proxyUsername,
                    PlaceholderText = "Optional",
                },
                new SettingsPasswordTextBox
                {
                    LabelText = "Password",
                    Current = proxyPassword,
                    PlaceholderText = "Optional",
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

            restartNotice.AddText("Restart is required for proxy changes to take effect.");

            proxyUrl.ValueChanged += _ => showRestartNotice();
            proxyUsername.ValueChanged += _ => showRestartNotice();
            proxyPassword.ValueChanged += _ => showRestartNotice();
        }

        private void showRestartNotice() => restartNotice.FadeIn(250, Easing.OutQuint);
    }
}
