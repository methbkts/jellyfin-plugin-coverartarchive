using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using MetaBrainz.MusicBrainz.CoverArt;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CoverArtArchive
{
    /// <summary>
    /// The cover art archive image provider.
    /// </summary>
    public class CoverArtArchiveImageProvider : IRemoteImageProvider
    {
        private readonly ILogger<CoverArtArchiveImageProvider> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly JsonSerializerOptions _serializerOptions;

        private readonly CoverArt _coverArtClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="CoverArtArchiveImageProvider"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
        /// <param name="logger">Instance of the <see cref="ILogger{CoverArtArchiveImageProvider}"/> interface.</param>
        public CoverArtArchiveImageProvider(
            IHttpClientFactory httpClientFactory,
            ILogger<CoverArtArchiveImageProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters =
                {
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
                }
            };

            _coverArtClient = new CoverArt("Jellyfin Cover Art Archive Plugin", Assembly.GetExecutingAssembly().GetName().Version!.ToString(), "apps@jellyfin.org");
        }

        /// <inheritdoc />
        public string Name
            => "Cover Art Archive";

        /// <inheritdoc />
        public bool Supports(BaseItem item)
            => item is MusicAlbum;

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new[]
            {
                ImageType.Primary
            };
        }

        /// <inheritdoc />
        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            // TODO: Should probably also use the MusicBrainz library for this.
            _logger.LogDebug("GetImageResponse({Url})", url);
            var httpClient = _httpClientFactory.CreateClient(NamedClient.Default);
            return await httpClient.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();
            var musicBrainzId = item.GetProviderId(MetadataProvider.MusicBrainzAlbum);

            if (!string.IsNullOrEmpty(musicBrainzId))
            {
                try
                {
                    var release = await _coverArtClient.FetchReleaseAsync(Guid.Parse(musicBrainzId)).ConfigureAwait(false);

                    var frontImage = release.Images.SingleOrDefault(image => image.Types == CoverArtType.Front);

                    if (frontImage is not null)
                    {
                        list.Add(new RemoteImageInfo
                        {
                            ProviderName = Name,
                            Url = frontImage.Location!.ToString(),
                            Type = ImageType.Primary,
                            ThumbnailUrl = ((frontImage.Thumbnails.Large ?? frontImage.Thumbnails.Small)!).ToString(),
                            CommunityRating = frontImage.Approved ? 1 : 0,
                            RatingType = RatingType.Score,
                        });
                    }
                }
                catch (WebException e)
                {
                    HttpWebResponse response = (HttpWebResponse)e.Response!;
                    _logger.LogWarning("Got HTTP {StatusCode} when getting image for MusicBrainz release {ReleaseId}", response.StatusCode, musicBrainzId);
                }
            }

            if (list.Count == 0)
            {
                var musicBrainzGroupId = item.GetProviderId(MetadataProvider.MusicBrainzReleaseGroup);
                if (!string.IsNullOrEmpty(musicBrainzGroupId))
                {
                    try
                    {
                        var release = await _coverArtClient.FetchGroupReleaseAsync(Guid.Parse(musicBrainzGroupId)).ConfigureAwait(false);

                        var frontImage = release.Images.SingleOrDefault(image => image.Types == CoverArtType.Front);

                        if (frontImage is not null)
                        {
                            list.Add(new RemoteImageInfo
                            {
                                ProviderName = Name,
                                Url = frontImage.Location!.ToString(),
                                Type = ImageType.Primary,
                                ThumbnailUrl = ((frontImage.Thumbnails.Large ?? frontImage.Thumbnails.Small)!).ToString(),
                                CommunityRating = frontImage.Approved ? 1 : 0,
                                RatingType = RatingType.Score,
                            });
                        }
                    }
                    catch (WebException e)
                    {
                        HttpWebResponse response = (HttpWebResponse)e.Response!;
                        _logger.LogWarning("Got HTTP {StatusCode} when getting image for MusicBrainz release group {ReleaseId}", response.StatusCode, musicBrainzGroupId);
                    }
                }
            }

            return list;
        }
    }
}
