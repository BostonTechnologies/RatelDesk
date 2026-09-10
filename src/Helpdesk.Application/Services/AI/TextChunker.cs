using System.Collections.Generic;

namespace Helpdesk.Application.Services.AI;

public static class TextChunker
{
    public static IEnumerable<(string ChunkId, string Text)> Split(string text, int chunkSize = 500)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        var index = 0;
        for (var i = 0; i < text.Length; i += chunkSize)
        {
            var chunk = text.Substring(i, Math.Min(chunkSize, text.Length - i));
            yield return ($"{index++}", chunk);
        }
    }
}
