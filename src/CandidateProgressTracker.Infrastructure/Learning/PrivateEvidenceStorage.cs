using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CandidateProgressTracker.Application.Learning;
using Microsoft.Extensions.Options;
namespace CandidateProgressTracker.Infrastructure.Learning;
public sealed class EvidenceOptions
{
    public string Root { get; set; } = "";
    public string RepositoryRoot { get; set; } = "";
    public string ScannerHost { get; set; } = "127.0.0.1";
    public int ScannerPort { get; set; } = 3310;
}
public sealed class PrivateEvidenceStorage(IOptions<EvidenceOptions> options) : IPrivateEvidenceStorage
{
    private string Root
    {
        get
        {
            var configured = options.Value.Root;
            if (string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured)) throw new IOException("Private evidence storage is not configured.");
            var path = Path.GetFullPath(configured).TrimEnd(Path.DirectorySeparatorChar);
            var repo = Path.GetFullPath(string.IsNullOrWhiteSpace(options.Value.RepositoryRoot) ? Directory.GetCurrentDirectory() : options.Value.RepositoryRoot).TrimEnd(Path.DirectorySeparatorChar);
            if (path.Equals(repo, StringComparison.OrdinalIgnoreCase) || path.StartsWith(repo + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || path.Split(Path.DirectorySeparatorChar).Contains("wwwroot", StringComparer.OrdinalIgnoreCase)) throw new IOException("Evidence must be stored outside the repository and wwwroot.");
            return path;
        }
    }
    private string ObjectPath(string key)
    {
        if (!Guid.TryParseExact(key, "N", out _)) throw new IOException("Invalid storage identity.");
        var root = Root; Directory.CreateDirectory(root);
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new IOException("Storage cannot be a link.");
        var dir = Path.Combine(root, "cpt-evidence-v1"); Directory.CreateDirectory(dir);
        if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) throw new IOException("Storage cannot be a link.");
        var path = Path.Combine(dir, key + ".object");
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Storage cannot be a link.");
        return path;
    }
    public async Task<StoredEvidence> StoreAsync(string name, Stream input, CancellationToken ct)
    {
        name = Path.GetFileName(name.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name.Any(char.IsControl)) throw new InvalidDataException("Rename the file using a name of up to 255 characters without control characters, then upload it again.");
        var key = Guid.NewGuid().ToString("N"); var path = ObjectPath(key);
        // Durable ownership marker permits safe orphan reconciliation after a crash.
        // A foreign file with a similar name is never sufficient proof of ownership.
        await using (var marker = new FileStream(path + ".staging", FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        { await marker.WriteAsync(Encoding.ASCII.GetBytes("CPT-EVIDENCE-V1:" + key), ct); marker.Flush(true); }
        try
        {
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                var buffer = new byte[65536]; long count = 0; int read;
                while ((read = await input.ReadAsync(buffer, ct)) > 0)
                { count += read; if (count > FileLimits.PerFile) throw new InvalidDataException("Each file must be 10 MB or smaller."); await output.WriteAsync(buffer.AsMemory(0, read), ct); }
                await output.FlushAsync(ct); output.Flush(true);
            }
            var bytes = await File.ReadAllBytesAsync(path, ct);
            var type = FileContent.Validate(name, bytes);
            return new(key, name, type, bytes.LongLength, Convert.ToHexString(SHA256.HashData(bytes)));
        }
        catch { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".staging")) File.Delete(path + ".staging"); throw; }
    }
    public async Task<Stream> OpenVerifiedAsync(string key, long length, string hash, CancellationToken ct)
    {
        // Read into a bounded snapshot: scanner and downloader never see different
        // bytes through a replaceable filesystem path or a concurrent writer.
        await using var file = new FileStream(ObjectPath(key), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length != length || length is <= 0 or > FileLimits.PerFile) throw new IOException("Evidence integrity failure.");
        var bytes = new byte[(int)length]; await file.ReadExactlyAsync(bytes, ct);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(hash, StringComparison.Ordinal)) throw new IOException("Evidence integrity failure.");
        return new MemoryStream(bytes, false);
    }
    public Task DeleteAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var path=ObjectPath(key);
        if(File.Exists(path+".hold")) throw new IOException("Evidence has an explicit storage hold.");
        if(File.Exists(path) && (!File.Exists(path+".staging") || File.ReadAllText(path+".staging")!="CPT-EVIDENCE-V1:"+key)) throw new IOException("Storage ownership cannot be established.");
        // Exclusive open refuses deletion while a writer/reader owns the object.
        if(File.Exists(path)) { using(var check=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None)) {} File.Delete(path); }
        if(File.Exists(path+".staging")) File.Delete(path+".staging"); return Task.CompletedTask;
    }
    public IEnumerable<(string Key, DateTimeOffset Created)> OwnedObjects()
    {
        var dir = Path.GetDirectoryName(ObjectPath(Guid.Empty.ToString("N")))!;
        foreach (var file in Directory.EnumerateFiles(dir, "*.object", SearchOption.TopDirectoryOnly))
            if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out _) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0
                && File.Exists(file+".staging") && !File.Exists(file+".hold")
                && (File.GetAttributes(file+".staging") & FileAttributes.ReparsePoint)==0
                && File.ReadAllText(file+".staging")=="CPT-EVIDENCE-V1:"+Path.GetFileNameWithoutExtension(file))
                yield return (Path.GetFileNameWithoutExtension(file), File.GetLastWriteTimeUtc(file+".staging"));
    }
}
internal static class FileContent
{
    internal static string Validate(string name, byte[] b)
    {
        if (b.Length == 0) throw new InvalidDataException("Empty files are not accepted.");
        var ext = Path.GetExtension(name).ToLowerInvariant();
        if (ext == ".pdf")
        {
            var text = Encoding.Latin1.GetString(b);
            var decoded = Regex.Replace(text, "#([0-9a-fA-F]{2})", m => ((char)Convert.ToInt32(m.Groups[1].Value,16)).ToString());
            if (!Regex.IsMatch(text, @"\A%PDF-[12]\.\d") || !Regex.IsMatch(text, @"%%EOF\s*\z") || !text.Contains("endobj")
                || Regex.IsMatch(decoded, @"/(Encrypt|JavaScript|JS|Launch|EmbeddedFile|OpenAction|AA|RichMedia|XFA)")) throw new InvalidDataException("This PDF is damaged, password-protected or contains active content. Export a standard PDF without a password and upload it again.");
            var xref = Regex.Match(text, @"startxref\s+(\d+)\s+%%EOF\s*\z");
            if (!xref.Success || !int.TryParse(xref.Groups[1].Value, out var offset) || offset < 8 || offset >= b.Length
                || !(text[offset..].StartsWith("xref") || Regex.IsMatch(text[offset..], @"\A\d+\s+\d+\s+obj"))) throw new InvalidDataException("This PDF could not be read safely. Export a new standard PDF and upload it again.");
            return "application/pdf";
        }
        if (ext == ".png" && b.AsSpan().StartsWith(new byte[] {137,80,78,71,13,10,26,10}))
        {
            var pos = 8; var data = false; var header = false;
            while (pos + 12 <= b.Length)
            {
                var n = BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(pos)); if (n > b.Length-pos-12) break;
                var len = (int)n; var type = Encoding.ASCII.GetString(b,pos+4,4);
                uint crc = 0xffffffff; foreach (var value in b.AsSpan(pos+4,len+4)) { crc ^= value; for (var bit=0;bit<8;bit++) crc = (crc & 1)!=0 ? (crc>>1)^0xedb88320 : crc>>1; }
                if (~crc != BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(pos+8+len))) break;
                if (!header && (type != "IHDR" || len != 13)) break;
                if (type == "IHDR") { if (header || BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(pos+8)) is 0 or > 20000 || BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(pos+12)) is 0 or > 20000) break; header = true; }
                if (type == "IDAT") data = true;
                pos += len+12;
                if (type == "IEND" && len == 0 && pos == b.Length && data) return "image/png";
            }
            throw new InvalidDataException("This PNG could not be read safely. Save a new PNG or JPEG and upload it again.");
        }
        if (ext is ".jpg" or ".jpeg" && b.Length > 8 && b[0]==255 && b[1]==216 && b[^2]==255 && b[^1]==217)
        {
            var pos=2; var frame=false;
            while(pos+4 < b.Length)
            {
                if(b[pos++]!=255) break; while(pos<b.Length && b[pos]==255) pos++;
                var marker=b[pos++]; if(marker==0xda && frame) return "image/jpeg";
                var len=BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(pos)); if(len<2 || pos+len>b.Length) break;
                if(marker is 0xc0 or 0xc1 or 0xc2 && len>=8) frame=true;
                pos+=len;
            }
        }
        throw new InvalidDataException("Upload a valid PDF, PNG or JPEG file. Renaming a different file type does not convert it.");
    }
}
