#!/usr/bin/env bats

RUN="bazel run :knapsack_with_duplicates"

@test "empty array" {
    input=$'0 10'
    expected=$'0'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one item fits repeatedly" {
    input=$'1 10 2 3'
    expected=$'15'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one item does not fit" {
    input=$'1 1 2 5'
    expected=$'0'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "multiple items with duplicates" {
    input=$'2 10 3 4 4 5'
    expected=$'13'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "optimal combination with repeats" {
    input=$'2 11 3 5 4 7'
    expected=$'15'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "all items too heavy" {
    input=$'2 5 6 7 10 13'
    expected=$'0'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "multiple items" {
    input=$'3 9 2 3 4 1 2 5'
    expected=$'10'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
