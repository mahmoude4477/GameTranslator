// SPDX-License-Identifier: AGPL-3.0-only

using GameTranslator.Core.Dubbing;

namespace GameTranslator.Translation;

/// <summary>Çevrilen parçalardan seslendirilecek cümleleri çıkarır (Mac: SpeechScript.swift).</summary>
internal static class SpeechScript
{
    /// <summary><paramref name="translations"/>[i], <paramref name="pieces"/>[i] çevrilecek bir parçaysa onun Türkçesidir.</summary>
    public static IReadOnlyList<SpokenLine> Lines(IReadOnlyList<SubtitleSegmenter.Piece> pieces, IReadOnlyList<string?> translations)
    {
        var lines = new List<SpokenLine>();
        string? speaker = null;
        var newTurn = false;
        for (var i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            if (piece.Translate)
            {
                if (i >= translations.Count || translations[i] is not { } turkish || !turkish.Any(char.IsLetter)) continue;
                lines.Add(new SpokenLine(piece.Text, turkish, speaker, newTurn));
                // Aynı konuşmanın sonraki cümleleri aynı sesle sürer.
                newTurn = false;
            }
            else if (piece.Text == "\n")
            {
                speaker = null;
                newTurn = false;
            }
            else if (SubtitleSegmenter.LabelName(piece) is { } name)
            {
                speaker = name.Length > 0 ? name : null;
                newTurn = false;
            }
            else if (piece.Text.Length > 0 && "-–—".Contains(piece.Text[0]))
            {
                speaker = null;
                newTurn = true;
            }
        }
        return lines;
    }
}
