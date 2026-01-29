#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Gamebeast.Editor
{
    internal static class OverheadPngRenderer
    {
        internal sealed class RenderResult
        {
            public Texture2D Texture;
            // World-units per pixel in the output image.
            public float ResolutionFactor;
        }

        internal sealed class Options
        {
            public int Width = 2048;
            public int Height = 2048;
            public float PaddingWorld = 0f;
            public int CullingMask = ~0;
            public Color BackgroundColor = new Color(0f, 0f, 0f, 0f);

            // If true, tries to fit far clip based on renderers inside the region.
            public bool FitToRenderersInRegion = true;

            // Extra Y margin above tallest renderer (or bounds max Y).
            public float HeightMargin = 50f;
        }

        /// <summary>
        /// Renders a top-down orthographic Texture2D covering the XZ rectangle defined by cornerA/cornerB.
        /// Y is only used for clipping; the image coverage is in XZ.
        /// </summary>
        internal static Texture2D RenderToTexture2D(Vector3 cornerA, Vector3 cornerB, Options options = null)
        {
            var result = RenderToTexture2DWithResult(cornerA, cornerB, options);
            return result.Texture;
        }

        /// <summary>
        /// Same as <see cref="RenderToTexture2D"/>, but also returns the resolution factor (world-units per pixel).
        /// </summary>
        internal static RenderResult RenderToTexture2DWithResult(Vector3 cornerA, Vector3 cornerB, Options options = null)
        {
            options ??= new Options();

            var minX = Mathf.Min(cornerA.x, cornerB.x) - options.PaddingWorld;
            var maxX = Mathf.Max(cornerA.x, cornerB.x) + options.PaddingWorld;
            var minZ = Mathf.Min(cornerA.z, cornerB.z) - options.PaddingWorld;
            var maxZ = Mathf.Max(cornerA.z, cornerB.z) + options.PaddingWorld;

            var width = Mathf.Max(1, options.Width);
            var height = Mathf.Max(1, options.Height);

            var centerX = (minX + maxX) * 0.5f;
            var centerZ = (minZ + maxZ) * 0.5f;

            var yMin = Mathf.Min(cornerA.y, cornerB.y);
            var yMax = Mathf.Max(cornerA.y, cornerB.y);

            if (options.FitToRenderersInRegion)
            {
                TryComputeYExtentsFromRenderers(minX, maxX, minZ, maxZ, ref yMin, ref yMax);
            }

            var aspect = width / (float)height;
            var halfX = (maxX - minX) * 0.5f;
            var halfZ = (maxZ - minZ) * 0.5f;

            // Camera looks down -Y; screen horizontal maps to +X, vertical maps to +Z.
            // orthographicSize is half of the vertical world size.
            var orthoSize = Mathf.Max(halfZ, halfX / aspect);

            // Final world-units per pixel in the output image.
            // (Vertical world coverage is 2*orthoSize, spanning 'height' pixels.)
            var resolutionFactor = (2f * orthoSize) / height;

            var cameraY = yMax + Mathf.Max(1f, options.HeightMargin);
            var farClip = Mathf.Max(1f, cameraY - yMin + Mathf.Max(1f, options.HeightMargin));

            var cameraGo = new GameObject("__GB_OverheadCaptureCamera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            RenderTexture prevActive = null;
            try
            {
                var cam = cameraGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = orthoSize;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = options.BackgroundColor;
                cam.cullingMask = options.CullingMask;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = farClip;
                cam.allowHDR = false;
                cam.allowMSAA = false;

                cam.transform.position = new Vector3(centerX, cameraY, centerZ);
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 1,
                    wrapMode = TextureWrapMode.Clamp,
					filterMode = FilterMode.Point
                };

                cam.targetTexture = rt;
                cam.Render();

                prevActive = RenderTexture.active;
                RenderTexture.active = rt;

                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
				tex.filterMode = FilterMode.Point;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply(false, false);

                cam.targetTexture = null;
                RenderTexture.active = prevActive;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);

                return new RenderResult
                {
                    Texture = tex,
                    ResolutionFactor = resolutionFactor,
                };
            }
            finally
            {
                if (prevActive != null)
                {
                    RenderTexture.active = prevActive;
                }

                if (cameraGo != null)
                {
                    UnityEngine.Object.DestroyImmediate(cameraGo);
                }
            }
        }

        private static void TryComputeYExtentsFromRenderers(float minX, float maxX, float minZ, float maxZ, ref float yMin, ref float yMax)
        {
            var foundAny = false;

#if UNITY_2022_2_OR_NEWER
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
#else
            var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
#endif

            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                if (!r.enabled) continue;
                if (!r.gameObject.activeInHierarchy) continue;

                var b = r.bounds;

                // Intersect in XZ only.
                if (b.max.x < minX || b.min.x > maxX) continue;
                if (b.max.z < minZ || b.min.z > maxZ) continue;

                if (!foundAny)
                {
                    yMin = b.min.y;
                    yMax = b.max.y;
                    foundAny = true;
                }
                else
                {
                    yMin = Mathf.Min(yMin, b.min.y);
                    yMax = Mathf.Max(yMax, b.max.y);
                }
            }
        }

    }
}
#endif
