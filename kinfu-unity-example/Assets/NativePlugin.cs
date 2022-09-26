using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Runtime.InteropServices;

public class NativePlugin : MonoBehaviour
{
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
        // Copy values into internal buffers, as they will be freed after this method;
        Debug.LogFormat("Recieved {0} points in cloud", count);
    }

    [AOT.MonoPInvokeCallback(typeof(CloudDataCallback))]
    unsafe static void RecievePoseData(float* matrix)
    {
        // Copy values into internal buffers, as they will be freed after this method
        // This is a 4x4 transform for the camera
        Debug.LogFormat("Recieved pose matrix");
    }

    private void Awake() {
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
