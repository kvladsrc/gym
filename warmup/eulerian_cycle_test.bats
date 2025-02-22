#!/usr/bin/env bats

RUN="bazel run :eulerian_cycle"

@test "empty graph" {
    input=$'0 0'
    expected=$'YES'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "single vertex" {
    input=$'1 0'
    expected=$'YES'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "two vertices with one edge" {
    input=$'2 1 0 1'
    expected=$'NO'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "complete graph with three vertices" {
    input=$'3 3 0 1 1 2 2 0'
    expected=$'YES'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "disconnected graph" {
    input=$'4 2 0 1 2 3'
    expected=$'NO'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "graph with odd degree vertices" {
    input=$'4 3 0 1 1 2 2 3'
    expected=$'NO'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "graph with even degrees but disconnected" {
    input=$'4 4 0 1 1 0 2 3 3 2'
    expected=$'NO'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "graph with duplicating edges" {
    input=$'2 2 0 1 0 1'
    expected=$'NO'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "graph with isolated vertex" {
    input=$'3 1 0 1'
    expected=$'NO'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
