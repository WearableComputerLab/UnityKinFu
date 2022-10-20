
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;


public class KinectFusion : MonoBehaviour
{
    public static KinectFusion Instance
    {
        get;
        private set;
    }

    public RawImage colorImage;

    public UnityEvent<List<Vector3>> pointCloudUpdated;
    public UnityEvent<Matrix4x4> poseUpdated;

    Coroutine updateConnectedDevices;
    Coroutine automaticUpdate;
    Coroutine automaticCloudUpdate;
    public TMPro.TMP_Text automaticUpdateLabel;


    public GameObject connectButton;

    public GameObject connectedUI;

    [Range(50, 500)]
    public int SleepTime = 100;

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

    private void Awake()
    {

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

        StartCheckingForDevices();
    }

    private void Update()
    {
        if (updateThread != null && updateThread.IsAlive)
        {
            if (updateImage)
            {
                UpdateColorImage();

                UpdateCameraPose();
            }

            if (numPoints > 0)
            {
                ProcessPoints(numPoints);
            }
        }
    }

    ///

    void StartCheckingForDevices()
    {
        if (updateConnectedDevices != null) return;

        connectedLabel.gameObject.SetActive(true);
        updateConnectedDevices = StartCoroutine(CheckForDevices());

        // Show UIs
        connectButton.SetActive(true);
        connectedUI.SetActive(false);
    }

    void StopCheckingForDevices()
    {
        if (updateConnectedDevices == null) return;

        connectedLabel.gameObject.SetActive(false);

        StopCoroutine(updateConnectedDevices);
        updateConnectedDevices = null;

        // Hide UI button
        connectButton.SetActive(false);
        connectedUI.SetActive(true);
    }

    IEnumerator CheckForDevices()
    {
        int numDevices = 0;
        while (true)
        {
            numDevices = KinFuUnity.getConnectedSensorCount();

            if (connectedLabel != null)
            {
                connectedLabel.text = string.Format("Connected Devices: {0}", numDevices);
            }
            yield return new WaitForSecondsRealtime(1);
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
        lock (threadLock)
        {
            for (int i = 0; i < numPoints - 3; i += 3)
            {
                var point = new Vector3(points[i * 3], points[i * 3 + 1], points[i * 3 + 2]);

                positions.Add(point);
            }
        }

        if (Instance.pointCloudUpdated != null)
        {
            Instance.pointCloudUpdated.Invoke(positions);
        }

        this.numPoints = 0;
    }

    void UpdateColorImage()
    {
        lock (threadLock)
        {
            tex.SetPixels32(pixel32);
            tex.Apply();
        }

        updateImage = false;
    }

    void UpdateCameraPose()
    {
        Matrix4x4 poseMatrix = new Matrix4x4();

        lock (threadLock)
        {
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    poseMatrix[row, col] = poseMatrixArray[row * 4 + col];
                }
            }
        }

        if (Instance.poseUpdated != null)
        {
            Instance.poseUpdated.Invoke(poseMatrix);
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

    Thread updateThread;

    public void ConnectAndStartCameras()
    {
        var success = KinFuUnity.connectAndStartCameras();
        Debug.LogFormat("connectAndStartCameras: {0} ({1})", success == 0, success);

        StopCheckingForDevices();

        updateThread = new Thread(new ThreadStart(UpdateKinectThread));
        Debug.LogFormat("Starting update thread");
        runUpdateThread = true;
        updateThread.Start();
    }

    public void CloseCamera()
    {
        if (updateThread != null && updateThread.IsAlive)
        {
            runUpdateThread = false;
            updateThread.Join();
            updateThread = null;
        }

        KinFuUnity.closeDevice();
        Debug.Log("Device Closed");
        StartCheckingForDevices();
    }

    public void ResetDevice()
    {
        lock (threadLock)
        {
            KinFuUnity.resetDevice();
        }

        Debug.Log("Device Reset");
    }

    static object threadLock = new object();
    static bool runUpdateThread = true;

    bool updateImage = false;
    int numPoints = 0;

    static void UpdateKinectThread()
    {
        while (runUpdateThread)
        {
            lock (threadLock)
            {
                var updateSuccess = KinFuUnity.captureFrame(Instance.pixelPtr);

                // This is a fatal status and we need to close the device
                // K4A_WAIT_RESULT_FAILED
                if (updateSuccess == -2)
                {
                    Instance.CloseCamera();
                    break;
                }

                Instance.updateImage = updateSuccess == 1;

                Instance.numPoints = KinFuUnity.capturePointCloud(Instance.pointsPtr);
                KinFuUnity.requestPose(Instance.poseMatrixArrayPtr);
            }

            Thread.Sleep(Instance.SleepTime);
        }
    }
}
