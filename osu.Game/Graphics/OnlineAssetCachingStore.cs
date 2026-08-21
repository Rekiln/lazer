// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Models;
using osu.Game.Online;
using Realms;

namespace osu.Game.Graphics
{
    /// <summary>
    /// <para>
    /// Store for retrieval and caching of assets (background, avatars, covers) retrieved from the web to disk.
    /// </para>
    /// <para>
    /// This store assumes relies on the uniqueness of the URL of retrieved assets to determine identity.
    /// Therefore, this store <b>MUST</b> only be used with URLs that are content-addressed in some way
    /// (by containing a content-based hash in the filename, or a cache-busting query string based on time of last update).
    /// </para>
    /// <para>
    /// This store <b>MUST NOT</b> be used with URLs containing naive cache-busting strings (e.g. <c>test.jpg?TIMESTAMP</c>)
    /// as it both makes the caching ineffective <b>AND</b> trashes the cache with entries that will never be used again.
    /// </para>
    /// </summary>
    public class OnlineAssetCachingStore : IResourceStore<byte[]?>
    {
        private readonly RealmAccess realmAccess;
        private readonly RealmFileStore realmFileStore;
        private readonly OnlineStore onlineResourceStore;
        private readonly StorageBackedResourceStore localResourceStore;
        private readonly LargeTextureStore largeTextureStore;

        private readonly ConcurrentDictionary<string, string> cachedPaths = [];

        public OnlineAssetCachingStore(GameHost host, RealmAccess realmAccess)
        {
            this.realmAccess = realmAccess;
            realmFileStore = new RealmFileStore(realmAccess, host.Storage);
            onlineResourceStore = new TrustedDomainOnlineStore();
            localResourceStore = new StorageBackedResourceStore(realmFileStore.Storage);
            largeTextureStore = new LargeTextureStore(host.Renderer, host.CreateTextureLoaderStore(this));
        }

        public Texture? Get(string url) => largeTextureStore.Get(url);

        #region IResourceStore<byte[]>

        byte[] IResourceStore<byte[]?>.Get(string name) => throw new NotSupportedException();

        Task<byte[]?> IResourceStore<byte[]?>.GetAsync(string name, CancellationToken cancellationToken) => throw new NotSupportedException();

        /// <remarks>
        /// The reason why this class implements <see cref="IResourceStore{T}"/> explicitly
        /// instead of just wrapping <see cref="largeTextureStore"/>
        /// is that in the cache miss case, it is not desirable to wait for realm operations to complete
        /// as that will induce additional delays on devices with slow disk I/O.
        /// To this end, the online asset is retrieved, then copied off;
        /// the original requester of the asset gets the original online stream, while the realm store write happens in the background.
        /// </remarks>
        Stream? IResourceStore<byte[]?>.GetStream(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            if (cachedPaths.TryGetValue(url, out string? cachedPath))
                return localResourceStore.GetStream(cachedPath);

            string? path = realmAccess.Run(r =>
            {
                var a = r.All<RealmOnlineAsset>().Filter($@"{nameof(RealmOnlineAsset.File)}.{nameof(RealmNamedFileUsage.Filename)} == $0", url).FirstOrDefault();
                return a?.File.File.GetStoragePath();
            });

            if (path == null)
            {
                var onlineStream = onlineResourceStore.GetStream(url);

                if (onlineStream == null)
                    return null;

                var copyForAsyncWrite = new MemoryStream();
                onlineStream.CopyTo(copyForAsyncWrite);
                // relies on the underlying stream being `MemoryStream` (implementation-dependent)
                onlineStream.Seek(0, SeekOrigin.Begin);

                realmAccess.WriteAsync(r =>
                {
                    var file = realmFileStore.Add(copyForAsyncWrite, r, addToRealm: false);
                    return r.Add(new RealmOnlineAsset(file, url));
                }).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        Logger.Log($@"Failed to write {nameof(RealmOnlineAsset)} to realm: {t.Exception}", LoggingTarget.Database);
                });

                return onlineStream;
            }

            Logger.Log($"Online asset {url} retrieved from {nameof(OnlineAssetCachingStore)}.", LoggingTarget.Network);
            realmAccess.WriteAsync(r =>
            {
                var a = r.All<RealmOnlineAsset>().Filter($@"{nameof(RealmOnlineAsset.File)}.{nameof(RealmNamedFileUsage.Filename)} == $0", url).FirstOrDefault();
                if (a != null)
                    a.LastAccessed = DateTimeOffset.Now;
            }).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Logger.Log($@"Failed to update last access date of {nameof(RealmOnlineAsset)} to realm: {t.Exception}", LoggingTarget.Database);
            });

            cachedPaths.TryAdd(url, path);
            return localResourceStore.GetStream(path);
        }

        IEnumerable<string> IResourceStore<byte[]?>.GetAvailableResources() => throw new NotSupportedException();

        #endregion

        public void Dispose()
        {
            onlineResourceStore.Dispose();
            largeTextureStore.Dispose();
        }
    }
}
