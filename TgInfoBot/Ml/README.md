# Mercury text classifier (prototype)

A lightweight, in-process text classifier that answers **"is this message about
(retrograde) Mercury?"**. Built on [ML.NET](https://dotnet.microsoft.com/apps/machinelearning-ai/ml-dotnet)
(bag-of-words + SDCA logistic regression): CPU-only, tiny model, no external
services — it runs even on a single-board computer.

It **complements** the keyword matcher (`InfoByDate.Accept`): the matcher cheaply
catches literal and obfuscated spellings, while the classifier catches messages
that are *about* Mercury without containing the word.

## Pieces

- `MercuryClassifier.cs` — train / predict / save / load, plus light per-character
  normalization (case, Latin→Cyrillic homoglyphs, `ё→е`) that keeps word boundaries.
- `MercuryDataset.cs` — the built-in seed set and a `LoadTsv(path)` reader.
- `ClassifierMatcher.cs` — optional bot integration, off by default.
- `data/mercury-samples.tsv` — a starter dataset / labeling template.

## Enabling it in the bot

Off by default. Turn it on via configuration (env vars or `appsettings.json`):

```json
"Classifier": {
  "Enabled": true,
  "Threshold": 0.5,
  "FallbackCommand": "rm",
  "ModelPath": "",
  "DatasetPath": "Ml/data/mercury-samples.tsv"
}
```

- `FallbackCommand` — the name of a configured `Command_*` whose info is sent when the
  classifier fires on a message no keyword matched. **Required** for the classifier to act.
- `Threshold` — raise it (e.g. `0.7`) for fewer false positives, lower it for more recall.
- Training source priority: `ModelPath` (a saved `.zip`) → `DatasetPath` (a TSV) → built-in seed.

## Growing the dataset

Quality on a few dozen rows is mostly lexical. For real semantic coverage, grow
`data/mercury-samples.tsv` to a few **hundred rows per class**, kept roughly balanced.

Format — two tab-separated columns, one example per line:

```
1	меркурий опять ретроградит
0	во сколько сегодня обед?
```

- Label: `1/0`, `true/false`, or `yes/no`.
- Lines starting with `#` and blank lines are ignored (use them for comments/sections).

Labeling tips:
- **Positives** must include messages that mean retrograde-Mercury *without* the word
  ("опять всё ломается как по расписанию") — that is what the classifier adds over keywords.
- Add hard **negatives** on purpose: the metal mercury ("ртутный градусник"), the prefix
  "ретро" ("ретро-игры"), other planets ("венера") — so the model learns the difference.
- Prefer real messages from your chats over synthetic ones.

After growing the TSV, just restart the bot — it retrains on startup. To avoid retraining
every start, train once and save a `.zip`, then set `ModelPath`.
