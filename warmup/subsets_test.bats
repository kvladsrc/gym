#!/usr/bin/env bats

RUN="bazel run :subsets"

@test "empty set" {
    input=$'0'
    expected=$''
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one element" {
    input=$'1 42'
    expected=$'\n42 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "two elements" {
    input=$'2 10 20'
    expected=$'\n10 \n20 \n10 20 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "three elements" {
    input=$'3 1 2 3'
    expected=$'\n1 \n2 \n1 2 \n3 \n1 3 \n2 3 \n1 2 3 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "set too large" {
    input="65"
    for i in $(seq 1 65); do
        input="${input}\n1"
    done
    expected="TOO LARGE"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
