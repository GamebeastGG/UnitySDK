#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Gamebeast.Runtime.Internal.Utils;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using System;

namespace Gamebeast.Editor
{
    public class SimplePluginWindow : EditorWindow
    {
        // Optional: some state if you want to display output

        private class Point
        {
            public float x;
            public float y;
            public float z;
        }
        private class HeatmapBounds
        {
            public Point pointA;
            public Point pointB;
        }
        private class HeatmapDetails
        {
            public string id;
            public string name;
            public string description;
            public HeatmapBounds bounds;
            public float resolutionFactor;
        }

        private class NewHeatmapResponse {
            public string id;
        }

        private string _statusMessage = "Idle";
		private HeatmapDetails _selectedHeatmap = null;
        private HeatmapDetails[] _fetchedHeatmaps = new HeatmapDetails[0];

        [SerializeField] private Transform _cornerA;
        [SerializeField] private Transform _cornerB;
        [SerializeField] private float _paddingWorld = 0f;
        private const int MaxOutputDimension = 512;

		private Texture2D _previewTexture;
        private float _previewResolutionFactor = 1.0f;
        private byte[] _previewPngBytes;
		private Vector2 _previewScroll;
        private string _currentKey = "";
        private string _nameField = "";
        private string _descField = "";
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

            EditorGUILayout.Space();
            if (GUILayout.Button("Fetch Heatmaps", GUILayout.Height(30)))
            {
                FetchHeatmaps();
            }

