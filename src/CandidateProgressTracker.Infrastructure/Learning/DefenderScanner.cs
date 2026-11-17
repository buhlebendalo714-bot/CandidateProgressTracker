using System.Diagnostics;
using System.Security.Cryptography;
using CandidateProgressTracker.Application.Learning;
namespace CandidateProgressTracker.Infrastructure.Learning;
public sealed class DefenderScanner : IMalwareScanner
{
    public async Task<MalwareResult> ScanAsync(Stream content,CancellationToken ct)
    {
        if(!OperatingSystem.IsWindows()) return MalwareResult.Unavailable;
        var platform=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Microsoft","Windows Defender","Platform");
        string? executable;
        try { executable=Directory.Exists(platform) ? Directory.GetDirectories(platform).OrderByDescending(x=>x,StringComparer.Ordinal).Select(x=>Path.Combine(x,"MpCmdRun.exe")).FirstOrDefault(File.Exists) : null; }
        catch(UnauthorizedAccessException) { return MalwareResult.Unavailable; }
        if(executable is null) return MalwareResult.Unavailable;
        var folder=Path.Combine(Path.GetTempPath(),"CptScan_"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var path=Path.Combine(folder,"payload.bin");
        try
        {
            await using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)) { await content.CopyToAsync(file,ct); await file.FlushAsync(ct); }
            var hash=SHA256.HashData(await File.ReadAllBytesAsync(path,ct));
            using var process=new Process {StartInfo=new ProcessStartInfo(executable) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}};
            foreach(var arg in new[] {"-Scan","-ScanType","3","-File",path,"-DisableRemediation"}) process.StartInfo.ArgumentList.Add(arg);
            if(!process.Start()) return MalwareResult.Unavailable;
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(60));
            var output=process.StandardOutput.ReadToEndAsync(timeout.Token); var error=process.StandardError.ReadToEndAsync(timeout.Token);
            try { await process.WaitForExitAsync(timeout.Token); await Task.WhenAll(output,error); }
            catch(OperationCanceledException) { if(!process.HasExited) process.Kill(true); return MalwareResult.Unavailable; }
            if(process.ExitCode!=0 || !File.Exists(path)) return MalwareResult.Unavailable;
            var after=SHA256.HashData(await File.ReadAllBytesAsync(path,ct));
            if(!hash.AsSpan().SequenceEqual(after)) return MalwareResult.Unavailable;
            return MalwareResult.Clean;
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or OperationCanceledException) { return MalwareResult.Unavailable; }
        finally { try { if(File.Exists(path)) File.Delete(path); Directory.Delete(folder); } catch(IOException) {} catch(UnauthorizedAccessException) {} }
    }
}
