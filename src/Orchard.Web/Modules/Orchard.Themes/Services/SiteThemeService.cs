﻿using Microsoft.Extensions.Caching.Memory;
using Orchard.Settings;
using Orchard.Environment.Extensions;
using Orchard.Environment.Extensions.Models;
using System;
using System.Threading.Tasks;
using Orchard.ContentManagement;

namespace Orchard.Themes.Services
{
    public class SiteThemeService : ISiteThemeService
    {
        private const string CacheKey = "CurrentThemeName";

        private readonly IExtensionManager _extensionManager;
        private readonly ISiteService _siteService;

        private readonly IMemoryCache _memoryCache;

        public SiteThemeService(
            ISiteService siteService,
            IExtensionManager extensionManager,
            IMemoryCache memoryCache
            )
        {
            _siteService = siteService;
            _extensionManager = extensionManager;
            _memoryCache = memoryCache;
        }

        public async Task<ExtensionDescriptor> GetSiteThemeAsync()
        {
            string currentThemeName = await GetCurrentThemeNameAsync();
            if (String.IsNullOrEmpty(currentThemeName))
            {
                return null;
            }

            return _extensionManager.GetExtension(currentThemeName);
        }

        public async Task SetSiteThemeAsync(string themeName)
        {
            var site = await _siteService.GetSiteSettingsAsync();
            (site as IContent).ContentItem.Content.CurrentThemeName = themeName;
            _memoryCache.Set(CacheKey, themeName);
            await _siteService.UpdateSiteSettingsAsync(site);
        }

        public async Task<string> GetCurrentThemeNameAsync()
        {
            string themeName;
            if (!_memoryCache.TryGetValue(CacheKey, out themeName))
            {
                var site = await _siteService.GetSiteSettingsAsync();
                themeName = (string)(site as IContent).ContentItem.Content.CurrentThemeName;
                _memoryCache.Set(CacheKey, themeName);
            }

            return themeName;
        }
    }
}
