load("@rules_cc//cc:defs.bzl", "cc_binary", "cc_library")

cc_library(
    name = "cifar10",
    srcs = ["cifar10.cc"],
    hdrs = ["cifar10.h"],
    copts = ["-std=c++17"],
    visibility = ["//visibility:public"],
)

cc_library(
    name = "mnist",
    srcs = ["mnist.cc"],
    hdrs = ["mnist.h"],
    copts = ["-std=c++17"],
    visibility = ["//visibility:public"],
)

cc_binary(
    name = "mnist_info",
    srcs = ["mnist_info.cc"],
    copts = ["-std=c++17"],
    deps = [":mnist"],
)

cc_library(
    name = "nn_lib",
    srcs = ["nn_lib/nn_lib.cc"],
    hdrs = ["nn_lib/nn_lib.h"],
    copts = ["-std=c++17"],
    visibility = ["//visibility:public"],
)

cc_library(
    name = "nn_io",
    srcs = ["nn_io.cc"],
    hdrs = ["nn_io.h"],
    copts = ["-std=c++17"],
    visibility = ["//visibility:public"],
    deps = [":nn_lib"],
)

cc_binary(
    name = "nn",
    srcs = ["nn.cc"],
    copts = ["-std=c++17"],
    deps = [
        ":mnist",
        ":nn_io",
        ":nn_lib",
    ],
)

cc_binary(
    name = "cifar10_nn",
    srcs = ["cifar10_nn.cc"],
    copts = ["-std=c++17"],
    deps = [
        ":cifar10",
        ":nn_io",
        ":nn_lib",
    ],
)

cc_binary(
    name = "nn_predict",
    srcs = ["nn_predict.cc"],
    copts = ["-std=c++17"],
    deps = [
        ":nn_io",
        ":nn_lib",
    ],
)
