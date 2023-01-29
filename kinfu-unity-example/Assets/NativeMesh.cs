using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.InteropServices;
using System.IO;

public class NativeMesh : MonoBehaviour
{
    
    [DllImport("MeshReconstruction")]
    public static extern unsafe bool reconstructFileCloud(string path, bool recalculateNormals);
    
    [DllImport("MeshReconstruction")]
    public static extern unsafe bool reconstruct(float* inputVertices, float* inputNormals, int verticesCount, bool recalculateNormals);

    [DllImport("MeshReconstruction")]
    public static extern unsafe int getMeshVerticesCount();

    [DllImport("MeshReconstruction")]
    public static extern unsafe int getMeshTrianglesCount();

    [DllImport("MeshReconstruction")]
    public static extern unsafe void getMesh(float* vertices, int* triangles, float* normals);

    [SerializeField] private string plyFilePath;

    bool readPointCloudFromFile(string path, List<Vector3> vertices, List<Vector3> normals)
    {
        StreamReader streamReader = new StreamReader(path);
        string pointFile = streamReader.ReadToEnd();
        string[] headerRemoved = pointFile.Split("end_header");
        if (headerRemoved.Length < 2)
        {
            return false;
        }
        string dataString = headerRemoved[1];
        streamReader.Close();
        var points = dataString.Split("\n");
        
        // Adding vertices and normals to points
        for (int i = 1; i < points.Length - 1; i++)
        {
            string[] vertexAndNormal = points[i].Split(" ");
            if (vertexAndNormal.Length < 3)
            {
                continue;
            }

            float pointX = float.Parse(vertexAndNormal[0]);
            float pointY = float.Parse(vertexAndNormal[1]);
            float pointZ = float.Parse(vertexAndNormal[2]);
            vertices.Add(new Vector3(pointX, pointY, pointZ));

            if (vertexAndNormal.Length < 6)
            {
                continue;
            }
            if (vertexAndNormal[3] == "nan")
            {
                normals.Add(new Vector3(0, 0, 0));
            }
            else
            {
                float normalX = float.Parse(vertexAndNormal[3]);
                float normalY = float.Parse(vertexAndNormal[4]);
                float normalZ = float.Parse(vertexAndNormal[5]);
                normals.Add(new Vector3(normalX, normalY, normalZ));
            }
        }

        // Make sure vertices and normals are equal
        if (vertices.Count != normals.Count)
        {
            normals.Clear();
        }

        return true;
    }

    float [] getFloatArrayFromPointsList(List<Vector3> points)
    {
        float[] res = new float[points.Count * 3];
        for (int i = 0; i < points.Count; ++i)
        {
            res[i * 3 + 0] = points[i].x;
            res[i * 3 + 1] = points[i].y;
            res[i * 3 + 2] = points[i].z;
        }
        return res;
    }

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("Starting.");

        if(plyFilePath.Length == 0)
        {
            Debug.Log("Please provide PLY file path.");
            return;
        }

        List<Vector3> inputVerticesList = new List<Vector3>();
        List<Vector3> inputNormalsList = new List<Vector3>();

        bool fileReadSucceded = readPointCloudFromFile(plyFilePath, inputVerticesList, inputNormalsList);
        if (!fileReadSucceded)
        {
            Debug.Log("Unable to read point cloud from file");
            return;
        }

        if (inputVerticesList.Count == 0)
        {
            Debug.Log("Vertices list is empty");
            return;
        }

        //print to see if it points read correctly
        Debug.Log("~~~~~~~~~~");
        printPoint(inputVerticesList[0]);
        printPoint(inputNormalsList[0]);
        Debug.Log("~~~~~~~~~~");
        printPoint(inputVerticesList[inputVerticesList.Count - 1]);
        printPoint(inputNormalsList[inputNormalsList.Count - 1]);
        Debug.Log("~~~~~~~~~~");

        float[] inputVerticesArray = getFloatArrayFromPointsList(inputVerticesList);
        float[] inputNormalsArray = getFloatArrayFromPointsList(inputNormalsList);

        // bool success = reconstructFileCloud(plyFilePath, true);
        bool success = false;
        unsafe
        {
            fixed (float* inputVerticesArrayPtr = inputVerticesArray)
            {
                fixed (float* inputNormalsArrayPtr = inputNormalsArray)
                {
                    success = reconstruct(inputVerticesArrayPtr, inputNormalsArrayPtr, inputVerticesList.Count, true);
                }
            }
        }
               
        if (success)
        {
            Debug.Log("Reconstruction successful.");
            int verticesCount = getMeshVerticesCount();
            int trianglesCount = getMeshTrianglesCount();

            Debug.Log("Vertices count: " +  verticesCount);
            Debug.Log("Triangles count: " +  trianglesCount);

            float[] verticesBuffer = new float[verticesCount * 3];
            int[] trianglesBuffer = new int[trianglesCount * 3];
            float[] normalsBuffer = new float[verticesCount * 3];

            unsafe
            {
                fixed (float* verticesBufferPtr = verticesBuffer)
                {
                    fixed (int* trianglesBufferPtr = trianglesBuffer)
                    {
                        fixed (float* normalsBufferPtr = normalsBuffer)
                        {
                            getMesh(verticesBufferPtr, trianglesBufferPtr, normalsBufferPtr);
                        }
                    }
                }
            }
            //Reconstruct mesh
            Mesh mesh = new Mesh();

            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            Vector3[] vertices = new Vector3[verticesCount];
            Vector3[] normals = new Vector3[verticesCount];
            for (int i = 0; i < verticesCount; i++)
            {
                vertices[i] = new Vector3(verticesBuffer[i * 3 + 0], verticesBuffer[i * 3 + 1], verticesBuffer[i * 3 + 2]);
                normals[i] = new Vector3(normalsBuffer[i * 3 + 0], normalsBuffer[i * 3 + 1], normalsBuffer[i * 3 + 2]);
            }
            mesh.vertices = vertices;
            mesh.normals = normals;

            int[] trianglesDoubled = new int[trianglesCount * 6];
            for (int i = 0; i < trianglesCount * 3; ++i)
            {
                trianglesDoubled[i] = trianglesBuffer[i];
                trianglesDoubled[i + trianglesCount * 3] = trianglesBuffer[trianglesCount * 3 - 1 - i];
            }
            mesh.triangles = trianglesDoubled;


            GameObject gameObject = new GameObject("Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            
            gameObject.GetComponent<MeshFilter>().mesh = mesh;

            MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
            meshRenderer.material = new Material(Shader.Find("Diffuse"));
        }
        else
        {
            Debug.Log("Unable to generate mesh.");
        }
    }

    void printPoint(Vector3 point)
    {
        Debug.Log($"X: {point.x}\nY: {point.y}\nZ: {point.z}");
    }
}