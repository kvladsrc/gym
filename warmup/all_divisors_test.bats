#!/usr/bin/env bats

RUN="bazel run :all_divisors"

@test "zero" {
    input=0
    expected=""
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one" {
    input=1
    expected="1 "
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "prime" {
    input=5
    expected="1 5 "
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "square" {
    input=9
    expected="1 3 9 "
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "negative" {
    input=-6
    expected="1 2 3 6 "
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "twelve" {
    input=12
    expected="1 2 3 4 6 12 "
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "two" {
    input=2
    expected="1 2 "
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