            if (!_isFetched)
            {
                EditorGUILayout.HelpBox("Please enter your API Key and click 'Fetch Heatmaps' to load available heatmaps.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Selected Heatmap", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(_fetchedHeatmaps.Length == 0))
            {
                if ( EditorGUILayout.DropdownButton(new GUIContent(_selectedHeatmap?.name ?? "Select Heatmap"), FocusType.Passive, GUILayout.Height(20)))
                {

                    var menu = new GenericMenu();
                    foreach (var heatmap in _fetchedHeatmaps)
                    {
                        var captured = heatmap; // avoid modified-closure issue
                        menu.AddItem(new GUIContent(captured.name), _selectedHeatmap.id == captured.id, () =>
                        {
                            SelectHeatmap(captured.id);
                        });
                    }

                    // Show under the dropdown button.
                    var buttonRect = GUILayoutUtility.GetLastRect();
                    buttonRect.y += buttonRect.height;
                    menu.DropDown(buttonRect);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create new Heatmap", GUILayout.Height(20)))
                {
                    var newHeatmap = new HeatmapDetails
                    {
                        name = "New Heatmap",
                        description = "",
                        bounds = new HeatmapBounds
                        {
                            pointA = new Point { x = 0, y = 0, z = 0 },
                            pointB = new Point { x = 10, y = 0, z = 10 },
                        },
                        resolutionFactor = 1.0f,
                    };     

                    // Ensure name is unique by appending a number if needed

                    var num = 2;
                    while (true)
                    {
                        var exists = false;
                        foreach (var hm in _fetchedHeatmaps)
                        {
                            if (hm.name == newHeatmap.name)
                            {
                                exists = true;
                                break;
                            }
                        }
                        if (!exists) break;
                        newHeatmap.name = "New Heatmap (" + num + ")";
                        num++;
                    }

                    GBRequest.MakeRequestAsync<NewHeatmapResponse>(GBRequestType.CreateHeatmap, newHeatmap).ContinueWith(task =>
                    {
                        if (task.IsFaulted)
                        {
                            _statusMessage = "Create failed. See Console.";
                            Debug.LogError($"[Gamebeast Heatmaps] Create failed: {task.Exception}");
                        }
                        else
                        {
                            var createdId = task.Result.id;
                            _statusMessage = "Created new heatmap with ID: " + createdId;
                            Debug.Log("[Gamebeast Heatmaps] Created new heatmap with ID: " + createdId);

                            newHeatmap.id = createdId;
                            _fetchedHeatmaps = _fetchedHeatmaps.Append(newHeatmap).ToArray();


                            SelectHeatmap(createdId);
                            
                        }
                        Repaint();
                    });
                }

                if (_selectedHeatmap != null && GUILayout.Button("Delete Heatmap", GUILayout.Height(20), GUILayout.Width(140)))
                {
                    if (_selectedHeatmap == null) return;

                    var ok = EditorUtility.DisplayDialog(
                        "Delete heatmap?",
                        $"This will permanently delete '{_selectedHeatmap.name}'.\n\nThis cannot be undone.",
                        "Delete",
                        "Cancel");

                    if (!ok) return;

                    // Do the actual delete here (API call, update UI, etc.)

                    GBRequest.MakeRequestAsync<string>(GBRequestType.DeleteHeatmap, urlParams: new Dictionary<string, string> { { "id",  _selectedHeatmap.id } }).ContinueWith(task =>
                    {
                        if (task.IsFaulted)
                        {
                            _statusMessage = "Delete failed. See Console.";
                            Debug.LogError($"[Gamebeast Heatmaps] Delete failed: {task.Exception}");
                        }
                        else
                        {
                            _statusMessage = "Deleted heatmap successfully.";
                            Debug.Log("[Gamebeast Heatmaps] Deleted heatmap successfully.");

                            // Remove from local list
                            var remaining = new List<HeatmapDetails>();
                            foreach (var hm in _fetchedHeatmaps)
                            {
                                if (hm.id != _selectedHeatmap.id)
                                {
                                    remaining.Add(hm);
                                }
                            }
                            _fetchedHeatmaps = remaining.ToArray();

                            // Select first heatmap or null
                            if (_fetchedHeatmaps.Length > 0)
                            {
                                SelectHeatmap(_fetchedHeatmaps[0].id);
                            }
                            else
                            {
                                _selectedHeatmap = null;
                            }
                        }
                        Repaint();
                    });
                }
            }

            if (_selectedHeatmap == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Heatmap Details", EditorStyles.boldLabel);
            _nameField = EditorGUILayout.TextField("Name", _nameField);
            _descField = EditorGUILayout.TextField("Description", _descField);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Save Details", GUILayout.Width(140)))
                {
                    var toSave = new HeatmapDetails
                    {
                        name = _nameField,
                        description = _descField,
                        bounds = _selectedHeatmap.bounds,
                        resolutionFactor = _selectedHeatmap.resolutionFactor,
                    };

                    Debug.Log("Saving with name " + toSave.name + " desc " + toSave.description);

                    GBRequest.MakeRequestAsync<string>(GBRequestType.UpdateHeatmap, toSave, urlParams: new Dictionary<string, string> { { "id", _selectedHeatmap.id } }).ContinueWith(task =>
                    {
                        if (task.IsFaulted)
                        {
                            _statusMessage = "Update failed. See Console.";
                            Debug.LogError($"[Gamebeast Heatmaps] Update failed: {task.Exception}");
                        }
                        else
                        {
                            _statusMessage = "Updated heatmap details successfully.";
                            Debug.Log("[Gamebeast Heatmaps] Updated heatmap details successfully.");
                            Debug.Log(task.Result);

                            _selectedHeatmap.name = _nameField;
                            _selectedHeatmap.description = _descField;

                            SelectHeatmap(_selectedHeatmap.id);


                            Debug.Log(JsonConvert.SerializeObject(_fetchedHeatmaps, Formatting.Indented));
                        }
                        Repaint();
                    });
                }

                
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
            //_paddingWorld = EditorGUILayout.FloatField("Padding", _paddingWorld);

            var computed = ComputeOutputSize();
            EditorGUILayout.LabelField("Computed Size", computed.HasValue ? $"{computed.Value.x} x {computed.Value.y}" : "—");
            EditorGUILayout.LabelField("Resolution Factor", $"{_selectedHeatmap.resolutionFactor:0.####} units/px");

            using (new EditorGUI.DisabledScope(_cornerA == null || _cornerB == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Render Preview", GUILayout.Height(30)))
                    {
                        RenderPreview();
                    }
                    using (new EditorGUI.DisabledScope(_previewTexture == null))
                    {
                        if (GUILayout.Button("Upload PNG", GUILayout.Height(30)))
                        {
                            UploadPng();
                        }
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
                PaddingWorld = 0//Mathf.Max(0f, _paddingWorld),
            };

            try
            {
                if (_previewTexture != null)
                {
                    DestroyImmediate(_previewTexture);
                    _previewTexture = null;
                }

                var result = OverheadPngRenderer.RenderToTexture2DWithResult(_cornerA.position, _cornerB.position, options);
                _previewTexture = result.Texture;
                _previewTexture.filterMode = FilterMode.Point;

                _previewResolutionFactor = Math.Max(1.0f, result.ResolutionFactor);
                _previewPngBytes = _previewTexture.EncodeToPNG();
                _statusMessage = $"Preview rendered ({width} x {height}).";
            }
            catch (System.Exception ex)
            {
                _statusMessage = "Preview render failed. See Console.";
                Debug.LogError($"[Gamebeast Heatmaps] Render failed: {ex}");
            }

            Repaint();
        }

        // Use this when you're ready to upload the latest rendered preview.
        private byte[] GetPreviewPngBytes()
        {
            if (_previewTexture == null) return null;
            if (_previewPngBytes == null || _previewPngBytes.Length == 0)
            {
                _previewPngBytes = _previewTexture.EncodeToPNG();
            }
            return _previewPngBytes;
        }

        private static bool IsPngBytes(byte[] bytes)
        {
            // PNG signature: 89 50 4E 47 0D 0A 1A 0A
            if (bytes == null || bytes.Length < 8) return false;
            return bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
                   bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;
        }

        private async void UploadPng()
        {
            var pngBytes = GetPreviewPngBytes();
            if (!IsPngBytes(pngBytes))
            {
                _statusMessage = "Upload aborted: preview bytes are not PNG.";
                Debug.LogError("[Gamebeast Heatmaps] Upload aborted: preview bytes are not a valid PNG signature.");
                Repaint();
                return;
            }

            try
			{        
                var toSave = new HeatmapDetails
                {
                    name = _selectedHeatmap.name,
                    description = _selectedHeatmap.description,
                    bounds = new HeatmapBounds
                    {
                        pointA = new Point{ x = _selectedHeatmap.bounds.pointA.x, y = _selectedHeatmap.bounds.pointA.y, z = _selectedHeatmap.bounds.pointA.z },
                        pointB = new Point{ x = _selectedHeatmap.bounds.pointB.x, y = _selectedHeatmap.bounds.pointB.y, z = _selectedHeatmap.bounds.pointB.z },
                    },
                    resolutionFactor = Math.Max(1.0f, _previewResolutionFactor)
                };

                Debug.Log("Updating resolution factor to " + toSave.resolutionFactor);

                var updateResult = await GBRequest.MakeRequestAsync<string>(
                    GBRequestType.UpdateHeatmap,
                    toSave,
                    urlParams: new Dictionary<string, string> { { "id", _selectedHeatmap.id } });

                Debug.Log("[Gamebeast Heatmaps] Updated heatmap resolution factor successfully.");
                Debug.Log(updateResult);

                _selectedHeatmap.resolutionFactor = _previewResolutionFactor;
                SelectHeatmap(_selectedHeatmap.id);

                try
                {
                    var uploadResult = await Requester.PostAsync<string>(
                        $"/sdk/v1/heatmaps/{_selectedHeatmap.id}/image",
                        body: pngBytes,
                        headers: new Dictionary<string, string>
                        {
                            { "content-type", "image/png" },
                            { "authorization", _currentKey }
                        }
                    );

                    _statusMessage = "Uploaded heatmap image successfully.";
                    Debug.Log("[Gamebeast Heatmaps] Uploaded heatmap image successfully.");
                    Debug.Log(uploadResult);

                    if (true) {
                        return;
                    }

                    
                }
                catch (Exception ex)
                {
                    _statusMessage = "Upload failed. See Console.";
                    Debug.LogError($"[Gamebeast Heatmaps] Upload failed: {ex}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Gamebeast Heatmaps] Update resolution factor failed: {ex}");
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

			_previewPngBytes = null;
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


       
        private void SelectHeatmap(string id) {
            foreach (var heatmap in _fetchedHeatmaps)
            {
                if (heatmap.id == id) {
                    _selectedHeatmap = heatmap;
                    _statusMessage = "Selected Heatmap: " + heatmap.name;

                    _nameField = heatmap.name;
                    _descField = heatmap.description;

                    Repaint();
                    return;
                }
            }
            
        }    
        private void FetchHeatmaps()
        {
            GBRequest.SetApiKey(_currentKey);
            GBRequest.MakeRequestAsync<HeatmapDetails[]>(GBRequestType.GetHeatmaps).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    _statusMessage = "Fetch failed. See Console.";
                    Debug.LogError($"[Gamebeast Heatmaps] Fetch failed: {task.Exception}");
                }
                else
                {
                    _isFetched = true;
                    _statusMessage = "Fetched heatmaps successfully.";
                    Debug.Log("[Gamebeast Heatmaps] Fetched heatmaps successfully.");
                    Debug.Log(JsonConvert.SerializeObject(task.Result, Formatting.Indented));
                    _fetchedHeatmaps = task.Result;

                    if (_fetchedHeatmaps.Length > 0)
                    {
                        SelectHeatmap(_fetchedHeatmaps[0].id);
                        _statusMessage = "Selected first heatmap: " + _selectedHeatmap.name;
                    }
                    else
                    {
                        _selectedHeatmap = null;
                        _statusMessage = "No heatmaps available.";
                    }
                }
                Repaint();
            });

            Repaint();
        }
    }
}
#endif
