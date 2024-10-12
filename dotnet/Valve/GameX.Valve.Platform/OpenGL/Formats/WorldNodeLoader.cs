using GameX.Scenes;
using GameX.Valve.Formats;
using GameX.Valve.Formats.Vpk;
using GameX.Valve.OpenGL.Scenes;
using OpenStack.Gfx.Gl;
using OpenStack.Gfx.Scenes;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace GameX.Valve.OpenGL.Formats
{
    //was:Renderer/WorldLoader
    public class WorldNodeLoader(IOpenGLGfx gfx, D_WorldNode node)
    {
        readonly D_WorldNode Node = node;
        readonly IOpenGLGfx Gfx = gfx;

        public void Load(Scene scene)
        {
            var data = Node.Data;

            var worldLayers = data.ContainsKey("m_layerNames") ? data.Get<string[]>("m_layerNames") : Array.Empty<string>();
            var sceneObjectLayerIndices = data.ContainsKey("m_sceneObjectLayerIndices") ? data.GetInt64Array("m_sceneObjectLayerIndices") : null;
            var sceneObjects = data.GetArray("m_sceneObjects");
            var i = 0;

            // Output is WorldNode_t we need to iterate m_sceneObjects inside it
            foreach (var sceneObject in sceneObjects)
            {
                var layerIndex = sceneObjectLayerIndices?[i++] ?? -1;

                // sceneObject is SceneObject_t
                var renderableModel = sceneObject.Get<string>("m_renderableModel");
                var matrix = sceneObject.GetArray("m_vTransform").ToMatrix4x4();

                var tintColorVector = sceneObject.GetVector4("m_vTintColor");
                var tintColor = tintColorVector.W == 0 ? Vector4.One : tintColorVector;

                if (renderableModel != null)
                {
                    var newResource = Gfx.LoadFileObject<Binary_Src>($"{renderableModel}_c").Result;
                    if (newResource == null) continue;
                    var modelNode = new ModelSceneNode(scene, (IValveModel)newResource.DATA, null, false)
                    {
                        Transform = matrix,
                        Tint = tintColor,
                        LayerName = worldLayers[layerIndex],
                        Name = renderableModel,
                    };
                    scene.Add(modelNode, false);
                }

                var renderable = sceneObject.Get<string>("m_renderable");
                if (renderable != null)
                {
                    var newResource = Gfx.LoadFileObject<Binary_Src>($"{renderable}_c").Result;
                    if (newResource == null) continue;
                    var meshNode = new MeshSceneNode(scene, new D_Mesh(newResource), 0)
                    {
                        Transform = matrix,
                        Tint = tintColor,
                        LayerName = worldLayers[layerIndex],
                        Name = renderable,
                    };
                    scene.Add(meshNode, false);
                }
            }

            if (!data.ContainsKey("m_aggregateSceneObjects")) return;

            var aggregateSceneObjects = data.GetArray("m_aggregateSceneObjects");
            foreach (var sceneObject in aggregateSceneObjects)
            {
                var renderableModel = sceneObject.Get<string>("m_renderableModel");
                if (renderableModel != null)
                {
                    var newResource = Gfx.LoadFileObject<Binary_Src>($"{renderableModel}_c").Result;
                    if (newResource == null) continue;

                    var layerIndex = sceneObject.Get<int>("m_nLayer");
                    var modelNode = new ModelSceneNode(scene, (IValveModel)newResource.DATA, null, false)
                    {
                        LayerName = worldLayers[layerIndex],
                        Name = renderableModel,
                    };
                    scene.Add(modelNode, false);
                }
            }
        }
    }
}
