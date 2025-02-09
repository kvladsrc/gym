#!/usr/bin/env bats

RUN="bazel run :ll_reverse"

@test "empty" {
    result="$(echo '0' | $RUN)"
    [ "$result" = "-1" ]
}

@test "one" {
    result="$(echo '1 1' | $RUN)"
    [ "$result" = "1
1" ]
}

@test "simple" {
    result="$(echo '10 1 2 3 4 5 6 7 8 9 10' | $RUN)"
    [ "$result" = "10 9 8 7 6 5 4 3 2 1
1
10 9 8 7 6 5 4 3 2" ]
}
