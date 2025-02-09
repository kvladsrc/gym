#!/usr/bin/env bats

RUN="bazel run :combinations"

@test "C(0, 0) = 1" {
    input=$'0 0'
    expected="1"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "C(5, 2) = 10" {
    input=$'5 2'
    expected="10"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "C(10, 3) = 120" {
    input=$'10 3'
    expected="120"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "C(10, 10) = 1" {
    input=$'10 10'
    expected="1"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "C(10, 0) = 1" {
    input=$'10 0'
    expected="1"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "C(5, 6) (k > n) = 0" {
    input=$'5 6'
    expected="0"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "C(100, 50) = 538992043" {
    input=$'100 50'
    expected="538992043"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
