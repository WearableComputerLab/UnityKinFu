
using System;
using System.Runtime.InteropServices;

using fts;

using UnityEngine;

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
    public delegate int CaptureFrame(IntPtr color_data, IntPtr point_data, IntPtr pose_matrix_data);

    [PluginFunctionAttr("capturePointCloud")]
    public static CapturePointCloud capturePointCloud = null;
    public delegate int CapturePointCloud(IntPtr point_data);

    [PluginFunctionAttr("closeDevice")]
    public static CloseDevice closeDevice = null;
    public delegate void CloseDevice();

    [PluginFunctionAttr("reset")]
    public static ResetDevice resetDevice = null;
    public delegate void ResetDevice();

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
                Debug.LogFormat("{1} Kinfu: {0}", msg, DateTime.Now.ToString("hh:mm:ss.fff"));
                break;
        }
    }
}
