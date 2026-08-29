// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;

namespace osu.Game.Online
{
    public sealed class TrustedDomainOnlineStore : OnlineStore
    {
        protected override string GetLookupUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                Logger.Log($@"Blocking non-HTTPS or invalid resource lookup: {url}", LoggingTarget.Network, LogLevel.Important);
                return string.Empty;
            }

            return url;
        }
    }
}
