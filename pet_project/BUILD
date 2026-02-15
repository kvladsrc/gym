load("@rules_cc//cc:defs.bzl", "cc_binary", "cc_library")
load("//pet_project:onnx_config.bzl", "ONNX_COPTS", "ONNX_LINKOPTS")

cc_library(
    name = "types",
    hdrs = ["types.hpp"],
    copts = ["-std=c++17"],
)

cc_library(
    name = "config_lib",
    srcs = ["config.cc"],
    hdrs = ["config.hpp"],
    copts = ["-std=c++17"],
    deps = [
        ":types",
        "@nlohmann_json//:json",
    ],
)

cc_library(
    name = "stb",
    hdrs = ["third_party/stb_image_write.h"],
    strip_include_prefix = "third_party",
    include_prefix = "stb",
)

cc_library(
    name = "renderer_lib",
    srcs = ["renderer.cc"],
    hdrs = ["renderer.hpp"],
    copts = ["-std=c++17"],
    deps = [
        ":stb",
        ":types",
    ],
)

cc_library(
    name = "fitness_lib",
    srcs = ["fitness.cc"],
    hdrs = ["fitness.hpp"],
    copts = ["-std=c++17", "-fopenmp"] + ONNX_COPTS,
    linkopts = ["-fopenmp"],
    deps = [
        ":config_lib",
        ":renderer_lib",
        ":types",
    ],
)

cc_library(
    name = "evolution_lib",
    srcs = ["evolution.cc"],
    hdrs = ["evolution.hpp"],
    copts = ["-std=c++17"] + ONNX_COPTS,
    deps = [
        ":config_lib",
        ":fitness_lib",
        ":renderer_lib",
        ":types",
    ],
)



cc_binary(
    name = "pet_project",
    srcs = ["main.cc"],
    copts = ["-std=c++17"] + ONNX_COPTS,
    linkopts = ONNX_LINKOPTS,
    deps = [
        ":config_lib",
        ":evolution_lib",
        ":fitness_lib",
        ":renderer_lib",
    ],
    data = [
        "mobilenetv2-12.onnx",
        "resnet50-v2-7.onnx"
    ],
)
