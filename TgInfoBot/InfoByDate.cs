using System.Text;

namespace TgInfoBot
{
    public class InfoByDate : IMessageProcessor
    {
        private (DateTimeOffset from, DateTimeOffset to)[] Dates;
        private readonly string[] Keywords;
        // Keywords after normalization, precomputed once for matching in Accept.
        private readonly string[] NormalizedKeywords;

        // Latin (and a couple of Greek) look-alikes mapped to their Cyrillic
        // counterparts, so obfuscation like "mеркурий" (Latin 'm') still matches.
        // Applied after lower-casing, so only lower-case keys are needed.
        private static readonly IReadOnlyDictionary<char, char> Homoglyphs = new Dictionary<char, char>
        {
            ['a'] = 'а', ['b'] = 'в', ['c'] = 'с', ['e'] = 'е', ['h'] = 'н',
            ['k'] = 'к', ['m'] = 'м', ['o'] = 'о', ['p'] = 'р', ['t'] = 'т',
            ['x'] = 'х', ['y'] = 'у',
            ['ё'] = 'е',
        };

        /// <summary>
        /// Optional live date provider (e.g. JPL Horizons). When set, its ranges are
        /// used in <see cref="GetInfo"/> instead of the static config dates.
        /// </summary>
        public ILiveProvider? LiveProvider { get; set; }
        private readonly string Description;
        private readonly string DescriptionFromTo;
        private readonly Emotion Emotion;
        public TimeSpan AlertTimeBefore { get; }

