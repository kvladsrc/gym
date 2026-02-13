load("@rules_cc//cc:defs.bzl", "cc_library")

cc_library(
    name = "cuda",
    srcs = glob(["lib/*.so*"]),
    hdrs = glob(["include/**/*.h"]),
    includes = ["include"],
    visibility = ["//visibility:public"],
)
