load("@rules_cc//cc:defs.bzl", "cc_library", "cc_test")

cc_library(
    name = "nn_lib",
    srcs = ["nn_lib.cc"],
    hdrs = ["nn_lib.h"],
    copts = ["-std=c++17"],
    visibility = ["//visibility:public"],
)

cc_test(
    name = "nn_lib_gtest",
    srcs = ["nn_lib_gtest.cc"],
    copts = ["-std=c++17"],
    deps = [
        ":nn_lib",
        "@googletest//:gtest_main",
    ],
)
