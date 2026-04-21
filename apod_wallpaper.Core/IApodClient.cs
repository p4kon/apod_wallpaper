using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    internal interface IApodClient
    {
        ApodEntry GetEntry(DateTime date);
        Task<ApodEntry> GetEntryAsync(DateTime date);
        ApodEntry GetLatestEntry();
        Task<ApodEntry> GetLatestEntryAsync();
        IReadOnlyList<ApodEntry> GetEntries(DateTime startDate, DateTime endDate);
        Task<IReadOnlyList<ApodEntry>> GetEntriesAsync(DateTime startDate, DateTime endDate);
        Task<ApiKeyValidationState> ValidateApiKeyAsync(string apiKey);
    }
}
