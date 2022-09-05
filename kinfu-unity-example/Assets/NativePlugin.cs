using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Runtime.InteropServices;

public class NativePlugin : MonoBehaviour
{
    [DllImport("kinfuunity", EntryPoint = "getConnectedSensorCount")]
    public static extern int getConnectedSensorCount();

    
    [DllImport("kinfuunity", EntryPoint = "sumNumbers")]
    public static extern int sumNumbers(int a, int b);

    // Start is called before the first frame update
    void Start()
    {
        Debug.LogFormat("Connected Devices: {0}", getConnectedSensorCount());

        
        Debug.Log(sumNumbers(3, 5));
        
    }
}
