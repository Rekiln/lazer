// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Online.API.Requests.Responses;
using osuTK.Graphics;

namespace osu.Game.Users
{
    public partial class UserCoverBackground : ModelBackedDrawable<APIUser?>
    {
        public APIUser? User
        {
            get => Model;
            set => Model = value;
        }

        protected override Drawable CreateDrawable(APIUser? user) => new Cover(user);

        protected override double LoadDelay => 300;

        /// <summary>
        /// Delay before the background is unloaded while off-screen.
        /// </summary>
        protected virtual double UnloadDelay => 5000;

        protected override DelayedLoadWrapper CreateDelayedLoadWrapper(Func<Drawable> createContentFunc, double timeBeforeLoad)
            => new DelayedLoadUnloadWrapper(createContentFunc, timeBeforeLoad, UnloadDelay)
            {
                RelativeSizeAxes = Axes.Both,
            };

        [LongRunningLoad]
        private partial class Cover : CompositeDrawable
        {
            private readonly APIUser? user;
            private Sprite sprite = null!;
            private CancellationTokenSource? cancellationTokenSource;

            public Cover(APIUser? user)
            {
                this.user = user;

                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(OnlineAssetCachingStore textures, [CanBeNull] UserLookupCache? userLookupCache)
            {
                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientVertical(Color4.Black.Opacity(0.1f), Color4.Black.Opacity(0.75f))
                    },
                    sprite = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        FillMode = FillMode.Fill,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre
                    }
                };

                if (user != null)
                {
                    if (!string.IsNullOrEmpty(user.CoverUrl))
                    {
                        sprite.Texture = textures.Get(user.CoverUrl);
                    }
                    else if (user.OnlineID > 1 && userLookupCache != null)
                    {
                        cancellationTokenSource = new CancellationTokenSource();

                        userLookupCache.GetUserAsync(user.OnlineID, cancellationTokenSource.Token)
                                       .ContinueWith(task =>
                                       {
                                           var apiUser = task.GetResultSafely();

                                           if (apiUser != null && !string.IsNullOrEmpty(apiUser.CoverUrl))
                                           {
                                               Schedule(() =>
                                               {
                                                   sprite.Texture = textures.Get(apiUser.CoverUrl);
                                               });
                                           }
                                       }, cancellationTokenSource.Token);
                    }
                }
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                this.FadeInFromZero(400);
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                cancellationTokenSource?.Cancel();
                cancellationTokenSource?.Dispose();
            }
        }
    }
}
