#!/usr/bin/env bats

RUN="bazel run :sets_in_tree"

@test "first case" {
    input=$'4 1 2 3 4 1 2 2 3 2 4'
    expected=$'10 9 3 4 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "second case" {
    input=$'15 1 2 3 1 2 3 3 1 1 3 2 2 1 2 3 1 2 1 3 1 4 1 14 1 15 2 5 2 6 2 7 3 8 3 9 3 10 4 11 4 12 4 13'
    expected=$'6 5 4 3 2 3 3 1 1 3 2 2 1 2 3 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
