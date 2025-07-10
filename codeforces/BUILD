load("@rules_cc//cc:defs.bzl", "cc_binary")

#Automatically create a cc_binary for every.cc file in this directory.
[
    cc_binary(
        name = src.replace(".cc", ""),
        srcs = [src],
        copts = [
            "-Werror",
            "-std=c++17",
        ],
    )
    for src in glob(["*.cc"])
]
