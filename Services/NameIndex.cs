using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.International.Converters.PinYinConverter;
using Poe2DbLookup.Models;

namespace Poe2DbLookup.Services;

public sealed class NameIndex
{
    private const uint LcMapSimplifiedChinese = 0x02000000;

    private static readonly Dictionary<char, char> TraditionalToSimplified = new()
    {
        ['遠'] = '远',
        ['擊'] = '击',
        ['寶'] = '宝',
        ['輔'] = '辅',
        ['傷'] = '伤',
        ['屬'] = '属',
        ['閃'] = '闪',
        ['電'] = '电',
        ['臺'] = '台',
        ['術'] = '术',
        ['發'] = '发',
        ['髮'] = '发',
        ['級'] = '级',
        ['體'] = '体',
        ['點'] = '点',
        ['與'] = '与',
        ['戰'] = '战',
        ['鬥'] = '斗',
        ['劍'] = '剑',
        ['護'] = '护',
        ['轉'] = '转',
        ['換'] = '换',
        ['啟'] = '启',
        ['動'] = '动',
        ['雙'] = '双',
        ['單'] = '单',
        ['靈'] = '灵',
        ['氣'] = '气',
        ['強'] = '强',
        ['無'] = '无',
        ['盡'] = '尽',
        ['風'] = '风',
        ['龍'] = '龙',
        ['壓'] = '压',
        ['獄'] = '狱',
        ['燒'] = '烧',
        ['錄'] = '录',
        ['選'] = '选',
        ['擇'] = '择',
        ['輸'] = '输',
        ['貼'] = '贴'
    };

    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int LCMapStringEx(
        string localeName,
        uint mapFlags,
        string source,
        int sourceLength,
        [Out] char[] destination,
        int destinationLength,
        nint versionInformation,
        nint reserved,
        nint sortHandle);

    private readonly List<NameRecord> _records;
    private readonly List<SearchRecord> _searchRecords;

