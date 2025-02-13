#!/usr/bin/env bats

RUN="bazel run :merge_sort"

@test "empty" {
    input=$'0'
    expected=$''
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one element" {
    input=$'1 1'
    expected=$'1 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "already sorted" {
    input=$'10 1 2 3 4 5 6 7 8 9 10'
    expected=$'1 2 3 4 5 6 7 8 9 10 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "already sorted (reversed)" {
    input=$'10 10 9 8 7 6 5 4 3 2 1'
    expected=$'1 2 3 4 5 6 7 8 9 10 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "dubplicates" {
    input=$'10 1 3 3 3 3 3 3 3 4 2'
    expected=$'1 2 3 3 3 3 3 3 3 4 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "all equal elements" {
    input=$'5 1 1 1 1 1'
    expected=$'1 1 1 1 1 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "random order" {
    input=$'6 4 1 3 2 6 5'
    expected=$'1 2 3 4 5 6 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "negative numbers" {
    input=$'6 -3 -1 -2 -4 0 5'
    expected=$'-4 -3 -2 -1 0 5 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one element negative" {
    input=$'1 -1'
    expected=$'-1 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
