#!/usr/bin/env bats

RUN="bazel run :mo_algorithm"

@test "empty" {
    input=$'0 0'
    expected=$'EMPTY'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one element" {
    input=$'1 1 5 0 0'
    expected=$'1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "two elements same" {
    input=$'2 1 3 3 0 1'
    expected=$'4'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "two elements different" {
    input=$'2 1 3 4 0 1'
    expected=$'2'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "multiple queries" {
    input=$'5 3 1 2 1 3 2 0 2 1 3 0 4'
    expected=$'5\n3\n9'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "all equal elements" {
    input=$'5 2 7 7 7 7 7 0 4 2 2'
    expected=$'25\n1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "negative numbers" {
    input=$'6 1 -3 -1 -2 -4 0 5 0 5'
    expected=$'6'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one element negative" {
    input=$'1 1 -1 0 0'
    expected=$'1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
