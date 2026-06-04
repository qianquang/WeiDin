using WeiDin.Core.Interfaces;

namespace WeiDin.Application.Services.Knowledge;

/// <summary>
/// 文本分块服务实现
/// 策略：按段落分割 → 合并到目标大小 → 超长段落按句子边界切分 → 重叠窗口
/// </summary>
public class TextChunkingService : ITextChunkingService
{
    // 中英文句子结束符
    private static readonly char[] SentenceEndings = { '。', '！', '？', '；', '.', '!', '?', ';' };

    public List<string> Chunk(string text, int chunkSize = 500, int overlap = 50)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        // 第一步：按段落分割
        var paragraphs = text.Split(
            new[] { "\r\n\r\n", "\n\n", "\r\n", "\n" },
            StringSplitOptions.RemoveEmptyEntries);

        // 第二步：合并小段落到目标大小
        var mergedChunks = MergeParagraphs(paragraphs, chunkSize);

        // 第三步：对超长块按句子边界切分
        var finalChunks = new List<string>();
        foreach (var chunk in mergedChunks)
        {
            if (chunk.Length <= chunkSize)
            {
                finalChunks.Add(chunk);
            }
            else
            {
                finalChunks.AddRange(SplitBySentence(chunk, chunkSize));
            }
        }

        // 第四步：添加重叠
        if (overlap > 0 && finalChunks.Count > 1)
        {
            result = AddOverlap(finalChunks, overlap);
        }
        else
        {
            result = finalChunks;
        }

        return result.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList();
    }

    /// <summary>
    /// 合并小段落到不超过 chunkSize 的大块
    /// </summary>
    private static List<string> MergeParagraphs(string[] paragraphs, int chunkSize)
    {
        var result = new List<string>();
        var current = "";

        foreach (var para in paragraphs)
        {
            var trimmed = para.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            if (current.Length + trimmed.Length + 1 > chunkSize && current.Length > 0)
            {
                result.Add(current);
                current = trimmed;
            }
            else
            {
                current = string.IsNullOrEmpty(current) ? trimmed : current + "\n" + trimmed;
            }
        }

        if (!string.IsNullOrWhiteSpace(current))
        {
            result.Add(current);
        }

        return result;
    }

    /// <summary>
    /// 按句子边界切分超长文本
    /// </summary>
    private static List<string> SplitBySentence(string text, int chunkSize)
    {
        var result = new List<string>();

        // 按句子分割
        var sentences = SplitIntoSentences(text);

        var current = "";
        foreach (var sentence in sentences)
        {
            if (current.Length + sentence.Length > chunkSize && current.Length > 0)
            {
                result.Add(current.Trim());
                current = sentence;
            }
            else
            {
                current += sentence;
            }
        }

        if (!string.IsNullOrWhiteSpace(current))
        {
            result.Add(current.Trim());
        }

        // 如果单个句子仍然超长，按字符强制切分
        var finalResult = new List<string>();
        foreach (var chunk in result)
        {
            if (chunk.Length <= chunkSize)
            {
                finalResult.Add(chunk);
            }
            else
            {
                for (int i = 0; i < chunk.Length; i += chunkSize)
                {
                    var end = Math.Min(i + chunkSize, chunk.Length);
                    finalResult.Add(chunk.Substring(i, end - i));
                }
            }
        }

        return finalResult;
    }

    /// <summary>
    /// 将文本按句子分割，保留分隔符
    /// </summary>
    private static List<string> SplitIntoSentences(string text)
    {
        var sentences = new List<string>();
        var start = 0;

        for (int i = 0; i < text.Length; i++)
        {
            if (SentenceEndings.Contains(text[i]))
            {
                var sentence = text.Substring(start, i - start + 1);
                if (!string.IsNullOrWhiteSpace(sentence))
                {
                    sentences.Add(sentence);
                }
                start = i + 1;
            }
        }

        // 处理末尾没有句号的情况
        if (start < text.Length)
        {
            var remaining = text.Substring(start);
            if (!string.IsNullOrWhiteSpace(remaining))
            {
                sentences.Add(remaining);
            }
        }

        return sentences;
    }

    /// <summary>
    /// 为分块添加重叠：每个分块的开头附加上一个分块的末尾部分
    /// </summary>
    private static List<string> AddOverlap(List<string> chunks, int overlap)
    {
        var result = new List<string> { chunks[0] };

        for (int i = 1; i < chunks.Count; i++)
        {
            var prevChunk = chunks[i - 1];
            var overlapText = prevChunk.Length > overlap
                ? prevChunk.Substring(prevChunk.Length - overlap)
                : prevChunk;

            result.Add(overlapText + chunks[i]);
        }

        return result;
    }
}
