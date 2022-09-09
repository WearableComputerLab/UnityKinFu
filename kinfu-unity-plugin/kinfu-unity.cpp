// kinfu-unity.cpp : Defines the exported functions for the DLL.
//

#include "pch.h"
#include "framework.h"
#include "kinfu-unity.h"
#include "kinfu-helpers.h"

#include <stdio.h>
#include <fstream>
#include <sstream>
#include <vector>
#include <algorithm>
#include <k4a/k4a.h>
#include <math.h>

using namespace std;

#include <opencv2/core.hpp>
#include <opencv2/calib3d.hpp>
#include <opencv2/opencv.hpp>
#include <opencv2/rgbd.hpp>
#include <opencv2/viz.hpp>
using namespace cv;

// The currently connected device
k4a_device_t device = NULL;

// Configure the depth mode and fps
k4a_device_configuration_t config = K4A_DEVICE_CONFIG_INIT_DISABLE_ALL;

k4a_calibration_t calibration;

k4a_image_t lut = NULL;

Ptr<kinfu::KinFu> kf;

pinhole_t pinhole;
interpolation_t interpolation_type = INTERPOLATION_BILINEAR_DEPTH;

// For the point cloud
UMat points;
UMat normals;

int getConnectedSensorCount()
{
    return k4a_device_get_installed_count();
}

bool connectToDevice(int deviceIndex)
{

    if (K4A_RESULT_SUCCEEDED != k4a_device_open(deviceIndex, &device))
    {
        printf("Failed to open device\n");
        k4a_device_close(device);
        return false;
    }

    return true;
}

inline bool connectToDefaultDevice()
{
    return connectToDevice(K4A_DEVICE_DEFAULT);
}

bool setupConfigAndCalibrate()
{
    config = K4A_DEVICE_CONFIG_INIT_DISABLE_ALL;
    config.depth_mode = K4A_DEPTH_MODE_NFOV_UNBINNED;
    config.camera_fps = K4A_FRAMES_PER_SECOND_30;

    // Retrive calibration
    if (K4A_RESULT_SUCCEEDED !=
        k4a_device_get_calibration(device, config.depth_mode, config.color_resolution, &calibration))
    {
        printf("Failed to get calibration\n");
        k4a_device_close(device);
        return false;
    }

    return true;
}

bool startCameras()
{
    if (K4A_RESULT_SUCCEEDED != k4a_device_start_cameras(device, &config))
    {
        printf("Failed to start device\n");
        k4a_device_close(device);
        return false;
    }

    // Generate a pinhole model for depth camera
    pinhole = create_pinhole_from_xy_range(&calibration, K4A_CALIBRATION_TYPE_DEPTH);

    setUseOptimized(true);

    // Retrieve calibration parameters
    k4a_calibration_intrinsic_parameters_t *intrinsics = &calibration.depth_camera_calibration.intrinsics.parameters;
    const int width = calibration.depth_camera_calibration.resolution_width;
    const int height = calibration.depth_camera_calibration.resolution_height;

    // Initialize kinfu parameters
    Ptr<kinfu::Params> params;
    params = kinfu::Params::defaultParams();
    initialize_kinfu_params(
        *params, width, height, pinhole.fx, pinhole.fy, pinhole.px, pinhole.py);

    // Distortion coefficients
    Matx<float, 1, 8> distCoeffs;
    distCoeffs(0) = intrinsics->param.k1;
    distCoeffs(1) = intrinsics->param.k2;
    distCoeffs(2) = intrinsics->param.p1;
    distCoeffs(3) = intrinsics->param.p2;
    distCoeffs(4) = intrinsics->param.k3;
    distCoeffs(5) = intrinsics->param.k4;
    distCoeffs(6) = intrinsics->param.k5;
    distCoeffs(7) = intrinsics->param.k6;

    k4a_image_create(K4A_IMAGE_FORMAT_CUSTOM,
                     pinhole.width,
                     pinhole.height,
                     pinhole.width * (int)sizeof(coordinate_t),
                     &lut);

    create_undistortion_lut(&calibration, K4A_CALIBRATION_TYPE_DEPTH, &pinhole, lut, interpolation_type);

    kf = kinfu::KinFu::create(params);

    return true;
}

bool captureFrame()
{

    k4a_capture_t capture = NULL;
    k4a_image_t depth_image = NULL;
    k4a_image_t undistorted_depth_image = NULL;
    const int32_t TIMEOUT_IN_MS = 1000;

    const int width = calibration.depth_camera_calibration.resolution_width;
    const int height = calibration.depth_camera_calibration.resolution_height;

    // Get a depth frame
    switch (k4a_device_get_capture(device, &capture, TIMEOUT_IN_MS))
    {
    case K4A_WAIT_RESULT_SUCCEEDED:
        break;
    case K4A_WAIT_RESULT_TIMEOUT:
        printf("Timed out waiting for a capture\n");
        return true;

    case K4A_WAIT_RESULT_FAILED:
        printf("Failed to read a capture\n");
        k4a_device_close(device);
        return false;
    }

    // Retrieve depth image
    depth_image = k4a_capture_get_depth_image(capture);
    if (depth_image == NULL)
    {
        printf("Depth16 None\n");
        k4a_capture_release(capture);
        return false;
    }

    k4a_image_create(K4A_IMAGE_FORMAT_DEPTH16,
                     pinhole.width,
                     pinhole.height,
                     pinhole.width * (int)sizeof(uint16_t),
                     &undistorted_depth_image);
    remap(depth_image, lut, undistorted_depth_image, interpolation_type);

    // Create frame from depth buffer
    uint8_t *buffer = k4a_image_get_buffer(undistorted_depth_image);
    uint16_t *depth_buffer = reinterpret_cast<uint16_t *>(buffer);
    UMat undistortedFrame;
    create_mat_from_buffer<uint16_t>(depth_buffer, width, height).copyTo(undistortedFrame);

    if (undistortedFrame.empty())
    {
        k4a_image_release(depth_image);
        k4a_image_release(undistorted_depth_image);
        k4a_capture_release(capture);
        return true;
    }

    // Update KinectFusion
    if (!kf->update(undistortedFrame))
    {
        printf("Reset KinectFusion\n");
        kf->reset();
        k4a_image_release(depth_image);
        k4a_image_release(undistorted_depth_image);
        k4a_capture_release(capture);
        return true;
    }

    // get cloud
    kf->getCloud(points, normals);

    k4a_image_release(depth_image);
    k4a_image_release(undistorted_depth_image);
    k4a_capture_release(capture);

    return true;
}

void closeDevice()
{
    k4a_image_release(lut);

    k4a_device_close(device);
}