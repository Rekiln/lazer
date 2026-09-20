// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Graphics;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Users.Drawables
{
    [LongRunningLoad]
    public partial class DrawableAvatar : Sprite
    {
        private readonly IUser user;

        /// <summary>
        /// A simple, non-interactable avatar sprite for the specified user.
        /// </summary>
        /// <param name="user">The user. A null value will get a placeholder avatar.</param>
        public DrawableAvatar(IUser user = null)
        {
            this.user = user;

            RelativeSizeAxes = Axes.Both;
            FillMode = FillMode.Fit;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textures, OnlineAssetCachingStore onlineTextures, IAPIProvider api)
        {
            if (user != null && user.OnlineID > 1)
            {
                string avatarUrl = (user as APIUser)?.AvatarUrl;

                if (string.IsNullOrEmpty(avatarUrl))
                {
                    string baseUrl = api?.Endpoints?.WebsiteUrl;

                    avatarUrl = !string.IsNullOrEmpty(baseUrl) && !baseUrl.EndsWith(@".ppy.sh", StringComparison.OrdinalIgnoreCase)
                        ? $@"{baseUrl}/api/v2/users/{user.OnlineID}/avatar"
                        : $@"https://a.ppy.sh/{user.OnlineID}";
                }

                Texture = onlineTextures.Get(avatarUrl);
            }

            Texture ??= textures.Get(@"Online/avatar-guest");
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            this.FadeInFromZero(300, Easing.OutQuint);
        }
    }
}
