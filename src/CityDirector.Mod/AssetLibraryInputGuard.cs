using Colossal.UI;
using Game.Input;
using Game.SceneFlow;
using Game.Tools;
using UnityEngine;

namespace CityDirector
{
    // Own barriers/listeners only: never toggle another UI's global enabled/focus state.
    internal sealed class AssetLibraryInputGuard
    {
        private InputBarrier barrier;
        private UIView guardedView;
        private bool pendingRelease;
        private int releaseAfterFrame;
        private static readonly string script = ReadScript();
        internal bool Blocked => barrier?.blocked == true;

        private static string ReadScript()
        {
            using (var stream = typeof(AssetLibraryInputGuard).Assembly.GetManifestResourceStream("CityDirector.AssetLibraryInput.js"))
            using (var reader = new System.IO.StreamReader(stream)) return reader.ReadToEnd();
        }

        internal void Open()
        {
            pendingRelease = false;
            SetBarrier(true);
            // Switching tools lets the native ToolSystem stop the old tool and remove its preview.
            var world = DirectorSystem.Instance?.World;
            if (world != null && world.IsCreated)
                world.GetOrCreateSystemManaged<ToolSystem>().activeTool = world.GetOrCreateSystemManaged<DefaultToolSystem>();
            GuardNativeView();
        }

        internal void Close()
        {
            pendingRelease = true;
            releaseAfterFrame = Time.frameCount + 2;
            GUIUtility.keyboardControl = 0;
        }

        internal void Update(bool visible, bool overLauncher)
        {
            if (visible) GuardNativeView();
            // Consume the closing click/Escape all the way through key-up before returning input.
            if (pendingRelease && Time.frameCount >= releaseAfterFrame && !Input.anyKey)
            {
                RemoveNativeGuard();
                pendingRelease = false;
            }
            SetBarrier(visible || pendingRelease || overLauncher);
        }

        private void SetBarrier(bool blocked)
        {
            if (barrier == null && InputManager.instance != null)
                barrier = InputManager.instance.CreateGlobalBarrier("CityDirector.AssetLibrary");
            if (barrier != null) barrier.blocked = blocked;
        }

        private void GuardNativeView()
        {
            var view = GameManager.instance?.userInterface?.view;
            if (view == null || view.View == null || !view.View.IsReadyForBindings() || guardedView == view) return;
            RemoveNativeGuard();
            view.View.ExecuteScript(script);
            guardedView = view;
        }

        private void RemoveNativeGuard()
        {
            var view = guardedView;
            guardedView = null;
            if (view?.View != null)
                view.View.ExecuteScript("if(window.__cityDirectorInputGuard){window.__cityDirectorInputGuard();}");
        }

        internal void Dispose()
        {
            try { RemoveNativeGuard(); }
            finally { barrier?.Dispose(); barrier = null; pendingRelease = false; }
        }
    }
}
