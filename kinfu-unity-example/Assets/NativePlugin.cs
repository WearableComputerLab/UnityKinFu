using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Runtime.InteropServices;
using UnityEngine.Events;
using Unity.VisualScripting;
using System;
using UnityEngine.UI;
using UnityEngine.Rendering;
using System.IO;
using UnityEngine.UIElements;

public class NativePlugin : MonoBehaviour
{
    public static NativePlugin Instance
    {
        get;
        private set;
    }

    public RawImage colorImage;

    public UnityEvent<List<Vector3>> pointCloudUpdated;
    public UnityEvent<Matrix4x4> poseUpdated;

    Coroutine automaticUpdate;
    public TMPro.TMP_Text automaticUpdateLabel;

    public enum KinFuLogLevels
    {
        Critical = 0,
        Error,
        Warning,
        Information,
        Trace,
        Off
    }

    private Texture2D tex;
    private Color32[] pixel32;
    private GCHandle pixelHandle;
    private IntPtr pixelPtr;

    private float[] points;
    private GCHandle pointsHandle;
    private IntPtr pointsPtr;

    public TMPro.TMP_Text connectedLabel;
    public KinFuLogLevels logLevel = KinFuLogLevels.Warning;

    [DllImport("kinfuunity", EntryPoint = "getConnectedSensorCount")]
    public static extern int getConnectedSensorCount();
    
    [DllImport("kinfuunity", EntryPoint = "connectToDevice")]
    public static extern bool connectToDevice(int deviceIndex);

    [DllImport("kinfuunity", EntryPoint = "connectToDefaultDevice")]
    public static extern bool connectToDefaultDevice();

    [DllImport("kinfuunity", EntryPoint = "connectAndStartCameras")]
    public static extern int connectAndStartCameras();

    [DllImport("kinfuunity", EntryPoint = "setupConfigAndCalibrate")]
    public static extern bool setupConfigAndCalibrate();
    
    [DllImport("kinfuunity", EntryPoint = "startCameras")]
    public static extern bool startCameras();
    
    [DllImport("kinfuunity", EntryPoint = "captureFrame")]
    public static extern int captureFrame(IntPtr color_data, IntPtr point_data);
    
    [DllImport("kinfuunity", EntryPoint = "closeDevice")]
    public static extern void closeDevice();

    [DllImport("kinfuunity", EntryPoint = "getColorImageBytes")]
    private static extern void getColorImageBytes(IntPtr data, int width, int height);

    unsafe delegate void PoseDataCallback (float* matrix);
    [DllImport("kinfuunity", EntryPoint = "RegisterPoseDataCallback")]
    static extern void RegisterPoseDataCallback(PoseDataCallback callback);

    [DllImport("kinfuunity", EntryPoint = "requestPose")]
    static extern void requestPose();

    delegate void PrintMessageCallback(int level, string msg);

    [DllImport("kinfuunity", EntryPoint = "RegisterPrintMessageCallback")]
    static extern void RegisterPrintMessageCallback(PrintMessageCallback func, int level);
    
    [AOT.MonoPInvokeCallback(typeof(PrintMessageCallback))]
    static void PrintMessage(int level, string msg)
    {
        switch(level)
        {
            case 0:
            case 1:
                Debug.LogErrorFormat("Kinfu: {0}", msg);
                break;
            case 2:
                Debug.LogWarningFormat("Kinfu: {0}", msg);
                break;

            default:
                Debug.LogFormat("Kinfu: {0}", msg);
                break;
        }
    }

    private void ProcessPoints(int numPoints)
    {
        if (numPoints <= 0) return;

        List<Vector3> positions = new List<Vector3>(numPoints / 3);
        for (int i = 0; i < numPoints - 3; i += 3)
        {
            var point = new Vector3(points[i * 3], points[i * 3 + 1], points[i * 3 + 2]);

            if (point != Vector3.zero)
                positions.Add(point);
        }

        if (Instance.pointCloudUpdated != null)
        {
            Instance.pointCloudUpdated.Invoke(positions);
        }
    }

