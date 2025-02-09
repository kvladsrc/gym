#!/usr/bin/env bats

RUN="bazel run :floyd_warshall"

@test "empty graph (n=1)" {
    input=$'1 0'
    expected=$'0 0 0'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "single edge" {
    input=$'2 1\n0 1 10'
    expected=$'0 0 0\n0 1 10\n1 0 INF\n1 1 0'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "multiple paths" {
    input=$'3 3\n0 1 1\n1 2 2\n0 2 5'
    expected=$'0 0 0\n0 1 1\n0 2 3\n1 0 INF\n1 1 0\n1 2 2\n2 0 INF\n2 1 INF\n2 2 0'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "negative weights" {
    input=$'2 2\n0 1 -3\n1 0 5'
    expected=$'0 0 0\n0 1 -3\n1 0 5\n1 1 0'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}

@test "unreachable vertices" {
    input=$'3 1\n0 1 10'
    expected=$'0 0 0\n0 1 10\n0 2 INF\n1 0 INF\n1 1 0\n1 2 INF\n2 0 INF\n2 1 INF\n2 2 0'
    result="$(echo $input | $RUN)"
    [ "$result" = "$expected" ]
}
