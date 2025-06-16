#!/usr/bin/env bats

RUN="bazel run :trie"

@test "exact match" {
    input=$'abc abc'
    expected=$'YES'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "different word" {
    input=$'abc abd'
    expected=$'NO'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "prefix only" {
    input=$'abcd abc'
    expected=$'YES'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "longer word" {
    input=$'abc abcd'
    expected=$'NO'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "single letter" {
    input=$'a a'
    expected=$'YES'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "case sensitive" {
    input=$'Hello hello'
    expected=$'NO'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "empty pattern" {
    input=$(printf 'abc \n')
    expected=$'YES'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "unicode symbols" {
    input=$'привет привет'
    expected=$'YES'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "non-latin mismatch" {
    input=$'привет пока'
    expected=$'NO'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
