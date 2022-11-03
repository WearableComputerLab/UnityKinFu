// The following ifdef block is the standard way of creating macros which make exporting
// from a DLL simpler. All files within this DLL are compiled with the KINFUUNITY_EXPORTS
// symbol defined on the command line. This symbol should not be defined on any project
// that uses this DLL. This way any other project whose source files include this file see
// KINFUUNITY_API functions as being imported from a DLL, whereas this DLL sees symbols
// defined with this macro as being exported.
#ifdef KINFUUNITY_EXPORTS
#define KINFUUNITY_API __declspec(dllexport)
#else
#define KINFUUNITY_API __declspec(dllimport)
#endif

extern "C"
{
	// Register callback to print messages on the Unity side
	typedef void (*PrintMessageCallback)(int level, const char *);
	KINFUUNITY_API void registerPrintMessageCallback(PrintMessageCallback callback, int level);

	// Connect to the Default device, configure, and start the cameras
	KINFUUNITY_API int connectAndStartCameras();

	/// <summary>
	/// Combine Colour image capture, frame update, point cloud capture,
	/// and pose fetchin a single call
	/// </summary>
	/// <returns>Status of the update
	/// 1: Update successful
	/// 0: Update unsuccessful, can still process
	/// -2: Fatal issue and close device
	/// </returns>
	KINFUUNITY_API int captureFrame(
		unsigned char *color_data,
		unsigned char *point_data,
		unsigned char *matrix_data);

	// Captures the color image from the device
	KINFUUNITY_API int captureColorImage(unsigned char *color_data);

	// Updates the KinectFusion object with the latest undistorted frame
	KINFUUNITY_API int updateKinectFusion();

	// Captures the point cloud data from the latest frame
	// (assuming updateKinectFusion has been called first)
	KINFUUNITY_API int capturePointCloud(unsigned char *point_data);

	// Captures the camera pose matrix from the  latest frame
	// (assuming captureFrame has been called first)
	KINFUUNITY_API void requestPose(unsigned char *matrix_data);

	///
	/// Below are the raw calls to the Kinect k4a functions
	/// and can be called individually if required.
	///
	/// They have been ordered in the order you would call them
	///

	// Returns the number of connected devices
	KINFUUNITY_API int getConnectedSensorCount();

	// Connect to the first device (device 0)
	KINFUUNITY_API bool connectToDefaultDevice();

	// Connect to a specific device
	KINFUUNITY_API bool connectToDevice(int deviceIndex);

	// setup and configure device
	KINFUUNITY_API bool setupConfigAndCalibrate();

	// start connected device cameras
	KINFUUNITY_API bool startCameras();

	// Resets the KinectFusion algorithm
	// Clears current model and resets a pose.
	KINFUUNITY_API void reset();

	// stop connected device cameras
	KINFUUNITY_API bool stopCameras();

	// close device and release device handle
	KINFUUNITY_API void closeDevice();
}
