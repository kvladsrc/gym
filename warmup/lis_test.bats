#!/usr/bin/env bats

RUN="bazel run :lis"

@test "empty array" {
    input=$'0'
    expected=$'0'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one element" {
    input=$'1 1'
    expected=$'1'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "decreasing" {
    input=$'10 10 9 8 7 6 5 4 3 2 1'
    expected=$'1'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "increasing" {
    input=$'10 1 2 3 4 5 6 7 8 9 10'
    expected=$'10'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "duplicates" {
    input=$'10 1 1 1 1 1 1 1 1 1 1'
    expected=$'1'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "duplicates two elements" {
    input=$'10 1 1 1 1 1 2 2 2 2 2'
    expected=$'2'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "duplicates two elements decreasing" {
    input=$'10 2 2 2 2 2 1 1 1 1 1'
    expected=$'1'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "mixed" {
    input=$'10 7 5 3 7 9 6 34 6 8 5'
    expected=$'4'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}
