using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CityDirector
{
    // Small native window: no dependency on a UI framework or a separate web server.
    public sealed class AssetLibraryWindow : MonoBehaviour
    {
        private Rect window = new Rect(80, 100, 900, 610);
        private bool visible, showArchived, showDetails;
        private string confirmingDelete, actionError;
        private long deletionEstimate;
        private bool choosingCategory;
        private string categoryFilter = "";
        private Vector2 categoryScroll;
        private JObject categoryTree;
        private string selected, filter = "", error;
        private float nextRefresh;
        private Vector2 listScroll, detailScroll;
        private List<JObject> items = new List<JObject>();
        private readonly Dictionary<string, Texture2D> previews = new Dictionary<string, Texture2D>();
        private Font font;
        private GUIStyle wrap, title;
        private readonly AssetLibraryInputGuard inputGuard = new AssetLibraryInputGuard();
        internal static AssetLibraryWindow Instance { get; private set; }
        internal bool IsOpen => visible;
        internal bool InputBlocked => inputGuard.Blocked;

        private void Awake() { Instance = this; font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 16); }
        private void Update()
        {
            var mouse = Input.mousePosition;
            bool overButton = new Rect(Math.Max(10, Screen.width - 190), 50, 180, 36).Contains(new Vector2(mouse.x, Screen.height - mouse.y));
            if (visible && Input.GetKeyDown(KeyCode.Escape)) SetVisible(false);
            if (visible && Game.SceneFlow.GameManager.instance?.isGameLoading == true) SetVisible(false);
            inputGuard.Update(visible, overButton);
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 2;
            Refresh();
        }
        private void Refresh()
        {
            try
            {
                if (AssetLibrary.Root == null) { error = AssetLibrary.Error; return; }
                string jobs = Path.Combine(AssetLibrary.Root, "deletions");
                if (Directory.Exists(jobs))
                    foreach (string file in Directory.GetFiles(jobs, "*.json"))
                        AssetDeletion.Reconcile(AssetLibrary.Root, Path.GetFileNameWithoutExtension(file));
                string folder = Path.Combine(AssetLibrary.Root, "catalog");
                items = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.json").Select(AssetLibrary.Read)
                    .OrderByDescending(x => (string)x["createdAt"]).ToList() : new List<JObject>();
                error = null;
            }
            catch (Exception e) { error = "读取资产库失败：" + e.Message; }
        }
        private void OnGUI()
        {
            Font previous = GUI.skin.font;
            GUI.skin.font = font;
            try
            {
                if (visible && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                { SetVisible(false); Event.current.Use(); }
                if (GUI.Button(new Rect(Math.Max(10, Screen.width - 190), 50, 180, 36), "导入资产库 (" + items.Count + ")")) SetVisible(!visible);
                if (!visible) return;
                wrap = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 15 };
                title = new GUIStyle(wrap) { fontSize = 20, fontStyle = FontStyle.Bold };
                window.width = Math.Min(900, Screen.width - 20); window.height = Math.Min(650, Screen.height - 70);
                window.x = Mathf.Clamp(window.x, 0, Math.Max(0, Screen.width - window.width));
                window.y = Mathf.Clamp(window.y, 0, Math.Max(0, Screen.height - window.height));
                window = GUI.Window(0x43444952, window, DrawWindow, "CityDirector · 导入资产管理");
            }
            finally { GUI.skin.font = previous; }
        }
        private void DrawWindow(int windowId)
        {
            // IMGUI routes keyboard events to the focused window callback.
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            { SetVisible(false); Event.current.Use(); return; }
            try { DrawContents(); }
            catch (Exception e) { GUILayout.Label("无法显示当前资产：" + e.Message, wrap); }
            GUI.DragWindow(new Rect(0, 0, window.width - 50, 24));
        }
        private void DrawContents()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("查看和管理导入的资产", wrap);
            if (GUILayout.Button("刷新", GUILayout.Width(60))) Refresh();
            if (GUILayout.Button("关闭", GUILayout.Width(60))) SetVisible(false);
            GUILayout.EndHorizontal();
            if (error != null) { GUILayout.Label(error, wrap); return; }
            if (actionError != null) GUILayout.Label(actionError, wrap);
            DrawDeletionResults();
            if (confirmingDelete != null) { DrawDeleteConfirmation(); return; }
            bool allowed = AssetLibrary.AllowImports;
            bool next = GUILayout.Toggle(allowed, "允许 Codex 导入新资产（关闭后不会中断已经开始的导入）");
            if (next != allowed) AssetLibrary.SetAllowImports(next);
            GUILayout.BeginHorizontal();
            GUILayout.Label("搜索", GUILayout.Width(40)); filter = GUILayout.TextField(filter, GUILayout.Width(220));
            showArchived = GUILayout.Toggle(showArchived, "显示已隐藏");
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Width(270));
            int shown = 0;
            foreach (var item in items)
            {
                string id = (string)item["id"]; bool archived = AssetLibrary.Archived(id);
                string name = (string)item["title"] ?? id;
                if ((!showArchived && archived) || (filter.Length > 0 && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)) continue;
                shown++;
                var receipt = AssetLibrary.Read(AssetLibrary.FileFor("receipts", id));
                var deletion = AssetDeletion.Read(AssetLibrary.Root, id);
                string state = deletion != null ? DeleteState(deletion.state) : State((string)receipt["state"]);
                if (GUILayout.Button(name + "\n" + (archived ? "已隐藏 · " : "") + state, GUILayout.Height(52))) { selected = id; showDetails = false; choosingCategory = false; actionError = null; }
            }
            if (shown == 0) GUILayout.Label("暂无符合条件的资产。\nCodex 准备好的资产会出现在这里。", wrap);
            GUILayout.EndScrollView();
            detailScroll = GUILayout.BeginScrollView(detailScroll);
            var asset = items.FirstOrDefault(x => (string)x["id"] == selected);
            if (asset == null) GUILayout.Label("选择左侧资产，查看预览、来源、导入结果和管理操作。", wrap);
            else DrawAsset(asset);
            GUILayout.EndScrollView();
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            GUILayout.Label("隐藏保留文件；删除清理导入副本和游戏资源。Agent 原始工程始终保留。", wrap);
        }
        private void DrawAsset(JObject asset)
        {
            string id = (string)asset["id"];
            GUILayout.Label((string)asset["title"] ?? id, title);
            var receipt = AssetLibrary.Read(AssetLibrary.FileFor("receipts", id));
            GUILayout.Label("状态：" + State((string)receipt["state"]), wrap);
            var preview = Preview(id);
            if (preview != null) GUILayout.Label(preview, GUILayout.Width(320), GUILayout.Height(200));
            else GUILayout.Label("暂无预览图", wrap);
            GUILayout.Label((string)asset["description"] ?? "", wrap);
            GUILayout.Label("创建时间：" + (string)asset["createdAt"], wrap);
            GUILayout.Label("来源：" + ((string)asset["parentAssetId"] ?? "新建模型") + "\n编号：" + id, wrap);
            if (receipt["error"] != null) GUILayout.Label("导入详情：" + (string)receipt["error"], wrap);
            DrawCategoryPicker(id);
            var deletion = AssetDeletion.Read(AssetLibrary.Root, id);
            if (deletion != null)
            {
                GUILayout.Label(DeleteState(deletion.state), wrap);
                if (deletion.error != null) GUILayout.Label(deletion.error, wrap);
                if (deletion.state == "waiting_for_exit") GUILayout.Label("清理程序正在等待游戏退出；退出到桌面后执行。若任务中断，会显示失败原因并允许重试。", wrap);
            }
            bool archived = AssetLibrary.Archived(id);
            if (GUILayout.Button(archived ? "重新显示" : "从资产库隐藏", GUILayout.Height(34))) AssetLibrary.Archive(id, !archived);
            if (GUILayout.Button(deletion == null ? "删除资产…" : "重试删除…", GUILayout.Height(34)))
            {
                try
                {
                    if (DirectorSystem.Instance == null) throw new InvalidOperationException("游戏系统尚未就绪。");
                    deletionEstimate = DirectorSystem.Instance.PreviewDeletion(id);
                    confirmingDelete = id; actionError = null;
                }
                catch (Exception e) { actionError = "无法准备删除：" + e.Message; }
            }
            showDetails = GUILayout.Toggle(showDetails, "详细信息与文件位置");
            if (!showDetails) return;
            GUILayout.Label("摆放后的存档重载：尚未验证\n历史存档引用：未知（不阻止删除）", wrap);
            GUILayout.Label("工程副本：" + Path.Combine(AssetLibrary.Root, "sources", id), wrap);
            GUILayout.Label("导入文件：" + Path.Combine(AssetLibrary.Root, "imports", id), wrap);
            if (receipt["nativeDirectory"] != null) GUILayout.Label("游戏文件：" + (string)receipt["nativeDirectory"], wrap);
            if (receipt["nativeBackupDirectory"] != null) GUILayout.Label("备份：" + (string)receipt["nativeBackupDirectory"], wrap);
        }
        private void DrawCategoryPicker(string id)
        {
            var config = AssetLibrary.Read(AssetLibrary.FileFor("menus", id));
            GUILayout.Label("建造菜单分类：" + ((string)config["categoryTitle"] ?? (string)config["categoryId"] ?? "尚未指定"), wrap);
            if (config["error"]?.Type == JTokenType.String) GUILayout.Label("分类不可用：" + (string)config["error"], wrap);
            if (GUILayout.Button(choosingCategory ? "收起分类选择" : "修改建造菜单分类…"))
            {
                try { categoryTree = DirectorSystem.Instance.MenuTree(); choosingCategory = !choosingCategory; actionError = null; }
                catch (Exception e) { actionError = e.Message; }
            }
            if (!choosingCategory || categoryTree == null) return;
            categoryFilter = GUILayout.TextField(categoryFilter);
            categoryScroll = GUILayout.BeginScrollView(categoryScroll, GUILayout.Height(180));
            foreach (JObject menu in (JArray)categoryTree["menus"])
                foreach (JObject category in (JArray)menu["categories"])
                {
                    string label = (string)menu["title"] + " / " + (string)category["title"];
                    if (categoryFilter.Length > 0 && (label + category["categoryId"]).IndexOf(categoryFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (GUILayout.Button(label))
                    {
                        try
                        {
                            DirectorSystem.Instance.AssignCategory(new JObject { ["assetId"] = id, ["categoryId"] = category["categoryId"] });
                            choosingCategory = false; actionError = null;
                        }
                        catch (Exception e) { actionError = "分类修改失败：" + e.Message; }
                    }
                }
            GUILayout.EndScrollView();
        }
        private void DrawDeleteConfirmation()
        {
            var deleting = items.FirstOrDefault(x => (string)x["id"] == confirmingDelete);
            GUILayout.Label("删除“" + ((string)deleting?["title"] ?? confirmingDelete) + "”？", title);
            GUILayout.Label("预计删除 " + Size(deletionEstimate) + " 的导入文件。将在退出游戏后清理游戏资源、资产库副本及自动备份。\nAgent 的 Blender 原目录和原始导出文件不会被修改。\n当前城市实例数量及旧存档引用未知；删除可能造成物件缺失或存档加载异常。此操作不可撤销。", wrap);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("取消", GUILayout.Height(36))) confirmingDelete = null;
            if (GUILayout.Button("确认删除文件（退出游戏后）", GUILayout.Height(36)))
            {
                try { DirectorSystem.Instance.RequestDeletion(confirmingDelete); actionError = null; confirmingDelete = null; }
                catch (Exception e) { actionError = "未提交删除：" + e.Message; }
            }
            GUILayout.EndHorizontal();
        }
        private static string Size(long bytes) => (bytes / 1048576d).ToString("0.00") + " MB（" + bytes + " 字节）";
        private static string DeleteState(string state)
        {
            switch (state)
            {
                case "waiting_for_exit": return "等待退出游戏后删除";
                case "deleting": return "正在删除文件";
                case "deleted": return "已删除文件";
                default: return "删除未完成，可重试";
            }
        }
        private void DrawDeletionResults()
        {
            string folder = Path.Combine(AssetLibrary.Root, "deletions");
            if (!Directory.Exists(folder)) return;
            foreach (string file in Directory.GetFiles(folder, "*.json"))
            {
                string id = Path.GetFileNameWithoutExtension(file);
                var job = AssetDeletion.Read(AssetLibrary.Root, id);
                if (job == null || (job.state != "deleted" && File.Exists(AssetLibrary.FileFor("catalog", id)))) continue;
                GUILayout.Label(DeleteState(job.state) + "：" + id + "\n已删除文件大小：" + Size(job.deletedBytes) + (job.error == null ? "" : "\n" + job.error), wrap);
                if (job.state == "deleted" && GUILayout.Button("清除这条删除记录"))
                {
                    using (AssetDeletion.Lock(AssetLibrary.Root, id)) File.Delete(AssetDeletion.JobPath(AssetLibrary.Root, id));
                    File.Delete(AssetDeletion.Under(AssetLibrary.Root, "deletions", id + ".lock"));
                    if (previews.TryGetValue(id, out var texture)) { Destroy(texture); previews.Remove(id); }
                }
            }
        }
        private Texture2D Preview(string id)
        {
            if (previews.TryGetValue(id, out var cached)) return cached;
            string file = Path.Combine(AssetLibrary.Root, "sources", AssetLibrary.Id(id), "preview.png");
            if (!File.Exists(file) || new FileInfo(file).Length > 8 * 1024 * 1024) return null;
            if (previews.Count >= 32) { foreach (var image in previews.Values) Destroy(image); previews.Clear(); }
            var texture = new Texture2D(2, 2);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(file))) { Destroy(texture); return null; }
            previews[id] = texture; return texture;
        }
        private static string State(string state)
        {
            switch (state)
            {
                case "imported": return "已导入（重载待验证）";
                case "importing": return "已提交；通过 MCP 查询实时进度";
                case "failed_partial_possible": return "导入未完成，文件已保留";
                default: return "已准备，尚未导入";
            }
        }
        private void SetVisible(bool value)
        {
            if (visible == value) return;
            if (value) inputGuard.Open(); else inputGuard.Close();
            visible = value;
        }
        private void OnDisable() { visible = false; inputGuard.Dispose(); }
        private void OnDestroy() { inputGuard.Dispose(); if (Instance == this) Instance = null; foreach (var image in previews.Values) Destroy(image); if (font != null) Destroy(font); }
    }
}
