using System;
using System.IO;
using System.IO.Compression;
using Rei2D;

namespace Matrix.CoreGame;

public static class OszDropHandler
{
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        App.OnFileDropped += OnFileDropped;
        Console.WriteLine("[OszDropHandler] Drag-and-drop listener initialized. Drop an .osz file onto the window.");
    }

    private static void OnFileDropped(string path)
    {
        if (!path.EndsWith(".osz", StringComparison.OrdinalIgnoreCase) &&
            !path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Console.WriteLine($"[OszDropHandler] Dropped file detected: {path}");

        // 1. Process extraction in the background (non-intrusive & non-blocking!)
        App.Spawn(() =>
        {
            try
            {
                string folderName = Path.GetFileNameWithoutExtension(path);
                string targetDir = Path.Combine("include", "playlists", folderName);

                if (Directory.Exists(targetDir))
                    Directory.Delete(targetDir, true);

                Directory.CreateDirectory(targetDir);
                ZipFile.ExtractToDirectory(path, targetDir, overwriteFiles: true);

                Console.WriteLine($"[OszDropHandler] Successfully extracted '{folderName}' in background. Deferring UI update...");

                // 2. Marshal result back onto main update/draw thread
                App.Defer(() =>
                {
                    Program.RefreshBeatmaps();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OszDropHandler] Error during background OSZ extraction: {ex.Message}");
            }
        });
    }
}
