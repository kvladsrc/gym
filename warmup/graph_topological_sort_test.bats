#!/usr/bin/env bats

RUN="bazel run :graph_topological_sort"

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

@test "two vertices with edge 0->1" {
    input=$'2 1 0 1'
    expected=$'0 3\n1 2'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "two vertices with edge 1->0" {
    input=$'2 1 1 0'
    expected=$'0 1\n1 3'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "linear chain" {
    input=$'4 3 0 1 1 2 2 3'
    expected=$'0 7\n1 6\n2 5\n3 4'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "disconnected graph with two chains" {
    input=$'4 2 0 1 2 3'
    expected=$'0 3\n1 2\n2 7\n3 6'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "branching graph" {
    input=$'4 4 0 1 0 2 1 3 2 3'
    expected=$'0 7\n1 4\n2 6\n3 3'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
