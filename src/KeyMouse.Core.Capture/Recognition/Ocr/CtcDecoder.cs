namespace KeyMouse.Recognition.Ocr;

/// <summary>
/// CTC greedy decode: logits in, text and confidence out.
///
/// Three rules, all from the original decoder:
///  * index 0 is "blank" and never becomes a character;
///  * a repeated index is one character, not two (CTC emits the same label across timesteps);
///  * confidence is the mean of the scores of the timesteps that *produced* characters - averaging
///    over the whole timeline would drown the answer in blanks and report 0.2 for a perfect read.
/// </summary>
internal static class CtcDecoder
{
    internal sealed record Result(string Text, double Confidence);

    /// <summary>
    /// `logits` is `[T, C]` row-major (T timesteps, C characters + blank). `dictionary[0]` is the
    /// blank slot, so the mapping is `dictionary[index]` for index ≥ 1.
    /// </summary>
    internal static Result Decode(float[] logits, int timesteps, int classes, IReadOnlyList<string> dictionary)
    {
        var text = new System.Text.StringBuilder();
        double sum = 0;
        int produced = 0;
        int previous = 0;

        for (int t = 0; t < timesteps; t++)
        {
            int row = t * classes;
            float best = float.MinValue;
            int bestIndex = 0;
            for (int c = 0; c < classes; c++)
            {
                float value = logits[row + c];
                if (value > best)
                {
                    best = value;
                    bestIndex = c;
                }
            }

            bool repeat = t > 0 && bestIndex == previous;
            previous = bestIndex;
            if (bestIndex == 0 || repeat) continue;

            if (bestIndex < dictionary.Count) text.Append(dictionary[bestIndex]);
            sum += Probability(best);
            produced++;
        }

        return new Result(text.ToString(), produced == 0 ? 0 : sum / produced);
    }

    /// <summary>
    /// The model's last layer is a softmax, so the value *is* a probability - except when it is a
    /// raw logit (some exports keep the logits and do the softmax in the decoder). Treating a value
    /// outside 0..1 as a logit handles both without asking which export this is.
    /// </summary>
    private static double Probability(float value) =>
        value is >= 0 and <= 1 ? value : 1.0 / (1.0 + Math.Exp(-value));

    /// <summary>
    /// The dictionary ships inside the ONNX file as the `character` metadata entry, one character
    /// per line. Blank is prepended here so index 0 is blank everywhere in this class.
    /// </summary>
    internal static List<string> ParseDictionary(string? metadata)
    {
        var dictionary = new List<string> { string.Empty };
        if (string.IsNullOrEmpty(metadata)) return dictionary;

        foreach (string line in metadata.Split('\n'))
        {
            string trimmed = line.TrimEnd('\r');
            if (trimmed.Length == 0) continue;
            dictionary.Add(trimmed);
        }
        return dictionary;
    }
}
