#!/usr/bin/env bats

RUN="bazel run :fenwick_tree"

# TODO(zjgkkn): rewrite according the format with input/expect. Add
# more tests.
@test "empty" {
    result="$(echo '0' | $RUN)"
    [ "$result" = "" ]
}

@test "one element" {
    result="$(echo '1 1 1 1 1' | $RUN)"
    [ "$result" = "1
1" ]
}

@test "simple" {
    result="$(echo '10 7 6 45 32 6 89 67 4 6 8 5 1 1 1 2 2 4 8 9 4 7' | $RUN)"
    [ "$result" = "7
7
7
6
45
6
6
4
89
6" ]
}
