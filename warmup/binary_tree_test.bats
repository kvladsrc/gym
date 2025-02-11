#!/usr/bin/env bats

RUN="bazel run :binary_tree"

# TODO(zjgkkn): rewrite according the format with input/expect. Add
# more tests.
@test "zero" {
    result="$(echo '0' | $RUN)"
    [ "$result" = "0
0" ]
}

@test "one element" {
    result="$(echo '1 1' | $RUN)"
    [ "$result" = "0
1
1 
1 
1 " ]
}

@test "duplicates" {
    result="$(echo '10 1 1 1 1 1 2 2 2 2 2' | $RUN)"
    [ "$result" = "0
2
1 2 
1 2 
2 1 " ]
}
