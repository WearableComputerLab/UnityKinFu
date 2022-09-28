using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpdateCameraTransform : MonoBehaviour
{
    public void OnTransformUpdate(Matrix4x4 matrix)
    {
        transform.localPosition = matrix.GetPosition();
        transform.localRotation = matrix.rotation;
        transform.localScale = matrix.lossyScale;
    }
}