    private NameIndex(IEnumerable<NameRecord> records)
    {
        _records = records
            .Where(record => !string.IsNullOrWhiteSpace(record.Value))
            .OrderBy(record => record.UsLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _searchRecords = _records.Select(SearchRecord.Create).ToList();
    }

    public IReadOnlyList<NameRecord> Records => _records;

    public static NameIndex Empty { get; } = new([]);

    public static NameIndex Merge(
        IEnumerable<AutocompleteItem> cnItems,
        IEnumerable<AutocompleteItem> twItems,
        IEnumerable<AutocompleteItem> usItems)
    {
        var cnByValue = ToDictionary(cnItems);
        var twByValue = ToDictionary(twItems);
        var usByValue = ToDictionary(usItems);
        var values = cnByValue.Keys
            .Concat(twByValue.Keys)
            .Concat(usByValue.Keys)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var records = new List<NameRecord>(values.Count);
        foreach (var value in values)
        {
            cnByValue.TryGetValue(value, out var cn);
            twByValue.TryGetValue(value, out var tw);
            usByValue.TryGetValue(value, out var us);

            var cnLabel = Clean(cn?.Label);
            var twLabel = Clean(tw?.Label);
            var usLabel = Clean(us?.Label);
            var type = FirstNonEmpty(cn?.Desc, tw?.Desc, us?.Desc, cn?.ClassName, tw?.ClassName, us?.ClassName);
            var className = FirstNonEmpty(cn?.ClassName, tw?.ClassName, us?.ClassName);

            records.Add(new NameRecord(
                value,
                string.IsNullOrWhiteSpace(cnLabel) ? value : cnLabel,
                string.IsNullOrWhiteSpace(twLabel) ? value : twLabel,
                string.IsNullOrWhiteSpace(usLabel) ? value : usLabel,
                type,
                className));
        }

        return new NameIndex(records);
    }

    public IReadOnlyList<NameRecord> Search(string query, int limit = 80)
    {
        query = query.Trim();
        if (query.Length == 0)
        {
            return _records.Take(limit).ToList();
        }

        var normalizedQuery = NormalizeSearchText(query);
        var queryTokens = SplitSearchTokens(normalizedQuery);
        var pinyinQuery = NormalizePinyinText(query);
        return _searchRecords
            .Select(item => new
            {
                item.Record,
                Score = MatchScore(item, query, normalizedQuery, queryTokens, pinyinQuery),
                LabelLength = ShortestMatchingLength(item, query, normalizedQuery, queryTokens, pinyinQuery)
            })
            .Where(item => item.Score < int.MaxValue)
            .OrderBy(item => item.Score)
            .ThenBy(item => item.LabelLength)
            .ThenBy(item => item.Record.UsLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Record.Value, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(item => item.Record)
            .ToList();
    }

    public static async Task<NameIndex> LoadCacheAsync(string cachePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(cachePath);
        var records = await JsonSerializer.DeserializeAsync<List<NameRecord>>(stream, CacheJsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (records is null || records.Count == 0)
        {
            throw new InvalidDataException("本地缓存为空。");
        }

        return new NameIndex(records);
    }

    public async Task SaveCacheAsync(string cachePath, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(cachePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(cachePath);
        await JsonSerializer.SerializeAsync(stream, _records, CacheJsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, AutocompleteItem> ToDictionary(IEnumerable<AutocompleteItem> items)
    {
        return items
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .GroupBy(item => item.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }

    private static int MatchScore(
        SearchRecord item,
        string query,
        string normalizedQuery,
        string[] queryTokens,
        string pinyinQuery)
    {
        if (item.Fields.Any(field => string.Equals(field, query, StringComparison.OrdinalIgnoreCase))
            || item.NormalizedFields.Any(field => string.Equals(field, normalizedQuery, StringComparison.Ordinal)))
        {
            return 0;
        }

        if (item.Fields.Any(field => field.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            || item.NormalizedFields.Any(field => field.StartsWith(normalizedQuery, StringComparison.Ordinal)))
        {
            return 1;
        }

        if (item.Fields.Any(field => field.Contains(query, StringComparison.OrdinalIgnoreCase))
            || item.NormalizedFields.Any(field => field.Contains(normalizedQuery, StringComparison.Ordinal)))
        {
            return 2;
        }

        if (queryTokens.Length > 1
            && item.NormalizedFields.Any(field => ContainsOrderedTokens(field, queryTokens, StringComparison.Ordinal)))
        {
            return 3;
        }

        if (pinyinQuery.Length > 0
            && item.PinyinFields.Any(field => string.Equals(field, pinyinQuery, StringComparison.Ordinal)))
        {
            return 3;
        }

        if (pinyinQuery.Length > 0
            && item.PinyinFields.Any(field => field.StartsWith(pinyinQuery, StringComparison.Ordinal)))
        {
            return 4;
        }

        if (pinyinQuery.Length > 0
            && item.PinyinFields.Any(field => field.Contains(pinyinQuery, StringComparison.Ordinal)))
        {
            return 5;
        }

        return int.MaxValue;
    }

    private static int ShortestMatchingLength(
        SearchRecord item,
        string query,
        string normalizedQuery,
        string[] queryTokens,
        string pinyinQuery)
    {
        var labelLength = item.Fields
            .Select((field, index) => new
            {
                Field = field,
                NormalizedField = item.NormalizedFields[index]
            })
            .Where(field => field.Field.Contains(query, StringComparison.OrdinalIgnoreCase)
                || field.NormalizedField.Contains(normalizedQuery, StringComparison.Ordinal)
                || (queryTokens.Length > 1
                    && ContainsOrderedTokens(field.NormalizedField, queryTokens, StringComparison.Ordinal)))
            .Select(field => field.Field.Length)
            .DefaultIfEmpty(int.MaxValue)
            .Min();

        if (labelLength < int.MaxValue
            || pinyinQuery.Length == 0
            || !item.PinyinFields.Any(field => field.Contains(pinyinQuery, StringComparison.Ordinal)))
        {
            return labelLength;
        }

        return item.Fields
            .Select(field => field.Length)
            .DefaultIfEmpty(int.MaxValue)
            .Min();
    }

    private static string NormalizeSearchText(string value)
    {
        var simplified = TryConvertToSimplifiedChinese(value.Trim().ToLowerInvariant());
        var chars = simplified.ToCharArray();
        for (var index = 0; index < chars.Length; index++)
        {
            if (TraditionalToSimplified.TryGetValue(chars[index], out var mapped))
            {
                chars[index] = mapped;
            }
        }

        return new string(chars);
    }

    private static string[] SplitSearchTokens(string value)
    {
        return value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool ContainsOrderedTokens(string field, string[] queryTokens, StringComparison comparison)
    {
        var searchStart = 0;
        foreach (var token in queryTokens)
        {
            var tokenIndex = field.IndexOf(token, searchStart, comparison);
            if (tokenIndex < 0)
            {
                return false;
            }

            searchStart = tokenIndex + token.Length;
        }

        return true;
    }

    private static string NormalizePinyinText(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static string[] BuildPinyinSearchFields(string field)
    {
        var normalizedField = NormalizeSearchText(field);
        if (!normalizedField.Any(ChineseChar.IsValidChar))
        {
            return [];
        }

        var full = new StringBuilder(normalizedField.Length * 4);
        var initials = new StringBuilder(normalizedField.Length);
        foreach (var ch in normalizedField)
        {
            if (TryGetPinyinSyllable(ch, out var syllable))
            {
                full.Append(syllable);
                initials.Append(syllable[0]);
                continue;
            }

            if (char.IsAsciiLetterOrDigit(ch))
            {
                full.Append(ch);
                initials.Append(ch);
            }
        }

        return new[] { full.ToString(), initials.ToString() }
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool TryGetPinyinSyllable(char ch, out string syllable)
    {
        syllable = string.Empty;
        if (!ChineseChar.IsValidChar(ch))
        {
            return false;
        }

        try
        {
            var chineseChar = new ChineseChar(ch);
            var rawPinyin = chineseChar.Pinyins
                .Take(chineseChar.PinyinCount)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

            if (string.IsNullOrWhiteSpace(rawPinyin))
            {
                return false;
            }

            syllable = NormalizePinyinSyllable(rawPinyin);
            return syllable.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizePinyinSyllable(string value)
    {
        var normalized = value
            .Trim()
            .ToLowerInvariant()
            .Replace("u:", "v", StringComparison.Ordinal)
            .Replace('ü', 'v');
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (char.IsAsciiLetter(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static string TryConvertToSimplifiedChinese(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        try
        {
            var buffer = new char[value.Length * 2 + 8];
            var length = LCMapStringEx(
                "zh-CN",
                LcMapSimplifiedChinese,
                value,
                value.Length,
                buffer,
                buffer.Length,
                0,
                0,
                0);

            return length > 0 ? new string(buffer, 0, length) : value;
        }
        catch
        {
            return value;
        }
    }

    private static string Clean(string? value) => value?.Trim() ?? string.Empty;

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.Select(Clean).FirstOrDefault(value => value.Length > 0) ?? string.Empty;
    }

    private sealed record SearchRecord(
        NameRecord Record,
        string[] Fields,
        string[] NormalizedFields,
        string[] PinyinFields)
    {
        public static SearchRecord Create(NameRecord record)
        {
            var fields = new[] { record.CnLabel, record.TwLabel, record.UsLabel, record.Value };
            return new SearchRecord(
                record,
                fields,
                fields.Select(NormalizeSearchText).ToArray(),
                fields.SelectMany(BuildPinyinSearchFields).Distinct(StringComparer.Ordinal).ToArray());
        }
    }
}
