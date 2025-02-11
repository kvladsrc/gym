#!/usr/bin/env bats

RUN="bazel run :median_of_stream"

@test "empty array" {
    input=$'0'
    expected=$'0'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "increasing even" {
    input=$'10 1 2 3 4 5 6 7 8 9 10'
    expected=$'5'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "increasing odd" {
    input=$'11 1 2 3 4 5 6 7 8 9 10 11'
    expected=$'6'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "decreasing even" {
    input=$'10 10 9 8 7 6 5 4 3 2 1'
    expected=$'5'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "decreasing odd" {
    input=$'11 11 10 9 8 7 6 5 4 3 2 1'
    expected=$'6'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "duplicates" {
    input=$'10 1 2 2 2 2 2 2 2 2 3'
    expected=$'2'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "lower bound" {
    input=$'10 1 1 1 1 1 2 2 2 2 2'
    expected=$'1'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "upper bound" {
    input=$'10 1 1 1 1 2 2 2 2 2 2'
    expected=$'2'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "mixed" {
    input=$'10 1 2 2 2 1 1 2 2 1 1'
    expected=$'1'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}
