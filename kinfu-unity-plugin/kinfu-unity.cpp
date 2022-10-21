// kinfu-unity.cpp : Defines the exported functions for the DLL.
//

#include "pch.h"
#include "framework.h"
#include "kinfu-helpers.h"

#include "kinfu-unity.h"

#include <sstream>

const int32_t TIMEOUT_IN_MS = 1000;

// The currently connected device
k4a_device_t device = NULL;

// Configure the depth mode and fps
k4a_device_configuration_t config = K4A_DEVICE_CONFIG_INIT_DISABLE_ALL;
k4a_calibration_t calibration;
k4a_image_t lut = NULL;

pinhole_t pinhole;
interpolation_t interpolation_type = INTERPOLATION_BILINEAR_DEPTH;

Ptr<kinfu::KinFu> kf;

const int maxPoints = 1000000;
auto out_points = new float[maxPoints * 3];
auto out_normals = new float[maxPoints * 3];

///
///

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

void registerPrintMessageCallback(PrintMessageCallback callback, int level)
{
    printMessage = callback;
    k4a_set_debug_message_handler(&KinFuMessageHandler, NULL, (k4a_log_level_t)level);
}

///
///

/// <summary>
/// Capture color image from Kinect
/// </summary>
/// <returns>Status of the update
/// 0: Connected and started OK
/// -1: Failed to connect to Default device
/// -2: Failed to setup and calibrate
/// -3: Failed to start the device cameras
/// </returns>

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

/// <summary>
/// Capture camera 6DOF matrix from last capture frame
/// </summary>
void requestPose(unsigned char *matrix_data)
{
    auto pose = kf->getPose();
    memcpy(matrix_data, pose.matrix.val, sizeof(float) * 16);
}

/// <summary>
/// Capture color image from Kinect
/// </summary>
/// <returns>Status of the update
/// true: Capture successful
/// false: Capture unsuccessful, can still process
/// </returns>
bool captureColorImage(k4a_capture_t capture, unsigned char *data)
{
    // Retrieve color image
    k4a_image_t color_image = k4a_capture_get_color_image(capture);
    if (color_image == NULL)
    {
        PrintMessage(K4A_LOG_LEVEL_WARNING, "No color image fetched\n");
        return false;
    }

    // Create frame from color buffer
    uint8_t *buffer = k4a_image_get_buffer(color_image);

    //
    // image comes in upside down for our case
    // So we read it backwards and add into a new buffer
    size_t size = k4a_image_get_size(color_image);

    // This should be smarter (i.e stride / width to get bytes per pixel)
    // But I want something now and I know it's 4 bytes
    uint8_t *flipped = new uint8_t[size];
    std::memset(flipped, 0x0, size);

    for (int i = 0; i < size - 4; i += 4)
    {
        // Blue Channel
        flipped[i + 2] = buffer[i + 0];
        // Green
        flipped[i + 1] = buffer[i + 1];
        // Red (because the image is in BGRA not RGBA)
        flipped[i + 0] = buffer[i + 2];
        // Alpha
        flipped[i + 3] = buffer[i + 3];
    }

    std::memcpy(data, flipped, size);

    delete[] flipped;
    k4a_image_release(color_image);

    return true;
}

/// <summary>
/// Capture the point cloud from the last Kinect Fusion frame
/// Will only store up to a max of 1,000,000 3D points
/// </summary>
/// <param name="point_data">Pointer to memory to store the data</param>
/// <returns>Size of the points rendered</returns>
int capturePointCloud(unsigned char *point_data)
{
    // get cloud
    Mat points, normals;
    kf->getCloud(points, normals);

    int size = points.rows;
    memset(point_data, 0x0, maxPoints);
    memset(out_normals, 0x0, maxPoints);

    if (size > maxPoints)
    {
        std::stringstream error;
        error << "Cloud Size exceeds max points!! " << size << " vs " << maxPoints << std::endl;
        PrintMessage(K4A_LOG_LEVEL_CRITICAL, error.str().c_str());
        return -size;
    }

    for (int i = 0; i < size; i++)
    {
        out_points[i * 3 + 0] = points.at<float>(i, 0);
        out_points[i * 3 + 1] = points.at<float>(i, 1);
        out_points[i * 3 + 2] = points.at<float>(i, 2);

        //out_normals[i * 3 + 0] = normals.at<float>(i, 0);
        //out_normals[i * 3 + 1] = normals.at<float>(i, 1);
        //out_normals[i * 3 + 2] = normals.at<float>(i, 2);
    }

    memcpy(point_data, out_points, sizeof(float) * size);

    return size;
}

