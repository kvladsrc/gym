#!/usr/bin/env bats

RUN="bazel run :dutch_flag"

@test "empty array" {
    input=$'0'
    expected=$''
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one element (1)" {
    input=$'1 1'
    expected=$'1 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one element (2)" {
    input=$'1 2'
    expected=$'2 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one element (3)" {
    input=$'1 3'
    expected=$'3 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "already sorted" {
    input=$'6 1 1 2 2 3 3'
    expected=$'1 1 2 2 3 3 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "reverse sorted" {
    input=$'6 3 3 2 2 1 1'
    expected=$'1 1 2 2 3 3 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "mixed order" {
    input=$'10 3 1 2 1 3 2 2 1 3 1'
    expected=$'1 1 1 1 2 2 2 3 3 3 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "all 1s" {
    input=$'5 1 1 1 1 1'
    expected=$'1 1 1 1 1 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "all 2s" {
    input=$'5 2 2 2 2 2'
    expected=$'2 2 2 2 2 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "all 3s" {
    input=$'5 3 3 3 3 3'
    expected=$'3 3 3 3 3 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "only 1s and 2s" {
    input=$'7 2 1 1 2 1 2 1'
    expected=$'1 1 1 1 2 2 2 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "only 1s and 3s" {
    input=$'7 3 1 1 3 1 3 1'
    expected=$'1 1 1 1 3 3 3 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "only 2s and 3s" {
    input=$'7 2 3 3 2 3 2 3'
    expected=$'2 2 2 3 3 3 3 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "pivot in the middle" {
    input=$'9 1 3 2 1 3 2 1 3 2'
    expected=$'1 1 1 2 2 2 3 3 3 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
