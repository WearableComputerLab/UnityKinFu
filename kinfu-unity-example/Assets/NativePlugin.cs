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

    private void Awake() {
        RegisterPrintMessageCallback(PrintMessage, ((int)logLevel));    
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

    public void CloseCamera()
    {
        closeDevice();
    }
}
