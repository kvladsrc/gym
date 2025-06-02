#!/usr/bin/env bats

RUN="bazel run :3sum"

@test "empty array" {
    input=$'0 0'
    expected="NO"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "less than three elements" {
    input=$'2 1 1 3'
    expected="NO"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "three elements sum exists" {
    input=$'3 1 2 3 6'
    expected="YES"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "three elements sum not exists" {
    input=$'3 1 2 4 8'
    expected="NO"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "duplicates" {
    input=$'4 2 2 3 2 7'
    expected="YES"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "all equal elements" {
    input=$'5 2 2 2 2 2 6'
    expected="YES"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "negative numbers" {
    input=$'3 -1 0 1 0'
    expected="YES"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "with zero sum" {
    input=$'4 -5 -3 2 8 0'
    expected="YES"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "large numbers" {
    input=$'4 1000000000 1000000000 -1000000000 0'
    expected="YES" # 1e9 + 1e9 + (-1e9) = 1e9
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "multiple triplets" {
    input=$'6 1 2 3 4 5 6 9'
    expected="YES"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
