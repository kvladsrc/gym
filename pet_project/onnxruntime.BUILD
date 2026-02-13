load("@rules_cc//cc:defs.bzl", "cc_library")

# ONNX Runtime loads CUDA/TensorRT providers via dlopen from the same directory
# as libonnxruntime.so.  The --no-as-needed flag ensures the linker records
# DT_NEEDED entries for CUDA libs even though they are not directly referenced
# by application code (they are dlopen'd by libonnxruntime_providers_cuda.so).
#
# Excluding TensorRT provider to avoid incompatibility issues with driver version.

cc_library(
    name = "onnxruntime",
    srcs = glob(
        ["lib/*.so*"],
        exclude = ["lib/libonnxruntime_providers_tensorrt.so"],
    ),
    hdrs = glob(["include/**/*.h"]),
    includes = ["include"],
    linkopts = ["-Wl,--no-as-needed"],
    deps = [
        "@cuda_cublas//:cuda",
        "@cuda_cudart//:cuda",
        "@cuda_cudnn//:cudnn",
        "@cuda_cufft//:cuda",
        "@cuda_curand//:cuda",
    ],
    visibility = ["//visibility:public"],
)
