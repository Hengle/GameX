using OpenStack.Gfx;
using System.Collections.Generic;

namespace GameX.Valve.Formats.Vpk
{
    /// <summary>
    /// IValveModel
    /// </summary>
    public interface IValveModel : IModel
    {
        Skeleton Skeleton { get; }

        IEnumerable<(int MeshIndex, string MeshName, long LoDMask)> GetReferenceMeshNamesAndLoD();
        IEnumerable<(D_Mesh Mesh, int MeshIndex, string Name, long LoDMask)> GetEmbeddedMeshesAndLoD();
        IEnumerable<bool> GetActiveMeshMaskForGroup(string groupName);
        IEnumerable<string> GetMeshGroups();
        IEnumerable<string> GetDefaultMeshGroups();
        IEnumerable<Animation> GetAllAnimations(IOpenGfx gfx);
        D_PhysAggregateData GetEmbeddedPhys();
        IEnumerable<string> GetReferencedPhysNames();
    }
}