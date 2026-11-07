using Colossal.Logging;
using System;
using System.IO;
using Game;
using Game.Modding;
using Game.SceneFlow;
using UnityEngine;

namespace CityDirector
{
    public sealed class Mod : IMod
    {
        public const string Version = "0.3.4";
        private GameObject libraryWindow;
        internal static string InstallDirectory { get; private set; }
        internal static readonly ILog Log = LogManager.GetLogger("CityDirector").SetShowsErrorsInUI(false);

        public void OnLoad(UpdateSystem updateSystem)
        {
            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var executable))
                InstallDirectory = Path.GetDirectoryName(executable.path);
            Trace("OnLoad entered; version=" + Version);
            AssetLibrary.Initialize();
            updateSystem.UpdateAt<DirectorSystem>(SystemUpdatePhase.UIUpdate);
            Trace("DirectorSystem registered; library=" + (AssetLibrary.Root ?? AssetLibrary.Error));
            try
            {
                libraryWindow = new GameObject("CityDirector Asset Library");
                UnityEngine.Object.DontDestroyOnLoad(libraryWindow);
                libraryWindow.AddComponent<AssetLibraryWindow>();
                Trace("Asset library UI registered");
            }
            catch (Exception e) { Trace("Asset library UI failed: " + e); Log.Error(e, "Cannot create asset library UI."); }
            Log.Info("CityDirector " + Version + ": asset library, import bridge and managed deletion registered.");
        }

        // Separate bootstrap evidence from game log verbosity and directory timestamps.
        internal static void Trace(string message)
        {
            try { File.AppendAllText(Path.Combine(InstallDirectory, "CityDirector.bootstrap.log"), DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine); }
            catch { /* Diagnostics must never prevent startup or shutdown. */ }
        }

        public void OnDispose() { DirectorSystem.Instance?.StopBridge(); if (libraryWindow != null) UnityEngine.Object.Destroy(libraryWindow); Trace("Disposed"); }
    }
}
