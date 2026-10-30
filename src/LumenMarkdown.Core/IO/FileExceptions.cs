using System.Text;

namespace LumenMarkdown.Core.IO;

public sealed class FileTooLargeException(long size, long limit)
    : Exception($"The file is {size / 1024 / 1024.0:0.0} MB, larger than the {limit / 1024 / 1024} MB limit.")
{
    public long Size { get; } = size;
    public long Limit { get; } = limit;
}

public sealed class BinaryFileException()
    : Exception("This doesn't look like a text file.");

public sealed class DecodeException(string message, Exception? inner = null)
    : Exception(message, inner);

public sealed record LoadedFile(
    string Text,
    Encoding Encoding,
    bool HasBom,
    string LineEnding,
    bool IsReadOnly,
    long Length,
    bool HadDecodeErrors);
