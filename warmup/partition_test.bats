#!/usr/bin/env bats

RUN="bazel run :partition"

@test "empty array" {
    input=$'0'
    expected=$'-1 YES'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one element array" {
    input=$'1 1'
    expected=$'0 1 YES'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "two element array" {
    input=$'2 2 1'
    expected=$'0 1 2 YES'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "sorted ascending array" {
    input=$'5 1 2 3 4 5'
    expected=$'4 1 2 3 4 5 YES'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "sorted descending array" {
    input=$'5 5 4 3 2 1'
    expected=$'0 1 4 3 2 5 YES'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "all equal elements" {
    input=$'4 2 2 2 2'
    expected=$'0 2 2 2 2 YES'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "array with duplicates" {
    input=$'6 3 3 2 1 3 2'
    expected=$'1 1 2 2 3 3 3 YES'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "array with negative numbers" {
    input=$'5 -3 0 -1 2 -2'
    expected=$'1 -3 -2 -1 2 0 YES'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one element negative" {
    input=$'1 -5'
    expected=$'0 -5 YES'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
