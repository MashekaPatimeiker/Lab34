using System.Numerics;

namespace lab34.Models
{
    public struct Face
    {
        public int V1, V2, V3;   
        public int T1, T2, T3;
        public int N1, N2, N3;   

        public Face(int v1, int v2, int v3, int t1, int t2, int t3, int n1, int n2, int n3)
        {
            V1 = v1; V2 = v2; V3 = v3;
            T1 = t1; T2 = t2; T3 = t3;
            N1 = n1; N2 = n2; N3 = n3;
        }
    }

    public class ModelData
    {
        public Vector3[] Vertices { get; set; }
        public Vector2[] TexCoords { get; set; } 
        public Vector3[] Normals { get; set; }   
        public Face[] Faces { get; set; }
    }
}