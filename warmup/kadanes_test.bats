#!/usr/bin/env bats

RUN="bazel run :kadanes"

@test "empty array" {
    input="0"
    expected="0"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "single positive element" {
    input="1 42"
    expected="42"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "single negative element" {
    input="1 -5"
    expected="0"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "all negative array" {
    input="5 -8 -3 -6 -2 -7"
    expected="0"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "mixed array" {
    input="5 2 -1 2 3 -9"
    expected="6"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "all positive array" {
    input="4 1 2 3 4"
    expected="10"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
