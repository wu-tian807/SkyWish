using System;
using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Game;
using Game.City;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Simulation;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Scripting;

namespace CityDirector
{
    public sealed partial class DirectorSystem : GameSystemBase
    {
        internal static DirectorSystem Instance { get; private set; }
        private PipeBridge bridge;
        private string session = Guid.NewGuid().ToString("N");
        private EntityQuery prefabQuery;

        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            Instance = this;
            prefabQuery = GetEntityQuery(ComponentType.ReadOnly<PrefabData>());
            bridge = new PipeBridge(PipeBridge.DefaultName, message => Mod.Log.Warn(message));
            try { bridge.Start(); Mod.Trace("Bridge listening: " + PipeBridge.DefaultName); Mod.Log.Info("Observation bridge listening: " + PipeBridge.DefaultName); }
            catch (Exception e) { bridge.Dispose(); bridge = null; Mod.Trace("Bridge failed: " + e); Mod.Log.Error(e, "Cannot start observation bridge."); }
        }

        [Preserve]
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            session = Guid.NewGuid().ToString("N");
        }

        [Preserve]
        protected override void OnUpdate() { PumpImport(); PumpMenus(); bridge?.Drain(Handle); }

        private object Handle(PendingRead request)
        {
            if (request.Method == "assets.import") return StartImport((string)request.Parameters["assetId"]);
            if (request.Method == "assets.import_status") return ImportStatus((string)request.Parameters["assetId"]);
            if (request.Method == "menus.tree") return MenuTree();
            if (request.Method == "menus.assets") return MenuAssets(request.Parameters);
            if (request.Method == "menus.assign") return AssignCategory(request.Parameters);
            var manager = GameManager.instance;
            bool loaded = manager != null && manager.gameMode == GameMode.Game && !manager.isGameLoading;
            if (request.Method == "game.state")
            {
                var sim = loaded ? World.GetOrCreateSystemManaged<SimulationSystem>() : null;
                return new { session, gameMode = manager?.gameMode.ToString() ?? "Unknown", cityLoaded = loaded,
                    isLoading = manager?.isGameLoading ?? true,
                    cityName = loaded ? World.GetOrCreateSystemManaged<CityConfigurationSystem>().cityName : null,
                    paused = sim == null ? (bool?)null : sim.selectedSpeed <= 0,
                    selectedSpeed = sim == null ? (float?)null : sim.selectedSpeed,
                    activeTool = loaded ? World.GetOrCreateSystemManaged<Game.Tools.ToolSystem>().activeTool?.GetType().Name : null,
                    assetManagerOpen = AssetLibraryWindow.Instance?.IsOpen ?? false,
                    assetManagerInputBlocked = AssetLibraryWindow.Instance?.InputBlocked ?? false };
            }
            if (!loaded) throw new InvalidOperationException("Load a city before querying assets.");
            var queryToken = request.Parameters["query"];
            var limitToken = request.Parameters["limit"];
            if (queryToken != null && queryToken.Type != Newtonsoft.Json.Linq.JTokenType.String)
                throw new ArgumentException("query must be text.");
            string search = (string)queryToken ?? "";
            if (search.Length > 120) throw new ArgumentException("query exceeds 120 characters.");
            if (limitToken != null && limitToken.Type != Newtonsoft.Json.Linq.JTokenType.Integer)
                throw new ArgumentException("limit must be an integer.");
            int limit = limitToken == null ? 20 : (int)limitToken;
            if (limit < 1 || limit > 100) throw new ArgumentException("limit must be between 1 and 100.");
            var prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            var matches = new List<object>();
            int total = 0;
            using (var entities = prefabQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (var entity in entities)
                {
                    var prefab = prefabs.GetPrefab<PrefabBase>(entity);
                    if (prefab == null || prefab.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    total++;
                    if (matches.Count < limit) matches.Add(new { name = prefab.name, type = prefab.GetType().Name });
                }
            }
            return new { session, total, returned = matches.Count, truncated = total > matches.Count, assets = matches,
                note = "Names are lookup labels, not persistent instance identifiers; placement is not implemented." };
        }

        internal void StopBridge() { bridge?.Dispose(); bridge = null; ReleaseMenuIconHosts(); }
        [Preserve]
        protected override void OnDestroy() { StopBridge(); if (Instance == this) Instance = null; base.OnDestroy(); }
    }
}