/// <summary>
/// Update the KinectFusion frame
/// </summary>
/// <returns>Status of the update
/// 1: Update successful
/// 0: Update unsuccessful, can still process
/// -2: Fatal issue and close device
/// </returns>
bool updateKinectFusion(k4a_capture_t capture)
{
    k4a_image_t depth_image = NULL;
    k4a_image_t undistorted_depth_image = NULL;

    const int width = calibration.depth_camera_calibration.resolution_width;
    const int height = calibration.depth_camera_calibration.resolution_height;

    // Retrieve depth image
    depth_image = k4a_capture_get_depth_image(capture);
    if (depth_image == NULL)
    {
        PrintMessage(K4A_LOG_LEVEL_CRITICAL, "k4a_capture_get_depth_image returned NULL\n");
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
        PrintMessage(K4A_LOG_LEVEL_CRITICAL, "Undistorted frame is empty\n");
        k4a_image_release(depth_image);
        k4a_image_release(undistorted_depth_image);
        return false;
    }

    // Update KinectFusion
    if (!kf->update(undistortedFrame))
    {
        PrintMessage(K4A_LOG_LEVEL_INFO, "Did not update from frame\n");
        //        kf->reset();
        k4a_image_release(depth_image);
        k4a_image_release(undistorted_depth_image);
        return false;
    }

    k4a_image_release(depth_image);
    k4a_image_release(undistorted_depth_image);

    return true;
}

/// <summary>
/// Combine Colour image capture, frame update, point cloud capture,
/// and pose fetchin a single call
/// </summary>
/// <returns>Status of the update
/// 1: Update successful
/// 0: Update unsuccessful, can still process
/// -2: Fatal issue and close device
/// </returns>
int captureFrame(
    unsigned char *color_data,
    unsigned char *point_data,
    unsigned char *matrix_data)
{
    k4a_capture_t capture = NULL;

    switch (k4a_device_get_capture(device, &capture, TIMEOUT_IN_MS))
    {
    case K4A_WAIT_RESULT_SUCCEEDED:
        break;
    case K4A_WAIT_RESULT_TIMEOUT:
        PrintMessage(K4A_LOG_LEVEL_INFO, "Timed out waiting for a capture\n");
        return 0;

    case K4A_WAIT_RESULT_FAILED:
        PrintMessage(K4A_LOG_LEVEL_CRITICAL, "Failed to read a capture\n");
        closeDevice();
        return -2;
    }

    bool colorOk = captureColorImage(capture, color_data);
    bool updateOk = updateKinectFusion(capture);
    int numPoints = 0;

    if (updateOk)
    {
        requestPose(matrix_data);
        numPoints = capturePointCloud(point_data);
    }

    k4a_capture_release(capture);

    return numPoints;
}

/// <summary>
/// Capture color image from Kinect
/// </summary>
/// <returns>Status of the update
/// 1: Capture successful
/// 0: Capture unsuccessful, can still process
/// -2: Fatal issue and close device
/// </returns>
int captureColorImage(unsigned char *color_data)
{
    k4a_capture_t capture = NULL;

    switch (k4a_device_get_capture(device, &capture, TIMEOUT_IN_MS))
    {
    case K4A_WAIT_RESULT_SUCCEEDED:
        break;
    case K4A_WAIT_RESULT_TIMEOUT:
        PrintMessage(K4A_LOG_LEVEL_INFO, "Timed out waiting for a capture\n");
        return 0;

    case K4A_WAIT_RESULT_FAILED:
        PrintMessage(K4A_LOG_LEVEL_CRITICAL, "Failed to read a capture\n");
        closeDevice();
        return -2;
    }

    bool colorOk = captureColorImage(capture, color_data);

    k4a_capture_release(capture);

    return colorOk ? 1 : 0;
}

/// <summary>
/// Update the KinectFusion frame
/// </summary>
/// <returns>Status of the update
/// 1: Update successful
/// 0: Update unsuccessful, can still process
/// -2: Fatal issue and close device
/// </returns>
int updateKinectFusion()
{

    k4a_capture_t capture = NULL;

    switch (k4a_device_get_capture(device, &capture, TIMEOUT_IN_MS))
    {
    case K4A_WAIT_RESULT_SUCCEEDED:
        break;
    case K4A_WAIT_RESULT_TIMEOUT:
        PrintMessage(K4A_LOG_LEVEL_INFO, "Timed out waiting for a capture\n");
        return 0;

    case K4A_WAIT_RESULT_FAILED:
        PrintMessage(K4A_LOG_LEVEL_CRITICAL, "Failed to read a capture\n");
        closeDevice();
        return -2;
    }

    bool updateOk = updateKinectFusion(capture);

    k4a_capture_release(capture);
    return updateOk ? 1 : 0;
}

///
/// Below are the raw calls to the Kinect k4a functions
/// and can be called individually if required.
///
/// They have been ordered in the order you would call them
///

int getConnectedSensorCount()
{
    return k4a_device_get_installed_count();
}

inline bool connectToDefaultDevice()
{
    return connectToDevice(K4A_DEVICE_DEFAULT);
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

bool setupConfigAndCalibrate()
{
    config = K4A_DEVICE_CONFIG_INIT_DISABLE_ALL;
    config.color_format = K4A_IMAGE_FORMAT_COLOR_BGRA32;
    config.color_resolution = K4A_COLOR_RESOLUTION_1080P;
    config.depth_mode = K4A_DEPTH_MODE_NFOV_UNBINNED;
    config.camera_fps = K4A_FRAMES_PER_SECOND_5;

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

void reset()
{
    if (kf != NULL)
        kf->reset();
}

bool stopCameras()
{
    k4a_device_stop_cameras(device);
    return true;
}

void closeDevice()
{
    if (device == nullptr)
        return;

    if (lut != NULL)
    {
        k4a_image_release(lut);
        lut = NULL;
    }

    k4a_device_close(device);
    device = nullptr;
}