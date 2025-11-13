#!/usr/bin/env bats

RUN="bazel run :strongly_connected_components"

@test "empty graph" {
    input=$'0 0'
    expected=""
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "single vertex" {
    input=$'1 0'
    expected=$'0 1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "isolated vertices" {
    input=$'3 0'
    expected=$'0 3\n1 2\n2 1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "cycle of three vertices" {
    input=$'3 3 0 1 1 2 2 0'
    expected=$'0 1\n1 1\n2 1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "two SCCs" {
    input=$'4 5 0 1 1 0 2 3 3 2 1 2'
    expected=$'0 2\n1 2\n2 1\n3 1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "complex graph with three SCCs" {
    input=$'6 7 0 1 1 2 2 0 3 4 4 3 2 3 4 5'
    expected=$'0 3\n1 3\n2 3\n3 2\n4 2\n5 1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "linear chain" {
    input=$'4 3 0 1 1 2 2 3'
    expected=$'0 4\n1 3\n2 2\n3 1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
