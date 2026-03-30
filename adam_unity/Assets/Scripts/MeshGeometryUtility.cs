using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A utility class to calculate the volume of a 3D mesh.
/// </summary>
public static class MeshGeometryUtility
{
    /// <summary>
    /// Calculates the signed volume of a tetrahedron defined by four points.
    /// The fourth point is implicitly the origin (0,0,0).
    /// This method is now private and uses doubles for high precision.
    /// </summary>
    private static double SignedVolumeOfTriangle(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        // Cast to double *before* multiplication to maintain precision
        // and avoid intermediate float overflow.
        double p1x = p1.x; double p1y = p1.y; double p1z = p1.z;
        double p2x = p2.x; double p2y = p2.y; double p2z = p2.z;
        double p3x = p3.x; double p3y = p3.y; double p3z = p3.z;

        double v321 = p3x * p2y * p1z;
        double v231 = p2x * p3y * p1z;
        double v312 = p3x * p1y * p2z;
        double v132 = p1x * p3y * p2z;
        double v213 = p2x * p1y * p3z;
        double v123 = p1x * p2y * p3z;

        // The formula is based on the scalar triple product / 6
        return (1.0 / 6.0) * (-v321 + v231 + v312 - v132 - v213 + v123);
    }
    
    public static float BoundingBoxVolumeOfMeshRenderer(Renderer renderer)
    {
        // Get the size of the renderer bounding box
        Vector3 scaledSize = renderer.bounds.size;

        // Volume of a box is width * height * depth
        return scaledSize.x * scaledSize.y * scaledSize.z;
    }
    
    /// <summary>
    /// Calculates the volume of a mesh.
    /// </summary>
    /// <param name="mesh">The mesh to calculate.</param>
    /// <returns>The volume of the mesh. Returns 0 if the mesh is null or has no vertices.</returns>
    /// <remarks>
    /// This method assumes the mesh is "watertight" (i.e., it has no holes and is a closed surface).
    /// The result will be incorrect for non-closed meshes.
    /// </remarks>
    public static float VolumeOfMesh(Mesh mesh)
    {
        // Run the check first
        if (!IsMeshWatertight(mesh))
        {
            Debug.LogWarning("Cannot calculate volume. The mesh is not watertight!");
            //return -1f;
        }

        // Handle edge case: null mesh
        if (mesh == null)
        {
            Debug.LogWarning("Mesh is null. Returning 0 volume.");
            return 0.0f;
        }

        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        // Handle edge case: empty mesh
        if (vertices.Length == 0 || triangles.Length == 0)
        {
            return 0.0f;
        }

        // Use a double for the accumulator for high precision
        double volume = 0.0;
        
        // Use the first vertex as an "anchor point" to make the calculation
        // relative, preventing floating-point errors for meshes far
        // from the world origin.
        Vector3 anchor = vertices[0];

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 p1 = vertices[triangles[i + 0]];
            Vector3 p2 = vertices[triangles[i + 1]];
            Vector3 p3 = vertices[triangles[i + 2]];

            // Calculate vectors relative to the anchor point
            Vector3 rel_p1 = p1 - anchor;
            Vector3 rel_p2 = p2 - anchor;
            Vector3 rel_p3 = p3 - anchor;

            // Sum the signed volumes of the tetrahedrons
            // (anchor, p1, p2, p3)
            volume += SignedVolumeOfTriangle(rel_p1, rel_p2, rel_p3);
        }

        // Return the absolute value, cast back to float
        return (float)System.Math.Abs(volume);
    }
    
    /// <summary>
    /// Checks if a mesh is "watertight" (i.e., closed, with no holes).
    /// </summary>
    /// <param name="mesh">The mesh to check.</param>
    /// <returns>True if the mesh is watertight, false otherwise.</returns>
    /// <remarks>
    /// A watertight mesh has every edge shared by exactly two triangles.
    /// This method checks for:
    /// 1. "Naked" edges (shared by only 1 triangle) = holes.
    /// 2. "Non-manifold" edges (shared by 3+ triangles) = internal faces.
    /// </remarks>
    public static bool IsMeshWatertight(Mesh mesh)
    {
        if (mesh == null)
        {
            Debug.LogWarning("Mesh is null. Cannot check if watertight.");
            return false;
        }

        int[] triangles = mesh.triangles;
        if (triangles.Length == 0)
        {
            // An empty mesh is not considered watertight
            return false;
        }

        // Dictionary to store edge counts.
        // Key: A tuple representing the edge (v1, v2) where v1 < v2.
        // Value: The number of times this edge has been seen.
        var edgeCounts = new Dictionary<ValueTuple<int, int>, int>();

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v1 = triangles[i + 0];
            int v2 = triangles[i + 1];
            int v3 = triangles[i + 2];

            // Normalize and add the three edges of the triangle
            AddEdge(edgeCounts, v1, v2);
            AddEdge(edgeCounts, v2, v3);
            AddEdge(edgeCounts, v3, v1);
        }

        // Now, check if all edge counts are exactly 2
        foreach (var count in edgeCounts.Values)
        {
            if (count != 2)
            {
                // Found an edge that is not shared by exactly two triangles.
                // count == 1 means it's a "naked" edge (a hole).
                // count > 2 means it's "non-manifold" (e.g., fins).
                return false; 
            }
        }

        // If all edge counts are 2, the mesh is watertight.
        return true;
    }

    /// <summary>
    /// Helper method to add an edge to the count dictionary.
    /// It normalizes the edge (v1, v2) so that the smaller index is always first.
    /// </summary>
    private static void AddEdge(Dictionary<ValueTuple<int, int>, int> edgeCounts, int v1, int v2)
    {
        // Normalize the edge by always putting the smaller index first
        var edge = v1 < v2 ? (v1, v2) : (v2, v1);

        if (edgeCounts.TryGetValue(edge, out int count))
        {
            edgeCounts[edge] = count + 1;
        }
        else
        {
            edgeCounts[edge] = 1;
        }
    }
}