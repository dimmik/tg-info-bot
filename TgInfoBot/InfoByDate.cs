namespace TgInfoBot
{
    public class InfoByDate
    {
        private readonly IEnumerable<(DateTimeOffset from, DateTimeOffset to)> Dates;
        private readonly string Description;
        private readonly string DescriptionFromTo;
        private readonly Emotion Emotion;
        public TimeSpan AlertTimeBefore { get; private set; }
        public InfoByDate(string infoStr)
        {
            // [rm:Ретроградный Меркурий:Ретроградного Меркурия:Bad:3],2022-09-10:2022-10-02;2022-12-29:2023-01-18;
            var strParts = infoStr.Split(',');
            var (info, dates) = (strParts[0], strParts[1]);
            var infoParts = info.Trim(' ', '[', ']').Split(':');
            (Description, DescriptionFromTo, Emotion, AlertTimeBefore) 
                = (infoParts[1], infoParts[2],
                    infoParts[3].ToLower() == "bad" ? Emotion.Bad : Emotion.Good, 
                    TimeSpan.Parse(infoParts[4]));
            Dates = dates.Split(';').Where(d => !string.IsNullOrWhiteSpace(d))
                        .Select(ft => ft.Split(':'))
                        .Select(ft => (DateTimeOffset.ParseExact(ft[0], "yyyy-MM-dd", null), DateTimeOffset.ParseExact(ft[1], "yyyy-MM-dd", null).AddHours(23).AddMinutes(59).AddSeconds(59)))
                        .ToArray()
                        ;
        }
        public string GetInfo(DateTimeOffset dateTime)
        {
            var dt = (Dates ?? Enumerable.Empty<(DateTimeOffset from, DateTimeOffset to)>());
            var NowHappening = dt
                .Where(ft => ft.from <= dateTime && ft.to >= dateTime);
            if (NowHappening.Any())
            {
                var item = NowHappening.First();
                return $"{(Emotion == Emotion.Bad ? "Печаль," : "Отлично,")} {Description} в процессе, {item.from :dd.MM.yyyy} - {item.to :dd.MM.yyyy}. Осталось {((item.to - dateTime).TotalDays) :0.##} д. ({((item.to - dateTime).TotalHours):0.##} ч.)";
            }
            // find nearest
            var after = dt.Where(d => d.from > dateTime).OrderBy(d => d.from);
            var before = dt.Where(d => d.to < dateTime).OrderByDescending(d => d.to);
            string res = $"{(Emotion == Emotion.Bad ? "Ура" : "Эх")}! Сейчас нет {DescriptionFromTo}! ";
            if (after.Any())
            {
                var item = after.First();
                res += $"До следующего {DescriptionFromTo} осталось {((item.from - dateTime).TotalDays):0.##} д. ";
            } 
            if (before.Any()) 
            {
                var item = before.First();
                res += $"С предыдущего {DescriptionFromTo} прошло {((dateTime - item.to).TotalDays):0.##} д.";
            }
            return res;
        }
        public string GetInfoNow() => GetInfo(DateTimeOffset.Now);
    }
    public enum Emotion
    {
        Good, Bad
    }
}
