#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gamebeast.Editor
{
    public class SimplePluginWindow : EditorWindow
    {
        // Optional: some state if you want to display output
        private string _statusMessage = "Idle";
		private string _selectedFruit = "Apple";

        [SerializeField] private Transform _cornerA;
        [SerializeField] private Transform _cornerB;
        [SerializeField] private float _paddingWorld = 0f;
        private const int MaxOutputDimension = 512;

		private Texture2D _previewTexture;
		private Vector2 _previewScroll;
        private string _currentKey = "";
        private bool _isFetched = false;

        [MenuItem("Tools/Gamebeast/Heatmaps")]
        public static void ShowWindow()
        {
            // Creates/gets existing window instance
            var window = GetWindow<SimplePluginWindow>("Gamebeast");
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Status:", _statusMessage);



            var newKey = EditorGUILayout.TextField("API Key:", _currentKey);
            if (newKey != _currentKey)
            {
                _currentKey = newKey;
                _statusMessage = "API Key updated.";
            }

            if (GUILayout.Button("Fetch Heatmaps"))
            {
                _isFetched = true;
                _statusMessage = "Heatmaps fetched.";
            }

            if (!_isFetched)
            {
                EditorGUILayout.HelpBox("Please enter your API Key and click 'Fetch Heatmaps' to load available heatmaps.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Heatmap:", _selectedFruit);
            if (EditorGUILayout.DropdownButton(new GUIContent(_selectedFruit ?? "Select Heatmap"), FocusType.Passive))
            {
                var fruits = new[] { "Apple", "Banana", "Cherry", "Date" };
                var menu = new GenericMenu();
                foreach (var fruit in fruits)
                {
                    var captured = fruit; // avoid modified-closure issue
                    menu.AddItem(new GUIContent(captured), _selectedFruit == captured, () =>
                    {
                        _selectedFruit = captured;
                        _statusMessage = "Selected Heatmap: " + captured;
                        Repaint();
                    });
                }

                // Show under the dropdown button.
                var buttonRect = GUILayoutUtility.GetLastRect();
                buttonRect.y += buttonRect.height;
                menu.DropDown(buttonRect);
            }



            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Capture Area", EditorStyles.boldLabel);
            _cornerA = (Transform)EditorGUILayout.ObjectField("Corner A", _cornerA, typeof(Transform), true);
            _cornerB = (Transform)EditorGUILayout.ObjectField("Corner B", _cornerB, typeof(Transform), true);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Use Selected", GUILayout.Width(140)))
                {
                    if (Selection.transforms != null && Selection.transforms.Length >= 2)
                    {
                        _cornerA = Selection.transforms[0];
                        _cornerB = Selection.transforms[1];
                        _statusMessage = $"Selected corners: {_cornerA.name}, {_cornerB.name}";
                    }
                    else
                    {
                        _statusMessage = "Select 2 objects in the Hierarchy first.";
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            _paddingWorld = EditorGUILayout.FloatField("Padding", _paddingWorld);

            var computed = ComputeOutputSize();
            EditorGUILayout.LabelField("Computed Size", computed.HasValue ? $"{computed.Value.x} x {computed.Value.y}" : "—");

            using (new EditorGUI.DisabledScope(_cornerA == null || _cornerB == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Render Preview", GUILayout.Height(30)))
                    {
                        RenderPreview();
                    }

                    if (GUILayout.Button("Save PNG…", GUILayout.Height(30), GUILayout.Width(120)))
                    {
                        SavePng();
                    }
                }
            }

            if (_previewTexture != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
                _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll, GUILayout.Height(260));
                var rect = GUILayoutUtility.GetRect(10, 10000, 10, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                GUI.DrawTexture(rect, _previewTexture, ScaleMode.ScaleToFit, alphaBlend: true);
                EditorGUILayout.EndScrollView();
            }

        }

		private void RenderPreview()
        {
            if (_cornerA == null || _cornerB == null)
            {
                _statusMessage = "Corner A/B must be assigned.";
                return;
            }

            var computed = ComputeOutputSize();
            if (!computed.HasValue)
            {
                _statusMessage = "Could not compute output size (missing corners).";
                return;
            }

            var width = computed.Value.x;
            var height = computed.Value.y;
            var options = new OverheadPngRenderer.Options
            {
                Width = width,
                Height = height,
                PaddingWorld = Mathf.Max(0f, _paddingWorld),
            };

            try
            {
                if (_previewTexture != null)
                {
                    DestroyImmediate(_previewTexture);
                    _previewTexture = null;
                }

                _previewTexture = OverheadPngRenderer.RenderToTexture2D(_cornerA.position, _cornerB.position, options);
                _previewTexture.filterMode = FilterMode.Point;
                _statusMessage = $"Preview rendered ({width} x {height}).";
            }
            catch (System.Exception ex)
            {
                _statusMessage = "Preview render failed. See Console.";
                Debug.LogError($"[Gamebeast Heatmaps] Render failed: {ex}");
            }

            Repaint();
        }

        private void SavePng()
        {
            if (_previewTexture == null)
            {
                // No preview yet: render one first, then user can save.
                RenderPreview();
                if (_previewTexture == null) return;
            }

            var assetPath = EditorUtility.SaveFilePanelInProject(
                "Save Overhead PNG",
                "overhead",
                "png",
                "Choose where to save the overhead PNG.");
            if (string.IsNullOrEmpty(assetPath))
            {
                _statusMessage = "Save cancelled.";
                return;
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                _statusMessage = "Could not resolve project root.";
                return;
            }

            var absolutePath = Path.Combine(projectRoot, assetPath);
            try
            {
                var png = _previewTexture.EncodeToPNG();
                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? projectRoot);
                File.WriteAllBytes(absolutePath, png);
                AssetDatabase.Refresh();
                _statusMessage = $"Saved: {assetPath}";
            }
            catch (System.Exception ex)
            {
                _statusMessage = "Save failed. See Console.";
                Debug.LogError($"[Gamebeast Heatmaps] Save failed: {ex}");
            }

            Repaint();
        }

        private void OnDisable()
        {
            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
                _previewTexture = null;
            }
        }

        private Vector2Int? ComputeOutputSize()
        {
            if (_cornerA == null || _cornerB == null) return null;

            var a = _cornerA.position;
            var b = _cornerB.position;

            var worldWidth = Mathf.Abs(a.x - b.x) + (Mathf.Max(0f, _paddingWorld) * 2f);
            var worldHeight = Mathf.Abs(a.z - b.z) + (Mathf.Max(0f, _paddingWorld) * 2f);

            // 1 world unit => 1 pixel (rounded up so small regions still render).
            var pxW = Mathf.Max(1, Mathf.CeilToInt(worldWidth));
            var pxH = Mathf.Max(1, Mathf.CeilToInt(worldHeight));

            var maxDim = Mathf.Max(pxW, pxH);
            if (maxDim > MaxOutputDimension)
            {
                var scale = MaxOutputDimension / (float)maxDim;
                pxW = Mathf.Max(1, Mathf.RoundToInt(pxW * scale));
                pxH = Mathf.Max(1, Mathf.RoundToInt(pxH * scale));
            }

            return new Vector2Int(pxW, pxH);
        }

        /// <summary>
        /// Put whatever you want this plugin to actually do here.
        /// This method is called when the button is pressed.
        /// </summary>
        private void RunMyLogic()
        {
            // Example "logic"
            Debug.Log("[SimplePlugin] Button clicked, running logic...");

            // TODO: replace this with your real logic
            // e.g. scan scene, modify assets, generate files, etc.

            _statusMessage = "Logic ran at: " + System.DateTime.Now.ToLongTimeString();

            // Force window repaint to update status text immediately
            Repaint();
        }
    }
}
#endif
