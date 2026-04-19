using System.IO.Compression;

namespace AiInterview.Api.Services;

public static class KnowledgeFileSignatureValidator
{
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[][] ZipSignatures =
    [
        [0x50, 0x4B, 0x03, 0x04],
        [0x50, 0x4B, 0x05, 0x06],
        [0x50, 0x4B, 0x07, 0x08]
    ];

    public static async Task EnsureValidAsync(IFormFile file, string extension, CancellationToken cancellationToken = default)
    {
        await using var stream = file.OpenReadStream();

        switch (extension)
        {
            case "pdf":
                await EnsurePdfAsync(stream, cancellationToken);
                return;
            case "docx":
                await EnsureDocxAsync(stream, cancellationToken);
                return;
            case "txt":
            case "md":
                await EnsureTextAsync(stream, cancellationToken);
                return;
            default:
                throw new InvalidDataException("文件类型无法识别。");
        }
    }

    private static async Task EnsurePdfAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[PdfSignature.Length];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        if (read < PdfSignature.Length || !header.SequenceEqual(PdfSignature))
        {
            throw new InvalidDataException("文件内容与 PDF 扩展名不匹配。");
        }
    }

    private static async Task EnsureDocxAsync(Stream stream, CancellationToken cancellationToken)
    {
        await using var seekableStream = await CopyToSeekableStreamAsync(stream, cancellationToken);
        var header = new byte[4];
        var read = await seekableStream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        if (read < header.Length || !ZipSignatures.Any(signature => header.SequenceEqual(signature)))
        {
            throw new InvalidDataException("文件内容与 DOCX 扩展名不匹配。");
        }

        seekableStream.Position = 0;

        try
        {
            using var archive = new ZipArchive(seekableStream, ZipArchiveMode.Read, leaveOpen: true);
            var hasContentTypes = archive.GetEntry("[Content_Types].xml") is not null;
            var hasWordDocument = archive.GetEntry("word/document.xml") is not null;
            if (!hasContentTypes || !hasWordDocument)
            {
                throw new InvalidDataException("文件内容与 DOCX 扩展名不匹配。");
            }
        }
        catch (InvalidDataException)
        {
            throw new InvalidDataException("文件内容与 DOCX 扩展名不匹配。");
        }
    }

    private static async Task EnsureTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        const int SampleSize = 1024;
        var buffer = new byte[SampleSize];
        var read = await stream.ReadAsync(buffer.AsMemory(0, SampleSize), cancellationToken);
        if (buffer.Take(read).Contains((byte)0) || buffer.Take(read).Any(IsSuspiciousControlByte))
        {
            throw new InvalidDataException("文本文件包含无法接受的二进制内容。");
        }
    }

    private static bool IsSuspiciousControlByte(byte value)
    {
        return value < 0x09 || (value > 0x0D && value < 0x20);
    }

    private static async Task<MemoryStream> CopyToSeekableStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream is MemoryStream memoryStream)
        {
            memoryStream.Position = 0;
            return new MemoryStream(memoryStream.ToArray(), writable: false);
        }

        var copied = new MemoryStream();
        await stream.CopyToAsync(copied, cancellationToken);
        copied.Position = 0;
        return copied;
    }
}
