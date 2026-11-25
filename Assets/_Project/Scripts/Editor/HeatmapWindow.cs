using System.Collections.Generic;
using System.IO;
using RiskOfMagic.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RiskOfMagic.EditorTools
{
    /// <summary>
    /// Tools → Risk of Magic → Heatmap: loads the playtest session logs (SessionRecorder JSON files) and draws, in the
    /// Scene view of the open map, where players spent their time (cells colored by density) and where they were knocked
    /// down or died (markers with the cause on hover-free labels).
    /// </summary>
    public class HeatmapWindow : EditorWindow
    {
        private string folder;
        private float cellSize = 3f;
        private float opacity = 0.55f;
        private bool showHeat = true;
        private bool showDeaths = true;
        private bool showLabels = true;
        private string characterFilter = string.Empty;
        private Vector2 scroll;

        private readonly List<SessionRecorder.Session> sessions = new();
        private readonly Dictionary<Vector2Int, (int count, float height)> cells = new();
        private readonly List<SessionRecorder.DeathEvent> deaths = new();
        private int maxCount;
        private int samples;
        private int stagesOnMap;
        private float timeOnMap;
        private string scene;

        [MenuItem("Tools/Risk of Magic/Heatmap")]
        private static void Open() => GetWindow<HeatmapWindow>("Heatmap");

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(folder))
                folder = SessionRecorder.Folder;
            SceneView.duringSceneGui += OnSceneGui;
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
        }

        private void OnSceneChanged(UnityEngine.SceneManagement.Scene previous, UnityEngine.SceneManagement.Scene next)
        {
            Rebuild();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Ouvre la carte (Map_*) puis charge les journaux de session. " +
                                    "Les testeurs envoient les fichiers de leur dossier Sessions ; copie-les dans un même dossier.",
                MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                folder = EditorGUILayout.TextField("Dossier", folder);
                if (GUILayout.Button("…", GUILayout.Width(28f)))
                {
                    string picked = EditorUtility.OpenFolderPanel("Journaux de session", folder, string.Empty);
                    if (!string.IsNullOrEmpty(picked))
                        folder = picked;
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Charger"))
                    Load();
                if (GUILayout.Button("Ouvrir le dossier local"))
                    EditorUtility.RevealInFinder(SessionRecorder.Folder);
            }

            EditorGUI.BeginChangeCheck();
            cellSize = EditorGUILayout.Slider("Taille des cases (m)", cellSize, 1f, 10f);
            opacity = EditorGUILayout.Slider("Opacité", opacity, 0.1f, 1f);
            characterFilter = EditorGUILayout.TextField("Classe (vide = toutes)", characterFilter);
            showHeat = EditorGUILayout.Toggle("Présence", showHeat);
            showDeaths = EditorGUILayout.Toggle("Mises à terre / morts", showDeaths);
            showLabels = EditorGUILayout.Toggle("Causes", showLabels);
            if (EditorGUI.EndChangeCheck())
            {
                Rebuild();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Carte : {scene}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{sessions.Count} session(s) chargée(s), {stagesOnMap} passage(s) sur cette carte");
            EditorGUILayout.LabelField($"{samples} positions (1 par seconde), temps moyen sur la carte : {(stagesOnMap > 0 ? timeOnMap / stagesOnMap / 60f : 0f):0.0} min");
            EditorGUILayout.LabelField($"{deaths.Count} mise(s) à terre / mort(s)");

            if (deaths.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Causes", EditorStyles.boldLabel);
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(160f));
                var causes = new Dictionary<string, int>();
                foreach (var death in deaths)
                    causes[death.cause] = causes.TryGetValue(death.cause, out int n) ? n + 1 : 1;
                foreach (var pair in causes)
                    EditorGUILayout.LabelField(pair.Key, pair.Value.ToString());
                EditorGUILayout.EndScrollView();
            }
        }

        private void Load()
        {
            sessions.Clear();
            if (!Directory.Exists(folder))
            {
                ShowNotification(new GUIContent("Dossier introuvable"));
                return;
            }
            foreach (string path in Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    var session = JsonUtility.FromJson<SessionRecorder.Session>(File.ReadAllText(path));
                    if (session != null && session.stages != null)
                        sessions.Add(session);
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning($"[Heatmap] {Path.GetFileName(path)} ignoré : {exception.Message}");
                }
            }
            Rebuild();
            SceneView.RepaintAll();
        }

        private void Rebuild()
        {
            scene = EditorSceneManager.GetActiveScene().name;
            cells.Clear();
            deaths.Clear();
            maxCount = 0;
            samples = 0;
            stagesOnMap = 0;
            timeOnMap = 0f;
            foreach (var session in sessions)
            {
                if (!string.IsNullOrEmpty(characterFilter) && !string.Equals(session.character, characterFilter, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                foreach (var stage in session.stages)
                {
                    if (stage.scene != scene)
                        continue;
                    stagesOnMap++;
                    timeOnMap += stage.duration;
                    var p = stage.positions;
                    for (int i = 0; i + 2 < p.Count; i += 3)
                    {
                        var key = new Vector2Int(Mathf.FloorToInt(p[i] / cellSize), Mathf.FloorToInt(p[i + 2] / cellSize));
                        cells.TryGetValue(key, out var cell);
                        cell.height = (cell.height * cell.count + p[i + 1]) / (cell.count + 1);
                        cell.count++;
                        cells[key] = cell;
                        maxCount = Mathf.Max(maxCount, cell.count);
                        samples++;
                    }
                    deaths.AddRange(stage.deaths);
                }
            }
        }

        private void OnSceneGui(SceneView view)
        {
            if (showHeat && maxCount > 0)
            {
                var verts = new Vector3[4];
                foreach (var pair in cells)
                {
                    float t = Mathf.Sqrt(pair.Value.count / (float)maxCount); // sqrt: rare spots stay visible
                    var color = Gradient(t);
                    color.a = opacity * Mathf.Lerp(0.35f, 1f, t);
                    float x = pair.Key.x * cellSize, z = pair.Key.y * cellSize, y = pair.Value.height + 0.15f;
                    verts[0] = new Vector3(x, y, z);
                    verts[1] = new Vector3(x + cellSize, y, z);
                    verts[2] = new Vector3(x + cellSize, y, z + cellSize);
                    verts[3] = new Vector3(x, y, z + cellSize);
                    Handles.DrawSolidRectangleWithOutline(verts, color, Color.clear);
                }
            }

            if (showDeaths)
            {
                var style = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = Color.white } };
                foreach (var death in deaths)
                {
                    var position = new Vector3(death.x, death.y + 0.5f, death.z);
                    Handles.color = death.downed ? new Color(1f, 0.6f, 0.1f) : new Color(1f, 0.1f, 0.15f);
                    Handles.SphereHandleCap(0, position, Quaternion.identity, 0.9f, EventType.Repaint);
                    if (showLabels)
                        Handles.Label(position + Vector3.up * 0.8f, death.cause, style);
                }
            }
        }

        /// <summary>Blue (rare) → green → yellow → red (most visited).</summary>
        private static Color Gradient(float t)
        {
            if (t < 0.33f)
                return Color.Lerp(new Color(0.1f, 0.3f, 1f), new Color(0.1f, 0.9f, 0.3f), t / 0.33f);
            if (t < 0.66f)
                return Color.Lerp(new Color(0.1f, 0.9f, 0.3f), new Color(1f, 0.9f, 0.1f), (t - 0.33f) / 0.33f);
            return Color.Lerp(new Color(1f, 0.9f, 0.1f), new Color(1f, 0.15f, 0.1f), (t - 0.66f) / 0.34f);
        }
    }
}
