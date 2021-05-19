# Selective downloading

To prevent a file from being downloaded, set it's priority to DoNotDownload.
```
public async Task MarkAllFilesAsDoNotDownload (TorrentManager manager)
{
    foreach (var file in manager.Files)
        await manager.SetFilePriorityAsync (file, Priority.DoNotDownload);
}
```

You can also assign a higher, or lower, priority to some files:
```
public async Task PrioritiseTextAndSmallFiles (TorrentManager manager)
{
    foreach (var file in manager.Files) {
        if (file.Path.EndsWith (".txt", StringComparison.OrdinalIgnoreCase))
            await manager.SetFilePriorityAsync (file, Priority.Highest);
        else if (file.Length < 1 * 1024 * 1024) // files smaller than 1 megabyte
            await manager.SetFilePriorityAsync (file, Priority.High);
        else
            await manager.SetFilePriorityAsync (file, Priority.Low);
    }
}
```