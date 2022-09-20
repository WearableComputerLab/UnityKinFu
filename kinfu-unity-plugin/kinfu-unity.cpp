// kinfu-unity.cpp : Defines the exported functions for the DLL.
//

#include "pch.h"
#include "framework.h"
#include "kinfu-helpers.h"

#include "kinfu-unity.h"

#include <sstream>

// The currently connected device
k4a_device_t device = NULL;

// Configure the depth mode and fps
k4a_device_configuration_t config = K4A_DEVICE_CONFIG_INIT_DISABLE_ALL;

k4a_calibration_t calibration;

k4a_image_t lut = NULL;

Ptr<kinfu::KinFu> kf;

pinhole_t pinhole;
interpolation_t interpolation_type = INTERPOLATION_BILINEAR_DEPTH;

PrintMessageCallback printMessage = NULL;
void PrintMessage(int level, const char *msg)
{
    if (printMessage != nullptr)
    {
        printMessage(level, msg);
    }

    printf(msg);
}

void KinFuMessageHandler(void *context, k4a_log_level_t level, const char *file, const int line, const char *message)
{
    PrintMessage(level, message);
}

void RegisterPrintMessageCallback(PrintMessageCallback callback, int level)
{
    printMessage = callback;
    k4a_set_debug_message_handler(&KinFuMessageHandler, NULL, (k4a_log_level_t)level);
}

CloudDataCallback sendCloudData = NULL;
void RegisterCloudDataCallback(CloudDataCallback callback)
{
    sendCloudData = callback;
}

PoseDataCallback sendPoseData = NULL;
void RegisterPoseDataCallback(PoseDataCallback callback)
{
    sendPoseData = callback;
}

void requestPose()
{
    if (sendPoseData == nullptr) return;

    auto pose = kf->getPose();
    std::stringstream matrix_output;

    for (int row = 0; row < pose.matrix.rows; row++)
    {
        matrix_output << "[ ";
        for (int col = 0; col < pose.matrix.cols; col++)
        {
            auto cell = pose.matrix(row, col);
            matrix_output << std::fixed << cell;

            if (col < pose.matrix.cols - 1)
            {
                matrix_output << ", ";
            }
        }

        matrix_output << " ]\n";
    }

    PrintMessage(K4A_LOG_LEVEL_INFO, matrix_output.str().c_str());
    sendPoseData(pose.matrix.val);
}

int getConnectedSensorCount()
{
    return k4a_device_get_installed_count();
}

int connectAndStartCameras()
{
    if (!connectToDefaultDevice())
        return -1;
    if (!setupConfigAndCalibrate())
        return -2;
    if (!startCameras())
        return -3;

    return 0;
}

bool connectToDevice(int deviceIndex)
{

    if (K4A_RESULT_SUCCEEDED != k4a_device_open(deviceIndex, &device))
    {
        PrintMessage(K4A_LOG_LEVEL_CRITICAL, "Failed to open device\n");
        closeDevice();
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
        PrintMessage(K4A_LOG_LEVEL_CRITICAL, "Failed to get calibration\n");
        closeDevice();
        return false;
    }

    return true;
}

bool startCameras()
{
    stopCameras();

    if (K4A_RESULT_SUCCEEDED != k4a_device_start_cameras(device, &config))
    {
        PrintMessage(K4A_LOG_LEVEL_CRITICAL, "Failed to start device\n");
        closeDevice();
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
        PrintMessage(K4A_LOG_LEVEL_INFO, "Timed out waiting for a capture\n");
        return true;

    case K4A_WAIT_RESULT_FAILED:
        PrintMessage(K4A_LOG_LEVEL_CRITICAL, "Failed to read a capture\n");
        closeDevice();
        return false;
    }

    // Retrieve depth image
    depth_image = k4a_capture_get_depth_image(capture);
    if (depth_image == NULL)
    {
        PrintMessage(K4A_LOG_LEVEL_CRITICAL, "Depth16 None\n");
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
    create_mat_from_buffer(depth_buffer, width, height, 1).copyTo(undistortedFrame);

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
        PrintMessage(K4A_LOG_LEVEL_INFO, "Reset KinectFusion\n");
        kf->reset();
        k4a_image_release(depth_image);
        k4a_image_release(undistorted_depth_image);
        k4a_capture_release(capture);
        return true;
    }

    // get cloud
    Mat points, normals;
    kf->getCloud(points, normals);

    k4a_image_release(depth_image);
    k4a_image_release(undistorted_depth_image);
    k4a_capture_release(capture);

    if (sendCloudData != nullptr)
    {
        auto out_points = new float[points.rows * 3];
        auto out_normals = new float[points.rows * 3];
        for (int i = 0; i < points.rows; ++i)
        {
            out_points[i + 0] = points.at<float>(i, 0);
            out_points[i + 1] = points.at<float>(i, 1);
            out_points[i + 2] = points.at<float>(i, 2);

            out_normals[i + 0] = normals.at<float>(i, 0);
            out_normals[i + 1] = normals.at<float>(i, 1);
            out_normals[i + 2] = normals.at<float>(i, 2);
        }

        sendCloudData(points.rows, out_points, out_normals);

        delete[] out_normals;
        delete[] out_points;
    }

    return true;
}

bool stopCameras()
{
    k4a_device_stop_cameras(device);
    return true;
}

void closeDevice()
{
    if (lut != NULL)
    {
        k4a_image_release(lut);
        lut = NULL;
    }

    k4a_device_close(device);
}