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

@test "all equal elements" {
    input=$'5 2 2 2 2 2'
    expected=$'1'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "alternating high low" {
    input=$'6 1 3 2 4 3 5'
    expected=$'4'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "zigzag" {
    input=$'9 1 3 2 4 3 5 4 6 5'
    expected=$'5'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "multiple equal LIS lengths" {
    input=$'6 3 10 2 1 20 4'
    expected=$'3'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "long increasing with noise" {
    input=$'15 1 2 3 2 4 5 4 6 7 5 8 9 6 10 11'
    expected=$'11'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "maximal LIS at end" {
    input=$'7 10 9 8 1 2 3 4'
    expected=$'4'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "maximal LIS at beginning" {
    input=$'7 1 2 3 4 10 9 8'
    expected=$'5'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "with negative numbers" {
    input=$'7 -5 -1 -3 0 2 -2 3'
    expected=$'5'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}
