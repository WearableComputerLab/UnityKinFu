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

	typedef void (*PrintMessageCallback)(int level, const char *);
	KINFUUNITY_API void RegisterPrintMessageCallback(PrintMessageCallback callback, int level);

	typedef void (*PoseDataCallback)(float *matrix);
	KINFUUNITY_API void RegisterPoseDataCallback(PoseDataCallback callback);
	KINFUUNITY_API void requestPose();

	KINFUUNITY_API int getConnectedSensorCount();
	KINFUUNITY_API bool connectToDevice(int deviceIndex);
	KINFUUNITY_API bool connectToDefaultDevice();
	KINFUUNITY_API bool setupConfigAndCalibrate();
	KINFUUNITY_API bool startCameras();
	KINFUUNITY_API int connectAndStartCameras();
	KINFUUNITY_API int captureFrame(unsigned char* color_data, unsigned char* point_data);
	KINFUUNITY_API bool stopCameras();
	KINFUUNITY_API void closeDevice();
	KINFUUNITY_API void reset();

	KINFUUNITY_API void getColorImageBytes(unsigned char *data, int width, int height);
}