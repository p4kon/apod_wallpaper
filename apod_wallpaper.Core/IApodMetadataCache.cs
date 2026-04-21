using System;
using System.Collections.Generic;

namespace apod_wallpaper
{
    internal interface IApodMetadataCache
    {
        ApodCachedEntry Get(DateTime date);
        IReadOnlyList<ApodCachedEntry> GetRange(DateTime startDate, DateTime endDate);
        void Upsert(ApodEntry entry);
        void UpsertRange(IEnumerable<ApodEntry> entries);
        void SaveLocalImagePath(DateTime date, string localImagePath);
        void SyncLocalImagePaths();
    }
}
