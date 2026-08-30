// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Threading;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Users.Drawables
{
    [LongRunningLoad]
    public partial class DrawableAvatar : Sprite
    {
        private readonly IUser user;
        private CancellationTokenSource cancellationTokenSource;

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
        private void load(LargeTextureStore textures, OnlineAssetCachingStore onlineTextures, [CanBeNull] UserLookupCache userLookupCache)
        {
            if (user != null && user.OnlineID > 1)
            {
                string avatarUrl = (user as APIUser)?.AvatarUrl;

                if (!string.IsNullOrEmpty(avatarUrl))
                {
                    Texture = onlineTextures.Get(avatarUrl);
                }
                else if (userLookupCache != null)
                {
                    cancellationTokenSource = new CancellationTokenSource();

                    userLookupCache.GetUserAsync(user.OnlineID, cancellationTokenSource.Token)
                                   .ContinueWith(task =>
                                   {
                                       var apiUser = task.GetResultSafely();
                                       string targetUrl = apiUser?.AvatarUrl;

                                       if (string.IsNullOrEmpty(targetUrl))
                                           targetUrl = $@"https://a.ppy.sh/{user.OnlineID}";

                                       Schedule(() =>
                                       {
                                           Texture = onlineTextures.Get(targetUrl);
                                       });
                                   }, cancellationTokenSource.Token);
                }
                else
                {
                    Texture = onlineTextures.Get($@"https://a.ppy.sh/{user.OnlineID}");
                }
            }

            Texture ??= textures.Get(@"Online/avatar-guest");
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            this.FadeInFromZero(300, Easing.OutQuint);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }
    }
}
