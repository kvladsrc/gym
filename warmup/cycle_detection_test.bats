#!/usr/bin/env bats

RUN="bazel run :cycle_detection"

@test "empty graph" {
    input=$'0 0'
    expected=$'NO'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "single vertex, no edges" {
    input=$'1 0'
    expected=$'NO'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "self loop" {
    input=$'1 1 0 0'
    expected=$'YES'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "single edge, no cycle" {
    input=$'2 1 0 1'
    expected=$'NO'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "triangle" {
    input=$'3 3 0 1 1 2 2 0'
    expected=$'YES'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "tree" {
    input=$'5 4 0 1 1 2 2 3 3 4'
    expected=$'NO'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "square with diagonal" {
    input=$'4 5 0 1 1 2 2 3 3 0 0 2'
    expected=$'YES'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "duplicate edge" {
    input=$'2 2 0 1 0 1'
    expected=$'YES'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "several components, no cycles" {
    input=$'6 3 0 1 2 3 4 5'
    expected=$'NO'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "disconnected graph with one cycle" {
    input=$'6 5 0 1 1 2 2 0 3 4 4 5'
    expected=$'YES'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
