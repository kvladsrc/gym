#!/usr/bin/env bats

RUN="bazel run :factor"

@test "zero" {
    input=0
    expected=""
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one" {
    input=1
    expected=""
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "prime" {
    input=5
    expected="5 "
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "square" {
    input=9
    expected="3 3 "
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "negative" {
    input=-6
    expected="2 3 "
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "twelve" {
    input=12
    expected="2 2 3 "
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "two" {
    input=2
    expected="2 "
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
