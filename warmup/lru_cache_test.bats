#!/usr/bin/env bats

RUN="bazel run :lru_cache"

@test "empty cache" {
    input=$'0 1 5'
    expected=$'MISS MISS MISS'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one element" {
    input=$'1 42 100 1 42 '
    expected=$'42 100 42'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "existing key not at front" {
    input=$'3 1 10 2 20 3 30 1 2'
    expected=$'3 20 2'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "non-existing key" {
    input=$'3 1 10 2 20 3 30 1 5'
    expected=$'3 MISS 3'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "multiple queries" {
    input=$'3 1 10 2 20 3 30 3 1 2 3'
    expected=$'3 10 1\n1 20 2\n2 30 3'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "duplicate keys" {
    input=$'2 1 10 1 100 1 1'
    expected=$'1 100 1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "negative numbers" {
    input=$'3 -1 -10 -2 -20 -3 -30 1 -2'
    expected=$'-3 -20 -2'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
