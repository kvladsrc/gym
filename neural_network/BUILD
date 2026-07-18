load("@rules_cc//cc:defs.bzl", "cc_binary", "cc_library")

cc_library(
    name = "dataset",
    hdrs = ["dataset.h"],
    visibility = ["//visibility:public"],
)

cc_library(
    name = "cifar10",
    srcs = ["cifar10.cc"],
    hdrs = ["cifar10.h"],
    copts = ["-std=c++17"],
    visibility = ["//visibility:public"],
    deps = [":dataset"],
)

cc_library(
    name = "mnist",
    srcs = ["mnist.cc"],
    hdrs = ["mnist.h"],
    copts = ["-std=c++17"],
    visibility = ["//visibility:public"],
    deps = [":dataset"],
)

cc_library(
    name = "nn_io",
    srcs = ["nn_io.cc"],
    hdrs = ["nn_io.h"],
    copts = ["-std=c++17"],
    visibility = ["//visibility:public"],
    deps = ["//neural_network/nn_lib"],
)

cc_binary(
    name = "train",
    srcs = ["train.cc"],
    copts = ["-std=c++17"],
    deps = [
        ":cifar10",
        ":dataset",
        ":mnist",
        ":nn_io",
        "//neural_network/nn_lib",
    ],
)

cc_binary(
    name = "nn_predict",
    srcs = ["nn_predict.cc"],
    copts = ["-std=c++17"],
    deps = [
        ":nn_io",
        "//neural_network/nn_lib",
    ],
)
