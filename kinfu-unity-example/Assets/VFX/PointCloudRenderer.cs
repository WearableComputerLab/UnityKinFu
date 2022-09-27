using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using System.IO;
using UnityEditor;

[RequireComponent(typeof(VisualEffect))]
public class PointCloudRenderer : MonoBehaviour
{
    Texture2D texColor;
    Texture2D texPosScale;
    VisualEffect vfx;
    uint resolution = 4096;

    public float particleSize = 0.01f;
    bool toUpdate = false;
    uint particleCount = 0;

    // if camera cannot see bounds (determined via boundsSize) it
    // will clip pointcloud
    public Vector3 boundsSize;
    public Vector3 boundsCentre;

    // Assign an object in the editor (*.ply, etc.)
    public Object pointCloudObject;

    //List for dynamic assignment
    List<Vector3> pointVertices= new List<Vector3>();
    List<Vector3> pointNormals = new List<Vector3>();

    private void Start() {
        vfx = GetComponent<VisualEffect>();
        if (pointCloudObject != null)
        {
            // Get file and convert to point and normal array
            string[] vertexPoints = ReadFile();
            // Use point list to create vector3 points
            SetVertices(vertexPoints);
            // After we've assigned points we can build the cloud
            SetParticles(pointVertices);
        }
    }

    private void Update() {
        if (toUpdate) UpdateParticles();
    }

    /// Reads the file and splits it up returning an array of strings
    /// which should represent points in the point cloud
    string[] ReadFile() {
        // Object passed to streamreader which grabs the file path from the object itself
        StreamReader streamReader = new StreamReader(AssetDatabase.GetAssetPath(pointCloudObject));
        string pointFile = streamReader.ReadToEnd();
        // Split into array to seperate header and point data
        string[] headerRemoved = pointFile.Split("end_header");
        // Stringify the first element which contains point and normal data
        string dataString = headerRemoved[1];
        streamReader.Close();
        // Split on newlines and return
        return dataString.Split("\n");
    }

    /// Takes an array of strings that should represent points in the cloud
    /// splits them at spaces ' '
    /// and then adds a new vector3 to our pointcloud vertices
    void SetVertices(string[] points){
        //First and last lines are empty so we start i at 1 and go to length-1
        for (int i = 1; i < points.Length - 1; i++) {
            // Split based on space
            string[] coordinates = points[i].Split(" ");
            // First three elements are x, y, z points
            float pointX = float.Parse(coordinates[0]);
            float pointY = float.Parse(coordinates[1]);
            float pointZ = float.Parse(coordinates[2]);
            // which we map to a new vector and add to our list
            pointVertices.Add(new Vector3(pointX, pointY, pointZ));
        }
    }

    /// Creates a particle representation of our points
    public void SetParticles(List<Vector3> positions) {
        texColor = new Texture2D(positions.Count > (int)resolution ? (int)resolution : positions.Count, Mathf.Clamp(positions.Count / (int)resolution, 1, (int)resolution), TextureFormat.RGBAFloat, false);
        texPosScale = new Texture2D(positions.Count > (int)resolution ? (int)resolution : positions.Count, Mathf.Clamp(positions.Count / (int)resolution, 1, (int)resolution), TextureFormat.RGBAFloat, false);

        int texWidth = texColor.width;
        int texHeight = texColor.height;

        Bounds cloudBounds = new Bounds();

        float minZ = 0f;
        foreach (var position in positions)
        {
            if (position.z > minZ) minZ = position.z;

            cloudBounds.Encapsulate(position);
        }

        for (int y = 0; y < texHeight; y++) {
            for (int x = 0; x < texWidth; x++) {
                int index = x + y * texWidth;
                Vector3 point = positions[index];

                float percentageDistance = decimalPercent(point.z, 0, minZ);
                // When colouring pixels we set this so that closest renders lighter than further away pixels from origin
                float difference = (percentageDistance - 1) * -1;

                texColor.SetPixel(x, y, new Color(difference, difference, difference, 1));

                var data = new Color(point.x, point.y, point.z, particleSize);
                texPosScale.SetPixel(x, y, data);
            }
        }

        boundsCentre = cloudBounds.center;
        boundsSize = cloudBounds.size;

        texColor.Apply();
        texPosScale.Apply();
        particleCount = (uint)positions.Count;
        toUpdate = true;
    }

    /// Updates visual effect shader properties if needed
    void UpdateParticles() {
        toUpdate = false;

        vfx.Reinit();
        vfx.SetUInt(Shader.PropertyToID("ParticleCount"), particleCount);
        vfx.SetTexture(Shader.PropertyToID("TexColor"), texColor);
        vfx.SetTexture(Shader.PropertyToID("TexPosScale"), texPosScale);
        vfx.SetUInt(Shader.PropertyToID("Resolution"), resolution);
        vfx.SetVector3("BoundsSize", boundsSize);
        vfx.SetVector3("BoundsCentre", boundsCentre);
    }

    /// Set a decimal percentage (1 = 100%, 0.5 = 50% etc.) of passed value between min/max
    /// Used as we are using HDRP which uses 0.0 - 1.0 for RGB values and anything >1.0 
    /// sets intensity
    float decimalPercent(float val, float min, float max) {
        return (val - min) / (max - min);
    }
}