#!/usr/bin/env bats

RUN="bazel run :dsu"

@test "one element" {
    input="1 0 1 0 0"
    expected="CONNECTED"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "two elements, connected" {
    input="2 1 0 1 2 0 1 1 0"
    result="$(echo "$input" | $RUN)"
    expected="CONNECTED
CONNECTED"
    [ "$result" = "$expected" ]
}

@test "two elements, disjoined" {
    input="2 0 2 0 1 1 0"
    result="$(echo "$input" | $RUN)"
    expected="DISCONNECTED
DISCONNECTED"
    [ "$result" = "$expected" ]
}

@test "two connected components" {
    input="5 3 0 1 1 2 3 4 4 0 2 0 3 3 4 2 4"
    result="$(echo "$input" | $RUN)"
    expected="CONNECTED
DISCONNECTED
CONNECTED
DISCONNECTED"
    [ "$result" = "$expected" ]
}

@test "connected componets unite" {
    input="6 4 0 1 2 3 1 2 4 5 3 0 3 1 4 4 5"
    result="$(echo "$input" | $RUN)"
    expected="CONNECTED
DISCONNECTED
CONNECTED"
    [ "$result" = "$expected" ]
}

@test "already joined" {
    input="3 3 0 1 1 2 0 2 3 0 1 1 2 0 2"
    result="$(echo "$input" | $RUN)"
    expected="CONNECTED
CONNECTED
CONNECTED"
    [ "$result" = "$expected" ]
}
