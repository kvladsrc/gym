#!/usr/bin/env bats

RUN="bazel run :min_stack"

@test "empty stack" {
    input=$'0'
    expected=$''
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one element" {
    input=$'1 5'
    expected=$'5 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "two elements descending" {
    input=$'2 2 1'
    expected=$'1 2 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "two elements ascending" {
    input=$'2 1 2'
    expected=$'1 1 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "three elements mixed" {
    input=$'3 3 1 2'
    expected=$'1 1 3 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "duplicates" {
    input=$'5 3 1 2 1 3'
    expected=$'1 1 1 1 3 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "all equal elements" {
    input=$'4 2 2 2 2'
    expected=$'2 2 2 2 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "descending order" {
    input=$'5 5 4 3 2 1'
    expected=$'1 2 3 4 5 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "ascending order" {
    input=$'5 1 2 3 4 5'
    expected=$'1 1 1 1 1 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "negative numbers" {
    input=$'4 0 -2 3 -1 '
    expected=$'-2 -2 -2 0 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