    [AOT.MonoPInvokeCallback(typeof(PoseDataCallback))]
    unsafe static void RecievePoseData(float* matrix)
    {
        // Copy values into internal buffers, as they will be freed after this method
        // This is a 4x4 transform for the camera
        Debug.LogFormat("Recieved pose matrix");
        Matrix4x4 poseMatrix = new Matrix4x4();
        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                poseMatrix[row, col] = matrix[row * 4 + col];
            }
        }

        if(Instance.poseUpdated != null)
        {
            Instance.poseUpdated.Invoke(poseMatrix);
        }
    }

    private void Awake() {

        if (Instance != null)
        {
            this.enabled = false;
            return;
        }

        Instance = this;

        RegisterPrintMessageCallback(PrintMessage, ((int)logLevel));
        unsafe
        {
            RegisterPoseDataCallback(RecievePoseData);
        }


        InitTexture();

        points = new float[1000000 * 3];
        //Pin points array
        pointsHandle = GCHandle.Alloc(points, GCHandleType.Pinned);
        //Get the pinned address
        pointsPtr = pointsHandle.AddrOfPinnedObject();
    }

    void Update()
    {
        var devices = getConnectedSensorCount();

        if (connectedLabel != null)
        {
            connectedLabel.text = string.Format("Connected Devices: {0}", devices);
        }
    }

    private void OnApplicationQuit()
    {
        CloseCamera();
        pixelHandle.Free();
    }

    public void ToggleAutomaticUpdate()
    {
        if (automaticUpdate == null)
        {
            automaticUpdate = StartCoroutine(AutomatedUpdateLoop());
            automaticUpdateLabel.text = "Stop Auto Updates"; 
        }
        else
        {
            StopCoroutine(automaticUpdate);
            automaticUpdate = null;
            automaticUpdateLabel.text = "Start Auto Updates";
        }
    }

    IEnumerator AutomatedUpdateLoop()
    {
        while (true)
        {
            CaptureFrame();
            //RequestPose();
            GetColorImage();

            yield return null;// new WaitForSecondsRealtime(1);
        }
    }
    
    void InitTexture()
    {
        tex = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
        pixel32 = tex.GetPixels32();
        //Pin pixel32 array
        pixelHandle = GCHandle.Alloc(pixel32, GCHandleType.Pinned);
        //Get the pinned address
        pixelPtr = pixelHandle.AddrOfPinnedObject();

        // Assign texture to renderer
        colorImage.texture = tex;
    }

    public void GetColorImage()
    {
        //Convert Mat to Texture2D
        getColorImageBytes(pixelPtr, tex.width, tex.height);
        //Update the Texture2D with array updated in C++
        tex.SetPixels32(pixel32);
        tex.Apply();
    }

    public void ConnectAndStartCameras()
    {
        var success = connectAndStartCameras();
        Debug.LogFormat("connectAndStartCameras: {0}", success);
    }

    public void ConnectCamera()
    {
        var success = connectToDefaultDevice();
        Debug.LogFormat("connectToDefaultDevice: {0}", success);
    }

    public void ConfigCamera()
    {
        var success = setupConfigAndCalibrate();
        Debug.LogFormat("setupConfigAndCalibrate: {0}", success);
    }

    public void StartCamera()
    {
        var success = startCameras();
        Debug.LogFormat("startCameras: {0}", success);
    }

    public void CaptureFrame()
    {
        var numPoints = captureFrame(pixelPtr, pointsPtr);
        tex.SetPixels32(pixel32);
        tex.Apply();

        ProcessPoints(numPoints);
    }

    public void RequestPose()
    {
        requestPose();
        Debug.Log("Pose Requested");
    }

    public void CloseCamera()
    {
        closeDevice();
        Debug.Log("Device Closed");
    }
}
