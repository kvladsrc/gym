load("@rules_cc//cc:defs.bzl", "cc_library")

# cuDNN engine libraries (engines_precompiled, engines_runtime_compiled) use
# dlsym for cuInitializeInjectionCaskEnhanced which is absent in driver 580.
# Excluding them lets cuDNN fall back to non-cask-enhanced kernel paths.

cc_library(
    name = "cudnn",
    srcs = glob(
        ["lib/*.so*"],
        exclude = [
            "lib/libcudnn_engines_precompiled*",
            "lib/libcudnn_engines_runtime_compiled*",
        ],
    ),
    hdrs = glob(["include/**/*.h"]),
    includes = ["include"],
    visibility = ["//visibility:public"],
)
