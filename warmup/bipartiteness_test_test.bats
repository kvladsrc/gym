#!/usr/bin/env bats

RUN="bazel run :bipartiteness_test"

@test "empty graph" {
    input=$'0 0'
    expected="YES"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "single vertex" {
    input=$'1 0'
    expected="YES"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "two vertices with edge 0-1" {
    input=$'2 1 0 1'
    expected="YES"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "odd cycle (triangle)" {
    input=$'3 3 0 1 1 2 2 0'
    expected="NO"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "even cycle (square)" {
    input=$'4 4 0 1 1 2 2 3 3 0'
    expected="YES"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "disconnected bipartite graph" {
    input=$'5 2 0 1 1 2'
    expected="YES"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "disconnected graph with non-bipartite component" {
    input=$'5 4 0 1 1 2 2 0 3 4'
    expected="NO"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
