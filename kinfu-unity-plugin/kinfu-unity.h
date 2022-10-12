
extern "C"
{

	typedef void (*PrintMessageCallback)(int level, const char *);
	__declspec(dllexport) void registerPrintMessageCallback(PrintMessageCallback callback, int level);

	__declspec(dllexport) void requestPose(unsigned char *matrix_data);

	__declspec(dllexport) int getConnectedSensorCount();
	__declspec(dllexport) bool connectToDevice(int deviceIndex);
	__declspec(dllexport) bool connectToDefaultDevice();
	__declspec(dllexport) bool setupConfigAndCalibrate();
	__declspec(dllexport) bool startCameras();
	__declspec(dllexport) int connectAndStartCameras();
	__declspec(dllexport) int captureFrame(unsigned char *color_data);
	__declspec(dllexport) bool stopCameras();
	__declspec(dllexport) void closeDevice();
	__declspec(dllexport) void reset();

	__declspec(dllexport) void getColorImageBytes(unsigned char *data, int width, int height);
	__declspec(dllexport) int capturePointCloud(unsigned char *point_data);
}