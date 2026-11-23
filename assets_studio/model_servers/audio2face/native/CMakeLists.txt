# The server's runner, against the SDK built by setup_native.py.
cmake_minimum_required(VERSION 3.24)
project(a2f_runner LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

find_package(CUDAToolkit 12.8 REQUIRED)

set(AUDIO2X_SDK "" CACHE PATH "The built SDK: its audio2x-sdk directory, with include/ and lib/")
if(NOT EXISTS "${AUDIO2X_SDK}/lib/libaudio2x.so")
    message(FATAL_ERROR "libaudio2x.so not found in ${AUDIO2X_SDK}/lib")
endif()

# The SDK library needs TensorRT; the linker looks for it here (the server
# names the same directory to the runner at run time).
set(TENSORRT_LIBRARY_DIR "" CACHE PATH "The directory with libnvinfer.so.10 and the other TensorRT libraries")

add_executable(a2f-runner a2f_runner.cpp)
target_link_options(a2f-runner PRIVATE "LINKER:-rpath-link,${TENSORRT_LIBRARY_DIR}")
# The SDK is built with the pre-C++11 string ABI, as its prebuilt dependencies
# are; error messages cross the library boundary as std::string.
target_compile_definitions(a2f-runner PRIVATE _GLIBCXX_USE_CXX11_ABI=0)
target_include_directories(a2f-runner PRIVATE "${AUDIO2X_SDK}/include")
target_link_libraries(a2f-runner PRIVATE "${AUDIO2X_SDK}/lib/libaudio2x.so" CUDA::cudart)
