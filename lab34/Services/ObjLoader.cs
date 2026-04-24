using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using lab34.Models;

namespace lab34.Services
{
    public static class ObjLoader
    {
        public static ModelData Load(string filePath)
        {
            var vertices = new List<Vector3>();
            var fileNormals = new List<Vector3>();
            var faces = new List<Face>();
            var culture = CultureInfo.InvariantCulture;

            foreach (string line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                ReadOnlySpan<char> span = line.AsMemory().Span.Trim();
                if (span.IsEmpty || span[0] == '#') continue;

                if (span.StartsWith("vn "))
                {
                    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        fileNormals.Add(Vector3.Normalize(new Vector3(
                            float.Parse(parts[1], culture),
                            float.Parse(parts[2], culture),
                            float.Parse(parts[3], culture)
                        )));
                    }
                }
                else if (span.StartsWith("v "))
                {
                    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        vertices.Add(new Vector3(
                            float.Parse(parts[1], culture),
                            float.Parse(parts[2], culture),
                            float.Parse(parts[3], culture)
                        ));
                    }
                }
                else if (span.StartsWith("f "))
                {
                    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        var (i1, n1) = ParseObjToken(parts[1]);
                        var (i2, n2) = ParseObjToken(parts[2]);
                        var (i3, n3) = ParseObjToken(parts[3]);
                        faces.Add(new Face(i1, i2, i3, n1, n2, n3));

                        if (parts.Length >= 5)
                        {
                            var (i4, n4) = ParseObjToken(parts[4]);
                            faces.Add(new Face(i1, i3, i4, n1, n3, n4));
                        }
                    }
                }
            }

            Vector3[] normals;
            if (fileNormals.Count > 0)
            {
                normals = fileNormals.ToArray();
            }
            else
            {
                normals = ComputeSmooth(vertices, faces);
                var updatedFaces = new List<Face>();
                foreach (var f in faces)
                    updatedFaces.Add(new Face(f.V1, f.V2, f.V3, f.V1, f.V2, f.V3));
                faces = updatedFaces;
            }

            return new ModelData
            {
                Vertices = vertices.ToArray(),
                Normals = normals,
                Faces = faces.ToArray()
            };
        }

        private static (int vIdx, int nIdx) ParseObjToken(string token)
        {
            string[] parts = token.Split('/');

            int vIdx = int.Parse(parts[0]);
            vIdx = vIdx > 0 ? vIdx - 1 : 0;

            int nIdx = -1;
            if (parts.Length >= 3 && parts[2].Length > 0)
            {
                nIdx = int.Parse(parts[2]);
                nIdx = nIdx > 0 ? nIdx - 1 : 0;
            }

            return (vIdx, nIdx);
        }

        private static Vector3[] ComputeSmooth(List<Vector3> vertices, List<Face> faces)
        {
            var normals = new Vector3[vertices.Count];

            foreach (var face in faces)
            {
                Vector3 v1 = vertices[face.V1];
                Vector3 v2 = vertices[face.V2];
                Vector3 v3 = vertices[face.V3];

                Vector3 faceNormal = Vector3.Cross(v2 - v1, v3 - v1);
                normals[face.V1] += faceNormal;
                normals[face.V2] += faceNormal;
                normals[face.V3] += faceNormal;
            }

            for (int i = 0; i < normals.Length; i++)
            {
                if (normals[i] != Vector3.Zero)
                    normals[i] = Vector3.Normalize(normals[i]);
            }

            return normals;
        }
    }
}