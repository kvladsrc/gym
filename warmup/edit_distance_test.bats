#!/usr/bin/env bats

RUN="bazel run :edit_distance"

@test "empty strings" {
    input=$'\n'
    expected='0'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "identical strings" {
    input=$'test\ntest'
    expected='0'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "insert operations only" {
    input=$'abc\nabcde'
    expected='2'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "delete operations only" {
    input=$'abcdef\nabc'
    expected='3'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "replace operations only" {
    input=$'abc\nxyz'
    expected='3'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "mixed operations" {
    input=$'intention\nexecution'
    expected='5'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "single empty and non-empty string" {
    input=$'\nhello'
    expected='5'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one character difference" {
    input=$'cat\ncut'
    expected='1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "reverse strings" {
    input=$'abcd\ndcba'
    expected='4'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "longer strings with multiple edits" {
    input=$'algorithm\naltruistic'
    expected='6'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
