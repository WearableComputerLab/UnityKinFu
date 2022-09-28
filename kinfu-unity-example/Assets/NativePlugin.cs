using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Runtime.InteropServices;
using UnityEngine.Events;
using Unity.VisualScripting;

public class NativePlugin : MonoBehaviour
{
    public static NativePlugin Instance
    {
        get;
        private set;
    }

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
    public static extern bool captureFrame();
    
    [DllImport("kinfuunity", EntryPoint = "closeDevice")]
    public static extern void closeDevice();


    unsafe delegate void CloudDataCallback (int count, float* points, float* normals);

    [DllImport("kinfuunity", EntryPoint = "RegisterCloudDataCallback")]
    static extern void RegisterCloudDataCallback(CloudDataCallback callback);

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

    [AOT.MonoPInvokeCallback(typeof(CloudDataCallback))]
    unsafe static void RecieveCloudData(int count, float* points, float* normals)
    {
        // Copy values into internal buffers, as they will be freed after this method
        List<Vector3> positions = new List<Vector3>(count);
        List<Vector3> vertexNormals = new List<Vector3>(count);
        Debug.LogFormat("Recieved {0} points in cloud", count);
        for (int i = 0; i < count; i++)
        {
            positions.Add(new Vector3(points[i * 3], points[i * 3 + 1], points[i * 3 + 2]));
            vertexNormals.Add(new Vector3(normals[i * 3], normals[i * 3 + 1], normals[i * 3 + 2]));
        }

        if (Instance.pointCloudUpdated != null)
        {
            Instance.pointCloudUpdated.Invoke(positions);
        }
    }

    [AOT.MonoPInvokeCallback(typeof(CloudDataCallback))]
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
            RegisterCloudDataCallback(RecieveCloudData);
            RegisterPoseDataCallback(RecievePoseData);
        }
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
            RequestPose();

            yield return new WaitForSecondsRealtime(1);
        }
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
        var success = captureFrame();
        Debug.LogFormat("captureFrame: {0}", success);
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
