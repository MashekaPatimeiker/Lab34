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
            var texCoords = new List<Vector2>();
            var normals = new List<Vector3>();
            var faces = new List<Face>();
            var culture = CultureInfo.InvariantCulture;

            foreach (string line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                if (parts[0] == "v") vertices.Add(new Vector3(float.Parse(parts[1], culture), float.Parse(parts[2], culture), float.Parse(parts[3], culture)));
                else if (parts[0] == "vt") texCoords.Add(new Vector2(float.Parse(parts[1], culture), float.Parse(parts[2], culture)));
                else if (parts[0] == "vn") normals.Add(Vector3.Normalize(new Vector3(float.Parse(parts[1], culture), float.Parse(parts[2], culture), float.Parse(parts[3], culture))));
                else if (parts[0] == "f")
                {
                    var p1 = ParseToken(parts[1]);
                    var p2 = ParseToken(parts[2]);
                    var p3 = ParseToken(parts[3]);
                    faces.Add(new Face(p1.v, p2.v, p3.v, p1.t, p2.t, p3.t, p1.n, p2.n, p3.n));
                    if (parts.Length > 4) {
                        var p4 = ParseToken(parts[4]);
                        faces.Add(new Face(p1.v, p3.v, p4.v, p1.t, p3.t, p4.t, p1.n, p3.n, p4.n));
                    }
                }
            }
            return new ModelData { 
                Vertices = vertices.ToArray(), 
                TexCoords = texCoords.ToArray(), 
                Normals = normals.ToArray(), 
                Faces = faces.ToArray() 
            };
        }

        private static (int v, int t, int n) ParseToken(string token)
        {
            string[] p = token.Split('/');
            int v = int.Parse(p[0]) - 1;
            int t = (p.Length > 1 && p[1] != "") ? int.Parse(p[1]) - 1 : 0;
            int n = (p.Length > 2 && p[2] != "") ? int.Parse(p[2]) - 1 : 0;
            return (v, t, n);
        }
    }
}