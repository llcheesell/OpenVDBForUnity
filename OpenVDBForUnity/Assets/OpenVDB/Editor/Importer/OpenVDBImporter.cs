using System;
using UnityEngine;
using System.IO;
using System.Linq;
using UnityEditor.AssetImporters;
using System.Text.RegularExpressions;
using UnityEngine.Rendering;
using Extensions;
using UnityEditor;
using Object = UnityEngine.Object;

namespace OpenVDB
{
    [Serializable]
    [ScriptedImporter(2, "vdb")]
    public class OpenVDBImporter : ScriptedImporter
    {
        [SerializeField] public OpenVDBStreamSettings streamSettings = new OpenVDBStreamSettings();

        private static string MakeShortAssetPath(string assetPath)
        {
            return Regex.Replace(assetPath, "^Assets/", "");
        }

        private static string SourcePath(string assetPath)
        {
            if (assetPath.StartsWith("Packages", System.StringComparison.Ordinal))
            {
                return Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath);
            }
            else
            {
                return Path.Combine(Application.dataPath, assetPath);
            }
        }

        static bool IsHDRP()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null) return false;
            return pipeline.GetType().Name.Contains("HDRenderPipelineAsset");
        }

        static string GetDefaultShaderName()
        {
            return IsHDRP() ? "OpenVDB/HDRP/Standard" : "OpenVDB/Standard";
        }

        static string GetRealtimeShaderName()
        {
            return IsHDRP() ? "OpenVDB/Realtime/HDRP" : "OpenVDB/Realtime/Standard";
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var shortAssetPath = MakeShortAssetPath(ctx.assetPath);
            var sourcePath = SourcePath(shortAssetPath);
            var destPath = Path.Combine(Application.streamingAssetsPath, shortAssetPath);
            var directoryPath = Path.GetDirectoryName(destPath);

            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);
            if (File.Exists(destPath))
                File.SetAttributes(destPath, FileAttributes.Normal);
            File.Copy(sourcePath, destPath, true);

            var fileName = Path.GetFileNameWithoutExtension(destPath);
            var go = new GameObject(fileName);

            var streamDescriptor = ScriptableObject.CreateInstance<OpenVDBStreamDescriptor>();
            streamDescriptor.name = go.name + "_VDBDesc";
            streamDescriptor.pathToVDB = shortAssetPath;
            streamDescriptor.settings = streamSettings;

            using (var vdbStream = new OpenVDBStream(go, streamDescriptor))
            {
                if (!vdbStream.Load())
                    return;

                var subassets = new Subassets(ctx, streamSettings.renderMode);
                subassets.Add(streamDescriptor.name, streamDescriptor);
                GenerateSubAssets(subassets, vdbStream, streamDescriptor);

                ctx.AddObjectToAsset(go.name, go);
                ctx.SetMainObject(go);
            }
        }

        class Subassets
        {
            AssetImportContext m_ctx;
            Material m_defaultMaterial;
            VolumeRenderMode m_renderMode;

            public Subassets(AssetImportContext ctx, VolumeRenderMode renderMode = VolumeRenderMode.Realtime)
            {
                m_ctx = ctx;
                m_renderMode = renderMode;
            }

            public Material defaultMaterial
            {
                get
                {
                    if (m_defaultMaterial != null) return m_defaultMaterial;

                    // Choose shader based on render mode
                    string shaderName;
                    if (m_renderMode == VolumeRenderMode.Realtime)
                    {
                        shaderName = GetRealtimeShaderName();
                    }
                    else
                    {
                        shaderName = GetDefaultShaderName();
                    }

                    var shader = Shader.Find(shaderName);
                    if (shader == null)
                    {
                        // Fallback to classic shader
                        shader = Shader.Find(GetDefaultShaderName());
                    }
                    if (shader == null)
                    {
                        shader = Shader.Find("OpenVDB/Standard");
                    }
                    if (shader == null)
                    {
                        Debug.LogWarning($"[OpenVDB] Could not find shader '{shaderName}' or any fallback. Using Standard shader.");
                        shader = Shader.Find("Standard");
                    }

                    m_defaultMaterial = new Material(shader)
                    {
                        name = "Default Material",
                        hideFlags = HideFlags.NotEditable,
                    };
                    Add("Default Material", m_defaultMaterial);
                    return m_defaultMaterial;
                }
            }
            public void Add(string identifier, Object asset)
            {
                m_ctx.AddObjectToAsset(identifier, asset);
            }
        }

        private void GenerateSubAssets(Subassets subassets, OpenVDBStream stream, OpenVDBStreamDescriptor descriptor)
        {
            CollectSubAssets(subassets, stream, descriptor);
        }

        private void CollectSubAssets(Subassets subassets, OpenVDBStream stream, OpenVDBStreamDescriptor descriptor)
        {
            var go = stream.gameObject;
            Texture texture = null;

            if (descriptor.settings.extractTextures)
            {
                texture = descriptor.settings.textures.First();
                AddRemap(new SourceAssetIdentifier(typeof(Texture), texture.name), texture);
            }
            else
            {
                if (stream.texture3D != null)
                {
                    texture = stream.texture3D;
                    subassets.Add(stream.texture3D.name, stream.texture3D);
                }
            }

            var meshFilter = go.GetOrAddComponent<MeshFilter>();
            if (meshFilter != null)
            {
                meshFilter.sharedMesh = stream.mesh;
                meshFilter.sharedMesh.name = go.name;
                subassets.Add(meshFilter.sharedMesh.name, meshFilter.sharedMesh);
            }
            var renderer = go.GetOrAddComponent<MeshRenderer>();
            if (renderer == null) return;

            // Add appropriate volume component based on render mode
            if (descriptor.settings.renderMode == VolumeRenderMode.Realtime)
            {
                go.AddComponent<Realtime.OpenVDBRealtimeVolume>();
            }
            else if (IsHDRP())
            {
                go.AddComponent<OpenVDBHDRPVolume>();
            }

            if (!descriptor.settings.importMaterials) return;
            if (descriptor.settings.extractMaterials)
            {
                var material = descriptor.settings.materials.First();
                AddRemap(new SourceAssetIdentifier(typeof(Material), material.name), material);
                renderer.sharedMaterial = material;
            }
            else
            {
                renderer.sharedMaterial = subassets.defaultMaterial;
            }

            if (texture == null) return;
            renderer.sharedMaterial.SetTexture("_Volume", texture);
            renderer.sharedMaterial.name = texture.name;
        }
    }
}
