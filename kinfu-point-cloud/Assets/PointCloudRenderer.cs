using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using System.IO;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PointCloudRenderer : MonoBehaviour
{
    Texture2D texColor;
    Texture2D texPosScale;
    VisualEffect vfx;
    uint resolution = 4096;

    public float particleSize = 0.01f;
    bool toUpdate = false;
    uint particleCount = 0;

    public Vector3 boundSize;
    public Vector3 boundCentre;

    Mesh mesh;
    Vector3[] vertices;
    Color[] colors;
    
    // public Mesh procMesh;

    List<Vector3> pointVertices= new List<Vector3>();
    List<Vector3> pointNormals = new List<Vector3>();

    void OnEnable(){
        mesh = new Mesh{ name = "procedural_mesh" };
        // mesh.vertices = new Vector3[]{
        //     Vector3.zero, Vector3.right, Vector3.up
        // };
        GetComponent<MeshFilter>().mesh = mesh; 
        // mesh.triangles = new int[] {0,2,1};
    }

    private void Start()
    {
        vfx = GetComponent<VisualEffect>();

        colors = new Color[(int)resolution * (int)resolution];
        // Use mesh rather than list of points
        // vertices = mesh.vertices;
        // SetParticles(vertices, colors);
        // Get file and convert to point and normal array
        string[] vertexPoints = ReadFile();
        // Use point list to create vector3 points
        SetVertices(vertexPoints);  
        // After we've assigned points we can build the cloud
        SetParticles(pointVertices, colors);
        
    }

    private void Update()
    {
        if (toUpdate)
        {
            toUpdate = false;

            vfx.Reinit();
            vfx.SetUInt(Shader.PropertyToID("ParticleCount"), particleCount);
            vfx.SetTexture(Shader.PropertyToID("TexColor"), texColor);
            vfx.SetTexture(Shader.PropertyToID("TexPosScale"), texPosScale);
            vfx.SetUInt(Shader.PropertyToID("Resolution"), resolution);
            vfx.SetVector3("BoundsSize", boundSize);
            vfx.SetVector3("BoundsCentre", boundCentre);
        }
    }

    // public void SetParticles(Vector3[] positions, Color[] colors)
    public void SetParticles(List<Vector3> positions, Color[] colors)
    {
        texColor = new Texture2D(positions.Count > (int)resolution ? (int)resolution : positions.Count, Mathf.Clamp(positions.Count / (int)resolution, 1, (int)resolution), TextureFormat.RGBAFloat, false);
        texPosScale = new Texture2D(positions.Count > (int)resolution ? (int)resolution : positions.Count, Mathf.Clamp(positions.Count / (int)resolution, 1, (int)resolution), TextureFormat.RGBAFloat, false);

        int texWidth = texColor.width;
        int texHeight = texColor.height;

        for (int y = 0; y < texHeight; y++)
        {
            for (int x = 0; x < texWidth; x++)
            {
                int index = x + y * texWidth;

                // 3 is an arbitrary number, if we can get the furthest positions[index].z from origin (0,0,0)
                // we should use that
                float percentageDistance = decimalPercent(positions[index].z, 0, 3);

                float difference = (percentageDistance - 1) * -1;

                texColor.SetPixel(x, y, new Color(difference, difference, difference, 1));

                var data = new Color(positions[index].x, positions[index].y, positions[index].z, particleSize);
                texPosScale.SetPixel(x, y, data);

            }
        }

        texColor.Apply();
        texPosScale.Apply();
        particleCount = (uint)positions.Count;
        toUpdate = true;
    }

    /// Set a decimal percentage (1 = 100%, 0.5 = 50% etc.) of passed value between min/max
    /// Used as we are using HDRP which uses 0.0 - 1.0 for RGB values and anything >1.0 
    /// sets intensity
    float decimalPercent(float val, float min, float max)
    {

        return (val - min) / (max - min);
    }

    string ReadFile(){
        // File to read in and pass to our streamReader
        string filePath = "Assets/kf_output.ply";
        StreamReader streamReader = new StreamReader(filePath);
        string pointFile = streamReader.ReadToEnd();
        // Split into array to seperate header and point data
        string[] headerRemoved = pointFile.Split("end_header");
        // Stringify the first element which contains point and normal data
        string dataString = headerRemoved[1];
        // Split based on new lines
        // string[] seperateLines = dataString.Split("\n");
        streamReader.Close();
        return dataString.Split("\n");
        // GilgaMesh(pointVertices);
    }

    void SetVertices(string[] points){
        //First and last lines are empty so we start i at 1 and go to length-1
        for (int i = 1; i < points.Length - 1; i++)
        {
            // Split based on space
            string[] buildIt = points[i].Split(" ");
            // First three elements are x, y, z points
            float pointX = float.Parse(buildIt[0]);
            float pointY = float.Parse(buildIt[1]);
            float pointZ = float.Parse(buildIt[2]);
            // which we map to a new vector and add to our list
            pointVertices.Add(new Vector3(pointX, pointY, pointZ));
            // Implement normals? currently some are getting passed as nan's
            // this causes the below to error. 
            // float nX = float.Parse(buildIt[3]);
            // float nY = float.Parse(buildIt[4]);
            // float nZ = float.Parse(buildIt[5]);
            // pointNormals.Add(new Vector3(nX,nY,nZ));
        }
    }

    void GilgaMesh(List<Vector3> points){
        Vector3[] array = points.ToArray();
        // Debug.Log(array);

        // var mesh = new Mesh{ name = "procedural_mesh" };
        mesh.vertices = array;
        for (int i = 0; i < array.Length; i++)
        {
            Debug.Log(array[i]);
        }
        // GetComponent<MeshFilter>().mesh = mesh; 
        mesh.triangles = new int[] {0,2,1};
    }
}