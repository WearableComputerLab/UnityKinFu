using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Runtime.InteropServices;
using UnityEngine.Events;
using System;
using UnityEngine.UI;
using fts;

[PluginAttr("kinfuunity")]
public static class KinFuUnity
{
    [PluginFunctionAttr("getConnectedSensorCount")]
    public static GetConnectedSensorCount getConnectedSensorCount = null;
    public delegate int GetConnectedSensorCount();

    [PluginFunctionAttr("connectToDevice")]
    public static ConnectToDevice connectToDevice = null;
    public delegate bool ConnectToDevice(int deviceIndex);

    [PluginFunctionAttr("connectToDefaultDevice")]
    public static ConnectToDefaultDevice connectToDefaultDevice = null;
    public delegate bool ConnectToDefaultDevice();

    [PluginFunctionAttr("connectAndStartCameras")]
    public static ConnectAndStartCameras connectAndStartCameras = null;
    public delegate int ConnectAndStartCameras();

    [PluginFunctionAttr("setupConfigAndCalibrate")]
    public static SetupConfigAndCalibrate setupConfigAndCalibrate = null;
    public delegate bool SetupConfigAndCalibrate();

    [PluginFunctionAttr("startCameras")]
    public static StartCameras startCameras = null;
    public delegate bool StartCameras();

    [PluginFunctionAttr("captureFrame")]
    public static CaptureFrame captureFrame = null;
    public delegate int CaptureFrame(IntPtr color_data);

    [PluginFunctionAttr("capturePointCloud")]
    public static CapturePointCloud capturePointCloud = null;
    public delegate int CapturePointCloud(IntPtr point_data);

    [PluginFunctionAttr("closeDevice")]
    public static CloseDevice closeDevice = null;
    public delegate void CloseDevice();

    [PluginFunctionAttr("reset")]
    public static ResetDevice resetDevice = null;
    public delegate void ResetDevice();

    [PluginFunctionAttr("getColorImageBytes")]
    public static GetColorImageBytes getColorImageBytes = null;
    public delegate void GetColorImageBytes(IntPtr data, int width, int height);

    [PluginFunctionAttr("requestPose")]
    public static RequestPose requestPose = null;
    public delegate void RequestPose(IntPtr pose_matrix_data);

    [PluginFunctionAttr("registerPrintMessageCallback")]
    public static RegisterPrintMessageCallback registerPrintMessageCallback = null;
    public delegate void RegisterPrintMessageCallback(PrintMessageCallback func, int level);
    public delegate void PrintMessageCallback(int level, string msg);

    [AOT.MonoPInvokeCallback(typeof(PrintMessageCallback))]
    public static void PrintMessage(int level, string msg)
    {
        switch (level)
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
}

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
    Coroutine automaticCloudUpdate;
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

    private float[] poseMatrixArray;
    private GCHandle poseMatrixArrayHandle;
    private IntPtr poseMatrixArrayPtr;

    public TMPro.TMP_Text connectedLabel;
    public KinFuLogLevels logLevel = KinFuLogLevels.Warning;

    private void Awake() {

        if (Instance != null)
        {
            this.enabled = false;
            return;
        }

        if (KinFuUnity.registerPrintMessageCallback == null)
        {
            Debug.LogError("KinFu DLL failed to load");
            this.enabled = false;
            return;
        }

        KinFuUnity.registerPrintMessageCallback(KinFuUnity.PrintMessage, ((int)logLevel));

        Instance = this;

        InitTexture();

        points = new float[1000000 * 3];
        //Pin points array
        pointsHandle = GCHandle.Alloc(points, GCHandleType.Pinned);
        //Get the pinned address
        pointsPtr = pointsHandle.AddrOfPinnedObject();

        // Pin pose matrix data
        poseMatrixArray = new float[16];
        poseMatrixArrayHandle = GCHandle.Alloc(poseMatrixArray, GCHandleType.Pinned);
        poseMatrixArrayPtr = poseMatrixArrayHandle.AddrOfPinnedObject();
    }