        public InfoByDate(string infoStr, bool requireDates = true)
        {
            // [rm,keyword1,keyword2:Description:DescriptionFromTo:Bad:3],2022-09-10:2022-10-02;2022-12-29:2023-01-18;
            if (string.IsNullOrWhiteSpace(infoStr))
            {
                throw new ArgumentException("Command configuration string cannot be empty.", nameof(infoStr));
            }

            var cleaned = infoStr.Trim(' ', '"', '\'');
            var firstComma = cleaned.IndexOf(',');
            var info = firstComma > 0 ? cleaned[..firstComma] : cleaned;
            var dates = (firstComma > 0 && firstComma < cleaned.Length - 1) ? cleaned[(firstComma + 1)..] : string.Empty;

            var infoParts = info.Trim(' ', '[', ']').Split(':', StringSplitOptions.TrimEntries);
            if (infoParts.Length < 4)
            {
                throw new FormatException("Command metadata is invalid. Expected at least 4 ':' separated fields.");
            }

            var cmd = infoParts[0];
            Keywords = cmd
                .Split('#', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Skip(1)
                .ToArray();

            NormalizedKeywords = Keywords
                .Select(Normalize)
                .Where(k => k.Length > 0)
                .ToArray();

            Description = infoParts[1];
            DescriptionFromTo = infoParts[2];
            Emotion = string.Equals(infoParts[3], "bad", StringComparison.OrdinalIgnoreCase) ? Emotion.Bad : Emotion.Good;
            AlertTimeBefore = ParseAlertTime(infoParts.Length > 4 ? infoParts[4] : "0");

            Dates = ParseDateRanges(dates)
                .OrderBy(d => d.from)
                .ToArray();

            if (requireDates && Dates.Length == 0)
            {
                throw new FormatException("No date ranges configured for command.");
            }
        }

        public string GetInfo(DateTimeOffset dateTime)
        {
            //Console.WriteLine($"lp: {LiveProvider} hasdata: {LiveProvider?.HasData}");
            var activeDates = (LiveProvider is { HasData: true })
                ? LiveProvider.Ranges
                : Dates;

            if (activeDates.Length == 0)
            {
                return $"Пока нет актуальных дат {DescriptionFromTo}. Ожидаю загрузку из JPL Horizons. " +
                    $"[сейчас у меня {DateTimeOffset.Now:yyyy.MM.dd HH:mm:sszzz}]";
            }

            var nowHappening = activeDates.FirstOrDefault(ft => ft.from <= dateTime && ft.to >= dateTime);
            if (nowHappening != default)
            {
                var liveNote = (LiveProvider is { HasData: true })
                    ? $" [данные JPL Horizons, обновлено {LiveProvider.LastUpdated:dd.MM.yyyy HH:mm}]"
                    : " [статические данные]";

                return $"{(Emotion == Emotion.Bad ? "Печаль," : "Отлично,")} {Description} в процессе, {nowHappening.from :dd.MM.yyyy} - " +
                $"{nowHappening.to :dd.MM.yyyy}. Осталось {((nowHappening.to - dateTime).TotalDays) :0.##} д. ({((nowHappening.to - dateTime).TotalHours):0.##} ч.) "+
                $"[сейчас у меня {DateTimeOffset.Now:yyyy.MM.dd HH:mm:sszzz}]{liveNote}";
            }

            // find nearest
            var after = activeDates.FirstOrDefault(d => d.from > dateTime);
            var before = activeDates.LastOrDefault(d => d.to < dateTime);
            string res = $"{(Emotion == Emotion.Bad ? "Ура" : "Эх")}! Сейчас нет {DescriptionFromTo}! ";

            if (after != default)
            {
                res += $"До следующего {DescriptionFromTo} осталось {((after.from - dateTime).TotalDays):0.##} д. " +
                    $"({after.from:dd.MM.yyyy} - {after.to:dd.MM.yyyy}); ";

                var alertSpan = after.from - dateTime;
                if (AlertTimeBefore > TimeSpan.Zero && alertSpan > TimeSpan.Zero && alertSpan <= AlertTimeBefore)
                {
                    res += $"Внимание: событие начнется скоро (меньше чем через {AlertTimeBefore.TotalHours:0.#} ч.); ";
                }
            } 

            if (before != default)
            {
                res += $"С предыдущего {DescriptionFromTo} прошло {((dateTime - before.to).TotalDays):0.##} д. ";
            }

            var dataSource = (LiveProvider is { HasData: true })
                ? $"данные JPL Horizons, обновлено {LiveProvider.LastUpdated:dd.MM.yyyy HH:mm}"
                : "статические данные";
            res += $"[сейчас у меня {DateTime.Now:yyyy.MM.dd HH:mm:sszzz}, {dataSource}]";
            return res;
        }

        public string GetInfoNow() => GetInfo(DateTimeOffset.Now);

        public bool Accept(string s)
        {
            if (NormalizedKeywords.Length == 0 || string.IsNullOrWhiteSpace(s))
            {
                return false;
            }

            var normalized = Normalize(s);
            if (normalized.Length == 0)
            {
                return false;
            }

            foreach (var keyword in NormalizedKeywords)
            {
                if (normalized.Contains(keyword, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Normalizes text for keyword matching so that obfuscated mentions still
        /// match: lower-cases, maps look-alike Latin letters to Cyrillic, and strips
        /// everything that is not a letter or digit. The stripping collapses
        /// "spaced-out" (м е р к у р и й) and dotted (м.е.р.к.у.р.и.й) writing into a
        /// solid run, so a stem keyword like "меркури" matches any declension too.
        /// </summary>
        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                var lower = char.ToLowerInvariant(ch);
                if (Homoglyphs.TryGetValue(lower, out var mapped))
                {
                    lower = mapped;
                }

                if (char.IsLetterOrDigit(lower))
                {
                    sb.Append(lower);
                }
            }

            return sb.ToString();
        }

        private static IEnumerable<(DateTimeOffset from, DateTimeOffset to)> ParseDateRanges(string dates)
        {
            foreach (var rawRange in dates.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var bounds = rawRange.Split(':', StringSplitOptions.TrimEntries);
                if (bounds.Length != 2)
                {
                    throw new FormatException($"Invalid date range '{rawRange}'. Expected 'yyyy-MM-dd:yyyy-MM-dd'.");
                }

                if (!DateTimeOffset.TryParseExact(bounds[0], "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var from))
                {
                    throw new FormatException($"Invalid date '{bounds[0]}'. Expected format yyyy-MM-dd.");
                }

                if (!DateTimeOffset.TryParseExact(bounds[1], "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var toInclusiveDate))
                {
                    throw new FormatException($"Invalid date '{bounds[1]}'. Expected format yyyy-MM-dd.");
                }

                var to = toInclusiveDate.AddHours(23).AddMinutes(59).AddSeconds(59);
                if (from > to)
                {
                    throw new FormatException($"Invalid range '{rawRange}'. Start date must be <= end date.");
                }

                yield return (from, to);
            }
        }

        private static TimeSpan ParseAlertTime(string raw)
        {
            if (TimeSpan.TryParse(raw, out var parsedTimeSpan))
            {
                return parsedTimeSpan;
            }

            if (double.TryParse(raw, out var days))
            {
                return TimeSpan.FromDays(days);
            }

            throw new FormatException($"Invalid alert time '{raw}'. Use TimeSpan format or number of days.");
        }
    }

    public enum Emotion
    {
        Good, Bad
    }
}
