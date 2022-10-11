using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpdateCameraTransform : MonoBehaviour
{
    public enum TransformType
    {
        Raw,
        LookRotation,
        FlipYEuler,
        FlipXEuler,
    }

    public TransformType type;
    public bool applyTranslation;

    public void OnTransformUpdate(Matrix4x4 matrix)
    {
        // NOTE: OpenCV has +Y as down, so we have had to add -1 to some scales on the GameObjects
        // and we need to account in other places. Ideally, we would apply this when capturing the data.
        Quaternion rot = Quaternion.identity;
        
        switch (type)
        {
            case TransformType.Raw: 
                rot = matrix.rotation; 
                break;

            case TransformType.LookRotation:
                rot = Quaternion.LookRotation(new Vector3(matrix.m20, -matrix.m21, matrix.m22), new Vector3(matrix.m10, -matrix.m11, matrix.m12));
                break;

            case TransformType.FlipYEuler:
                rot = Quaternion.Euler(matrix.rotation.eulerAngles.x, -matrix.rotation.eulerAngles.y, matrix.rotation.eulerAngles.z);
                break;

            case TransformType.FlipXEuler:
                rot = Quaternion.Euler(-matrix.rotation.eulerAngles.x, matrix.rotation.eulerAngles.y, matrix.rotation.eulerAngles.z);
                break;
        }

        transform.rotation = rot;

        if (applyTranslation)
        {
            var openCvPosition = matrix.GetPosition();
            var newPosition = new Vector3(openCvPosition.x, -openCvPosition.y, openCvPosition.z);
            transform.position = newPosition;
        }
    }
}
