# Kinfu Unity Plugin

This plugin allows the use of the Azure Kinect, and will extract the camera 6DOF pose and point cloud information using the OpenCV KinectFusion libraries.

## Example app

This application under `kinfu-unity-example` is a basic setup to showcase the use of the plugin. Futher documentation for the plugin can be found in the README file under `kinfu-unity-example`.

## References

[Azure Kinect SDK Docs](https://microsoft.github.io/Azure-Kinect-Sensor-SDK/release/1.4.x/index.html)

[Azure Kinect - OpenCV KinectFusion Sample](https://github.com/microsoft/Azure-Kinect-Samples/tree/master/opencv-kinfu-samples)

## Building

These are a condensed version from the OpenCV KinectFusion sample [README](https://github.com/microsoft/Azure-Kinect-Samples/blob/master/opencv-kinfu-samples/README.md)

- Download OpenCV source (4.6.0 used here) [Link](https://opencv.org/releases/) - Download the Windows .exe version and extract with something like 7Zip.
- Download the Extra modules matching the OpenCV version [Link](https://github.com/opencv/opencv_contrib/tags)
- Download VTK source (9.2.2 used) [Link](https://vtk.org/download/)
- Download and install [CMake GUI](https://cmake.org/download/) and [Visual Studio 2022 (17)](https://visualstudio.microsoft.com/downloads/)

Create build folder structure in this fashion:

```
+ opencv
    + src
    + build
+ vtk
    + src
    + build
```

Extract the OpenCV files into their own named folders in the `opencv/src` folder, and the VTK source directly into the `vtk/src` folder. The final layout should look like this:

```
+ opencv
    + src
        + opencv-4.6.0
            + 3rdparty
            + apps
            ...
        + opencv_contrib-4.6.0
            + doc
            + modules
            ...
    + build
+ vtk
    + src (Extract VTK source here)
        + Accelerators
        ...
    + build
```

### Building VTK

(These notes are in the VTK source: `Documentation\dev\build_windows_vs.md`)

Open CMake GUI and point the source folder to `{path}/vtk/src` and the build output to `{path}/vtk/build`. Configure and Generate the project.

### Building OpenCV

Once VTK has bee built we can move onto compiling OpenCV with the required changes in the config.
Open CMake GUI and point the source folder to `{path}/opencv/src/opencv-4.6.0` and the build output to `{path}/opencv/build`. Configure the project.

Once the configure step is completed, set the following settings:

- `OPENCV_ENABLE_NONFREE` checked
- `OPENCV_EXTRA_MODULES_PATH` `{path}/opencv/src/opencv_contrib-4.6.0/modules`
- `WITH_VTK` checked
- `VTK_DIR` `{path}/vtk/build`

With these set, you can now generate the project. Once project generation is completed we can finally open the Visual Studio project via the `Open Project` button, or by navigating to `{path}/opencv/build/OpenCV.sln`

Open the project and build both Debug and Release. This should build with no issues.

### Copying over required files for project.

Under the KinFu Unity plugin folder (this folder) create under `extern` the following folders:

- `lib`
- `opencv-{OPENCV_VERSION_WITHOUT_DOTS}\include` (i.e `opencv-460`)
- `opencv_contrib-{OPENCV_VERSION_WITHOUT_DOTS}\modules\rgbd\include` (i.e `opencv_contrib-460`)

Then copy over the following files:

- `extern/lib` -> `{path}/opencv/build/lib` copy both the debug and release folders into here
- `opencv-{OPENCV_VERSION_WITHOUT_DOTS}\include` -> `{path}/opencv/src/opencv-4.6.0/include`
- `opencv_contrib-{OPENCV_VERSION_WITHOUT_DOTS}\modules\rgbd\include` -> `{path}/opencv/src/opencv_contrib-4.6.0/modules/rgbd/include`

In the Solution Items open the OpenCV.props file and update the `OPENCV_VERSION` macro to the curent OpenCV version used without the periods (i.e. `4.6.0` -> `460`). This can also be accessed via the Property Manager (View -> Other Windows -> Property Manager).

### Building Kinfu for Unity

As long as the plugin folder resides with the example app, building the project will also copy the DLL to the Unity Project `Assets/Plugins` folder.

It is worth double checking the following DLLs also exist under the `Assets/Plugins` folder, and if not these can be copied from `{path}/opencv/build/bin` in the Debug and Release folders.

To ensure the app will run correctly, you will need the following DLLs under the Plugins folder:

KinFu Unity plugin: `kinfuunity.dll`

These are from the Kinect SDK

- `depthengine_2_0.dll`
- `k4a.dll`
- `k4arecord.dll`

These are from the version of OpenCV built in both Debug (`[version]d.dll`) and Release varieties

Release

- `opencv_calib3d460.dll`
- `opencv_core460.dll`
- `opencv_features2d460.dll`
- `opencv_flann460.dll`
- `opencv_highgui460.dll`
- `opencv_imgcodecs460.dll`
- `opencv_imgproc460.dll`
- `opencv_rgbd460.dll`
- `opencv_videoio460.dll`

Debug

- `opencv_calib3d460d.dll`
- `opencv_core460d.dll`
- `opencv_features2d460d.dll`
- `opencv_flann460d.dll`
- `opencv_highgui460d.dll`
- `opencv_imgcodecs460d.dll`
- `opencv_imgproc460d.dll`
- `opencv_rgbd460d.dll`
- `opencv_videoio460d.dll`