    void Update()
    {
        var devices = KinFuUnity.getConnectedSensorCount();

        if (connectedLabel != null)
        {
            connectedLabel.text = string.Format("Connected Devices: {0}", devices);
        }
    }

    private void OnApplicationQuit()
    {
        CloseCamera();
        pixelHandle.Free();
        pointsHandle.Free();
        poseMatrixArrayHandle.Free();
    }

    private void ProcessPoints(int numPoints)
    {
        if (numPoints <= 0)
        {
            Debug.LogFormat("No Points: {0}", numPoints);
            return;
        }

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

    public void ToggleAutomaticUpdate()
    {
        if (automaticUpdate == null)
        {
            automaticUpdate = StartCoroutine(AutomatedUpdateLoop());
            //automaticCloudUpdate = StartCoroutine(AutomatedCloudUpdateLoop());
            automaticUpdateLabel.text = "Stop Auto Updates"; 
        }
        else
        {
            StopCoroutine(automaticUpdate);
            automaticUpdate = null;
            //StopCoroutine(automaticCloudUpdate);
            //automaticCloudUpdate = null;
            automaticUpdateLabel.text = "Start Auto Updates";
        }
    }

    IEnumerator AutomatedUpdateLoop()
    {
        while (true)
        {
            CaptureFrame();
            CapturePoints();
            RequestPose();

            yield return null;// new WaitForSecondsRealtime(1.0f/15.0f);
        }
    }

    IEnumerator AutomatedCloudUpdateLoop()
    {
        while (true)
        {
            CapturePoints();

            yield return new WaitForSecondsRealtime(1.0f/15.0f);
        }
    }

    void InitTexture()
    {
        // NOTE: This is created to MATCH the Kinect buffers - and should either
        // a) be updated if this changes in the DLL
        // b) be passed into the DLL somehow to configure
        // c) be passed OUT of the DLL then created
        // 
        // But these are all tasks for later cleanup
        tex = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
        pixel32 = tex.GetPixels32();
        //Pin pixel32 array
        pixelHandle = GCHandle.Alloc(pixel32, GCHandleType.Pinned);
        //Get the pinned address
        pixelPtr = pixelHandle.AddrOfPinnedObject();

        // Assign texture to renderer
        colorImage.texture = tex;
    }

    public void ConnectAndStartCameras()
    {
        var success = KinFuUnity.connectAndStartCameras();
        Debug.LogFormat("connectAndStartCameras: {0} ({1})", success == 0, success);

        ToggleAutomaticUpdate();

    }

    public void ConnectCamera()
    {
        var success = KinFuUnity.connectToDefaultDevice();
        Debug.LogFormat("connectToDefaultDevice: {0}", success);
    }

    public void ConfigCamera()
    {
        var success = KinFuUnity.setupConfigAndCalibrate();
        Debug.LogFormat("setupConfigAndCalibrate: {0}", success);
    }

    public void StartCamera()
    {
        var success = KinFuUnity.startCameras();
        Debug.LogFormat("startCameras: {0}", success);
    }

    public void CaptureFrame()
    {
        var numPoints = KinFuUnity.captureFrame(pixelPtr);
        tex.SetPixels32(pixel32);
        tex.Apply();
    }

    public void CapturePoints()
    {
        var numPoints = KinFuUnity.capturePointCloud(pointsPtr);

        ProcessPoints(numPoints);
    }

    public void RequestPose()
    {
        KinFuUnity.requestPose(poseMatrixArrayPtr);

        Matrix4x4 poseMatrix = new Matrix4x4();
        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                poseMatrix[row, col] = poseMatrixArray[row * 4 + col];
            }
        }

        if (Instance.poseUpdated != null)
        {
            Instance.poseUpdated.Invoke(poseMatrix);
        }
    }

    public void CloseCamera()
    {
        KinFuUnity.closeDevice();
        Debug.Log("Device Closed");
    }

    public void ResetDevice()
    {
        KinFuUnity.resetDevice();
        Debug.Log("Device Reset");
    }
}
