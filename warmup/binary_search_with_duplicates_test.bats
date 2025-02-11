#!/usr/bin/env bats

RUN="bazel run :binary_search_with_duplicates"

@test "empty array" {
    input=$'0 3'
    expected=$'-1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "case found" {
    input=$'10 1 2 3 4 5 6 7 8 9 10 6'
    expected=$'5'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "case all bigger" {
    input=$'15 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 2'
    expected=$'0'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "case all smaller" {
    input=$'15 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 20'
    expected=$'-1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "case duplicates" {
    input=$'15 1 2 3 4 5 5 5 5 5 6 7 7 8 8 9 5'
    expected=$'4'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "case bound" {
    input=$'15 1 2 3 4 5 8 8 8 8 8 8 8 8 8 8 7'
    expected=$'5'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "target at beginning" {
    input=$'5 1 2 3 4 5 1'
    expected=$'0'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "target at end" {
    input=$'5 1 2 3 4 5 5'
    expected=$'4'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "target in middle not found" {
    input=$'6 1 3 5 7 9 11 6'
    expected=$'3'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "all equal elements" {
    input=$'5 5 5 5 5 5 5'
    expected=$'0'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
