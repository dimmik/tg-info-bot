using static TgInfoBot.Ml.MercuryClassifier;

namespace TgInfoBot.Ml
{
    /// <summary>
    /// Seed training data for <see cref="MercuryClassifier"/>. This is a small starter set
    /// meant to be EXPANDED with real chat messages (ideally a few hundred per class) — with
    /// only a few dozen examples the model mostly learns lexical cues, not deep semantics.
    /// Positives deliberately include messages that talk about retrograde Mercury WITHOUT the
    /// literal word, since catching those is the whole reason to add a classifier.
    /// </summary>
    public static class MercuryDataset
    {
        public static IReadOnlyList<Sample> Seed { get; } = Build();

        /// <summary>
        /// Loads labeled samples from a TSV file: two tab-separated columns
        /// <c>label &lt;TAB&gt; text</c>, one per line. Label is <c>1/0</c>, <c>true/false</c>,
        /// or <c>yes/no</c> (case-insensitive). Blank lines and lines starting with '#' are
        /// skipped, so the file can carry comments and section headers. This is the intended
        /// way to grow the training set: keep a big TSV under version control or alongside the
        /// deployment and point <c>Classifier:DatasetPath</c> at it.
        /// </summary>
        public static IReadOnlyList<Sample> LoadTsv(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            var samples = new List<Sample>();
            var lineNo = 0;
            foreach (var raw in File.ReadLines(path))
            {
                lineNo++;
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                var tab = line.IndexOf('\t');
                if (tab <= 0)
                {
                    throw new FormatException($"{path}:{lineNo}: expected 'label<TAB>text'.");
                }

                var labelText = line[..tab].Trim();
                var text = line[(tab + 1)..].Trim();
                if (text.Length == 0)
                {
                    throw new FormatException($"{path}:{lineNo}: empty text.");
                }

                samples.Add(new Sample { Label = ParseLabel(labelText, path, lineNo), Text = text });
            }

            if (samples.Count == 0)
            {
                throw new FormatException($"{path}: no samples found.");
            }

            return samples;
        }

        private static bool ParseLabel(string value, string path, int lineNo) => value.ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "y" or "+" => true,
            "0" or "false" or "no" or "n" or "-" => false,
            _ => throw new FormatException($"{path}:{lineNo}: invalid label '{value}'. Use 1/0, true/false or yes/no."),
        };

        private static Sample Pos(string text) => new() { Label = true, Text = text };
        private static Sample Neg(string text) => new() { Label = false, Text = text };

        private static Sample[] Build() =>
        [
            // --- Positive: explicit mention ---
            Pos("меркурий снова ретроградный, держитесь"),
            Pos("когда закончится ретроградный меркурий?"),
            Pos("опять этот меркурий всё ломает"),
            Pos("из-за меркурия техника глючит весь день"),
            Pos("говорят, меркурий входит в ретроград на следующей неделе"),
            Pos("ретроградный меркурий начался, ждём проблем со связью"),
            Pos("меркурий в ретрограде — не подписывайте договоры"),
            Pos("сколько дней ещё этот ретроградный период у меркурия"),
            Pos("меркурий директный наконец-то, выдыхаем"),
            Pos("всё из-за меркурия, конечно же"),
            Pos("проверьте, меркурий ретроградит сейчас или нет"),
            Pos("меркурий опять пятится назад по небу"),

            // --- Positive: semantic, WITHOUT the word "меркурий" ---
            Pos("опять техника барахлит как по расписанию, всё понятно"),
            Pos("ретроград начался, вот и почта не доходит"),
            Pos("планета снова пятится, жди сбоев в переписке"),
            Pos("не подписывай важные бумаги на этой неделе, сам знаешь почему"),
            Pos("связь опять пропадает — звёзды не в духе"),
            Pos("ретроградность в разгаре, все рейсы задерживают"),
            Pos("именно поэтому нельзя сейчас запускать новые проекты, астрология против"),
            Pos("та самая планета опять развернулась, отсюда и путаница"),
            Pos("период когда всё ломается и письма теряются, снова он"),
            Pos("не время для сделок, ретроградный период же"),

            // --- Negative: unrelated chatter ---
            Neg("привет, как дела?"),
            Neg("во сколько сегодня встречаемся?"),
            Neg("купил новый телефон, очень доволен"),
            Neg("кто-нибудь смотрел вчера матч?"),
            Neg("напомни мне позвонить в банк завтра"),
            Neg("обед в час дня всех устраивает?"),
            Neg("скинь пожалуйста презентацию к пятнице"),
            Neg("на улице отличная погода, идём гулять"),
            Neg("сервер снова упал, чиню деплой"),
            Neg("венера сегодня хорошо видна на закате"),
            Neg("посоветуйте хороший сериал на выходные"),
            Neg("в магазине скидки на кофе, брать?"),
            Neg("поезд опаздывает на двадцать минут"),
            Neg("давай перенесём созвон на завтра"),
            Neg("ретро-игры на выходных — кто со мной в денди?"),
            Neg("этот ртутный градусник наконец выбросил, купил электронный"),
            Neg("отправил отчёт, посмотри когда будет время"),
            Neg("кот опять уронил цветок с подоконника"),
        ];
    }
}
