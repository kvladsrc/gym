#!/usr/bin/env bats

RUN="bazel run :binary_search_with_duplicates"

@test "case found" {
    result="$(echo '10 1 2 3 4 5 6 7 8 9 10 6' | $RUN)"
    [ "$result" = "5" ]
}

@test "case all smaller" {
    result="$(echo '15 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 2' | $RUN)"
    [ "$result" = "0" ]
}

@test "case all bigger" {
    result="$(echo '15 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 20' | $RUN)"
    [ "$result" = "-1" ]
}

@test "case duplicates" {
    result="$(echo '15 1 2 3 4 5 5 5 5 5 6 7 7 8 8 9 5' | $RUN)"
    [ "$result" = "4" ]
}

@test "case bound" {
    result="$(echo '15 1 2 3 4 5 8 8 8 8 8 8 8 8 8 8 7' | $RUN)"
    [ "$result" = "5" ]
}
