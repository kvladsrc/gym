#!/usr/bin/env bats

RUN="bazel run :bubble_sort"

@test "empty" {
    result="$(echo '0' | $RUN)"
    [ "$result" = "" ]
}

@test "one element" {
    result="$(echo '1 1' | $RUN)"
    [ "$result" = "1 " ]
}

@test "already sorted" {
    result="$(echo '10 1 2 3 4 5 6 7 8 9 10' | $RUN)"
    [ "$result" = "1 2 3 4 5 6 7 8 9 10 " ]
}

@test "already sorted (reversed)" {
    result="$(echo '10 10 9 8 7 6 5 4 3 2 1' | $RUN)"
    [ "$result" = "1 2 3 4 5 6 7 8 9 10 " ]
}

@test "dubplicates" {
    result="$(echo '10 1 3 3 3 3 3 3 3 4 2' | $RUN)"
    [ "$result" = "1 2 3 3 3 3 3 3 3 4 " ]
}

@test "all equal elements" {
    result="$(echo '5 1 1 1 1 1' | $RUN)"
    [ "$result" = "1 1 1 1 1 " ]
}

@test "random order" {
    result="$(echo '6 4 1 3 2 6 5' | $RUN)"
    [ "$result" = "1 2 3 4 5 6 " ]
}

@test "negative numbers" {
    result="$(echo '6 -3 -1 -2 -4 0 5' | $RUN)"
    [ "$result" = "-4 -3 -2 -1 0 5 " ]
}

@test "one element negative" {
    result="$(echo '1 -1' | $RUN)"
    [ "$result" = "-1 " ]
}
