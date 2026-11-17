using System.Net.Sockets;
using System.Buffers.Binary;
using System.Text;
using CandidateProgressTracker.Application.Learning;
using Microsoft.Extensions.Options;
namespace CandidateProgressTracker.Infrastructure.Learning;
// ClamAV INSTREAM scans the verified byte snapshot, not a mutable file path.
public sealed class ClamAvScanner(IOptions<EvidenceOptions> options) : IMalwareScanner
{
    public async Task<MalwareResult> ScanAsync(Stream content, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            using var client = new TcpClient(); await client.ConnectAsync(options.Value.ScannerHost, options.Value.ScannerPort, timeout.Token);
            await using var stream = client.GetStream(); await stream.WriteAsync(Encoding.ASCII.GetBytes("zINSTREAM\0"), timeout.Token);
            var buffer = new byte[65536]; var size = new byte[4]; int read;
            while ((read = await content.ReadAsync(buffer, timeout.Token)) > 0)
            { BinaryPrimitives.WriteInt32BigEndian(size, read); await stream.WriteAsync(size,timeout.Token); await stream.WriteAsync(buffer.AsMemory(0,read),timeout.Token); }
            await stream.WriteAsync(new byte[4],timeout.Token);
            var response = new List<byte>(); var one = new byte[1];
            while(response.Count<1024 && await stream.ReadAsync(one,timeout.Token)>0 && one[0]!=0) response.Add(one[0]);
            var answer=Encoding.ASCII.GetString(response.ToArray()).Trim();
            return answer == "stream: OK" ? MalwareResult.Clean : answer.EndsWith(" FOUND",StringComparison.Ordinal) ? MalwareResult.Unsafe : MalwareResult.Unavailable;
        }
        catch(Exception e) when(e is IOException or SocketException or OperationCanceledException) { return MalwareResult.Unavailable; }
    }
}
