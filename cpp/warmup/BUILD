load("@rules_cc//cc:defs.bzl", "cc_binary", "cc_library", "cc_test")

# Automatically create a cc_library for every .cc file that is not a test or benchmark.
[
    cc_library(
        name = src.replace(".cc", "_lib"),
        srcs = [src],
        hdrs = [src.replace(".cc", ".hpp")],
        copts = ["-std=c++17"],
        visibility = ["//visibility:public"],
    )
    for src in glob(
        ["*.cc"],
        exclude = [
            "*_gtest.cc",
            "*_bench.cc",
        ],
    )
]

# Automatically create a cc_test for every _gtest.cc file.
[
    cc_test(
        name = src.replace(".cc", ""),
        srcs = [src],
        copts = ["-std=c++17"],
        deps = [
            "@googletest//:gtest_main",
            ":" + src.replace("_gtest.cc", "") + "_lib",
        ],
    )
    for src in glob(["*_gtest.cc"])
]

# Automatically create a cc_binary for every _bench.cc file.
[
    cc_binary(
        name = src.replace(".cc", ""),
        srcs = [src],
        copts = ["-std=c++17"],
        deps = [
            "@google_benchmark//:benchmark_main",
            ":" + src.replace("_bench.cc", "") + "_lib",
        ],
    )
    for src in glob(["*_bench.cc"])
]
