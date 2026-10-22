namespace MicroLIMS.Application.DTOs.Search;

public record GlobalSearchRequest(
    string Query,
    string? Category = null,
    int Limit = 5);

public record GlobalSearchResultDto(
    string Query,
    int TotalMatches,
    long ExecutionTimeMs,
    List<GlobalSearchCategoryDto> Categories);

public record GlobalSearchCategoryDto(
    string CategoryName,
    int Count,
    List<GlobalSearchItemDto> Items);

public record GlobalSearchItemDto(
    int Id,
    string EntityType,
    string Title,
    string Subtitle,
    string Status,
    string MatchedField,
    string MatchedValue,
    string BadgeColor,
    string NavigationTarget,
    Dictionary<string, string>? Metadata = null);
