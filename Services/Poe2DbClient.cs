using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Poe2DbLookup.Models;

namespace Poe2DbLookup.Services;

public sealed class Poe2DbClient
{
    private static readonly Uri CnHomeUri = new("https://poe2db.tw/cn/");
    private static readonly Uri CdnJsonBaseUri = new("https://cdn.poe2db.tw/json/");
    private static readonly Regex HeaderScriptRegex = new(
        @"(?:(?:https:)?//cdn\.poe2db\.tw/js/)?poedb_header[^""'<>]+\.js",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public Poe2DbClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
        }
    }

    public async Task<NameIndex> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var html = await GetStringUtf8Async(CnHomeUri, referer: null, cancellationToken).ConfigureAwait(false);
        var headerUrl = FindHeaderScriptUrl(html);
        var headerJs = await GetStringUtf8Async(new Uri(headerUrl), CnHomeUri, cancellationToken).ConfigureAwait(false);

        var cn = await DownloadAutocompleteAsync("cn", FindAutocompleteFileName(headerJs, "cn"), cancellationToken).ConfigureAwait(false);
        var tw = await DownloadAutocompleteAsync("tw", FindAutocompleteFileName(headerJs, "tw"), cancellationToken).ConfigureAwait(false);
        var us = await DownloadAutocompleteAsync("us", FindAutocompleteFileName(headerJs, "us"), cancellationToken).ConfigureAwait(false);

        if (cn.Count == 0 || tw.Count == 0 || us.Count == 0)
        {
            throw new InvalidDataException("PoE2DB autocomplete 数据为空。");
        }

        var index = NameIndex.Merge(cn, tw, us);
        if (index.Records.Count == 0)
        {
            throw new InvalidDataException("三语索引为空。");
        }

        return index;
    }

    public static string FindHeaderScriptUrl(string html)
    {
        var match = HeaderScriptRegex.Match(html);
        if (!match.Success)
        {
            throw new InvalidDataException("找不到 poedb_header JS。");
        }

        var value = match.Value;
        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            return "https:" + value;
        }

        if (value.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return "https://cdn.poe2db.tw/js/" + value;
    }

    public static string FindAutocompleteFileName(string headerJs, string language)
    {
        var escapedLanguage = Regex.Escape(language);
        var regex = new Regex(
            $@"autocompletecb_{escapedLanguage}\.json[""'`]?\s*[:=]\s*[""'`]([^""'`]+\.json)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        var match = regex.Match(headerJs);
        if (!match.Success)
        {
            throw new InvalidDataException($"找不到 autocompletecb_{language} 的真实 JSON 文件名。");
        }

        return match.Groups[1].Value;
    }

    private async Task<IReadOnlyList<AutocompleteItem>> DownloadAutocompleteAsync(
        string language,
        string fileName,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(CdnJsonBaseUri, fileName);
        var referer = new Uri($"https://poe2db.tw/{language}/");
        var text = await GetStringUtf8Async(uri, referer, cancellationToken).ConfigureAwait(false);
        var items = JsonSerializer.Deserialize<List<AutocompleteItem>>(text, JsonOptions);
        if (items is null)
        {
            throw new InvalidDataException($"autocompletecb_{language} JSON 格式无效。");
        }

        return items;
    }

    private async Task<string> GetStringUtf8Async(Uri uri, Uri? referer, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0");
        if (referer is not null)
        {
            request.Headers.Referrer = referer;
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            throw new HttpRequestException($"下载失败：{uri} 返回 {(int)response.StatusCode} {response.ReasonPhrase}。");
        }

        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        // PoE2DB 的 JSON 是 UTF-8；显式按字节解码可避免 Windows PowerShell/.NET 旧路径产生中文乱码。
        return Encoding.UTF8.GetString(bytes);
    }
}
